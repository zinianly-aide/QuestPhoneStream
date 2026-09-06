package com.questphonestream.agent

import android.content.Context
import android.os.SystemClock
import android.util.Log
import org.json.JSONArray
import org.json.JSONObject
import java.io.BufferedInputStream
import java.io.BufferedOutputStream
import java.io.FileInputStream
import java.io.InputStream
import java.net.ServerSocket
import java.net.Socket
import java.net.URLDecoder
import java.nio.charset.StandardCharsets
import java.security.SecureRandom
import java.util.Base64
import java.util.concurrent.ConcurrentHashMap
import java.util.concurrent.ExecutorService
import java.util.concurrent.Executors
import java.util.concurrent.atomic.AtomicBoolean

/** Embedded, local-only HTTP server for the user-approved media catalog. */
class MediaHttpServer(
    private val context: Context,
    private val catalog: MediaCatalog,
    requestedPort: Int = DEFAULT_PORT,
    private val pairingTokenProvider: () -> String = { "dev-token" },
    private val streamIdProvider: () -> String = { "" },
    private val signalingEndpointProvider: () -> String = { "" }
) {
    private val server = ServerSocket(requestedPort)
    private val nsdRegistration = MediaNsdRegistration(
        context,
        { port },
        streamIdProvider,
        signalingEndpointProvider
    )
    private val running = AtomicBoolean(false)
    private val workers: ExecutorService = Executors.newCachedThreadPool()
    private val acceptThread = Thread({ acceptLoop() }, "quest-phone-media-accept")
    private val random = SecureRandom()
    private val capabilities = ConcurrentHashMap<String, Capability>()

    val port: Int get() = server.localPort

    fun start() {
        if (running.compareAndSet(false, true)) {
            acceptThread.start()
            nsdRegistration.start()
        }
    }

    fun stop() {
        if (!running.compareAndSet(true, false)) return
        nsdRegistration.stop()
        runCatching { server.close() }
        workers.shutdownNow()
        capabilities.clear()
    }

    private fun acceptLoop() {
        while (running.get()) {
            runCatching { server.accept() }.onSuccess { socket ->
                workers.execute { handle(socket) }
            }.onFailure {
                if (running.get()) Log.w(TAG, "Media HTTP accept failed")
            }
        }
    }

    private fun handle(socket: Socket) {
        socket.use { client ->
            try {
                client.soTimeout = 15_000
                val input = BufferedInputStream(client.getInputStream())
                val output = BufferedOutputStream(client.getOutputStream())
                val requestLine = readLine(input) ?: return
                val request = requestLine.split(' ', limit = 3)
                if (request.size != 3) { sendError(output, 400, "Bad Request"); return }
                val method = request[0].uppercase()
                val target = request[1]
                val headers = LinkedHashMap<String, String>()
                while (true) {
                    val line = readLine(input) ?: return
                    if (line.isEmpty()) break
                    val colon = line.indexOf(':')
                    if (colon > 0) headers[line.substring(0, colon).trim().lowercase()] = line.substring(colon + 1).trim()
                }
                val uri = runCatching { android.net.Uri.parse(target) }.getOrNull()
                val path = uri?.path ?: run { sendError(output, 400, "Bad Request"); return }
                Log.d(TAG, "HTTP $method $path (range=${headers["range"]})")
                when {
                    method == "GET" && path == "/v1/media" ->
                        ifAuthorized(headers, output) { sendCatalog(output) }
                    method == "GET" && path.matches(Regex("/v1/media/[^/]+")) ->
                        ifAuthorized(headers, output) { sendMetadata(output, path.substringAfterLast('/')) }
                    method == "POST" && path.matches(Regex("/v1/media/[^/]+/play-token")) ->
                        ifAuthorized(headers, output) { issueToken(output, path.split('/')[3]) }
                    (method == "GET" || method == "HEAD") && path.matches(Regex("/v1/media/[^/]+/content")) ->
                        sendContent(output, method == "HEAD", path.split('/')[3], uri, headers["range"])
                    else -> sendError(output, 404, "Not Found")
                }
            } catch (e: Exception) {
                Log.e(TAG, "HTTP handler crashed: ${e.javaClass.simpleName}: ${e.message}", e)
                try {
                    val output = BufferedOutputStream(client.getOutputStream())
                    sendError(output, 500, "Internal Server Error")
                } catch (_: Exception) {}
            }
        }
    }

    private inline fun ifAuthorized(
        headers: Map<String, String>,
        output: BufferedOutputStream,
        action: () -> Unit
    ) {
        val token = pairingTokenProvider()
        val authorized = MediaPairingAuth.isAuthorized(headers, token)
        Log.d(TAG, "ifAuthorized: headersKeys=${headers.keys.joinToString(",")} expectedToken=${token.take(5)}... result=$authorized")
        if (!authorized) {
            sendError(output, 401, "Unauthorized")
            return
        }
        action()
    }

    private fun sendCatalog(output: BufferedOutputStream) {
        val array = JSONArray()
        catalog.shared().forEach { array.put(metadataJson(it)) }
        sendJson(output, 200, array.toString())
    }

    private fun sendMetadata(output: BufferedOutputStream, id: String) {
        val item = catalog.get(id)?.takeIf { it.shared } ?: run { sendError(output, 404, "Not Found"); return }
        sendJson(output, 200, metadataJson(item).toString())
    }

    private fun issueToken(output: BufferedOutputStream, id: String) {
        val item = catalog.get(id)?.takeIf { it.shared } ?: run { sendError(output, 404, "Not Found"); return }
        val bytes = ByteArray(24).also(random::nextBytes)
        val token = Base64.getUrlEncoder().withoutPadding().encodeToString(bytes)
        capabilities[token] = Capability(id, SystemClock.elapsedRealtime() + TOKEN_TTL_MS)
        sendJson(output, 200, JSONObject().put("token", token).put("expiresInSeconds", TOKEN_TTL_MS / 1000).toString())
    }

    private fun sendContent(output: BufferedOutputStream, head: Boolean, id: String, uri: android.net.Uri, rangeHeader: String?) {
        val item = catalog.get(id)?.takeIf { it.shared } ?: run { sendError(output, 404, "Not Found"); return }
        val token = uri.getQueryParameter("cap")
        val capability = token?.let { capabilities[it] }
        if (capability == null || capability.mediaId != id || capability.expiresAt < SystemClock.elapsedRealtime()) {
            token?.let(capabilities::remove)
            sendError(output, 401, "Unauthorized")
            return
        }
        Log.d(TAG, "sendContent: id=$id name=${item.displayName} size=${item.size} uri=${item.contentUri} range=$rangeHeader")
        val total = item.size
        if (total < 0) { sendError(output, 416, "Range Not Satisfiable", "bytes */*"); return }
        val range = if (rangeHeader == null) null else RangeParser.parse(rangeHeader, total)
        if (rangeHeader != null && range == null) { sendError(output, 416, "Range Not Satisfiable", "bytes */$total"); return }
        if (!item.seekable && range != null && range.start > 0) {
            sendError(output, 416, "Range Not Satisfiable", "bytes */$total"); return
        }
        val start = range?.start ?: 0L
        val end = range?.end ?: (total - 1)
        val length = (end - start + 1).coerceAtLeast(0)
        val headers = linkedMapOf(
            "Accept-Ranges" to "bytes", "Content-Type" to safeMime(item.mimeType),
            "Content-Length" to length.toString(), "Connection" to "close"
        )
        if (range != null) headers["Content-Range"] = "bytes $start-$end/$total"
        val stream = try {
            openStream(item).also { opened ->
                Log.d(TAG, "sendContent: stream opened, skipping to $start")
                if (!skipFully(opened, start)) {
                    opened.close()
                    throw IllegalStateException("Unable to seek media stream")
                }
            }
        } catch (e: Exception) {
            Log.e(TAG, "sendContent: stream unavailable: ${e.javaClass.simpleName}: ${e.message}")
            sendError(output, 500, "Media stream unavailable")
            return
        }

        // Do not advertise a successful response until the source is open and positioned.
        writeHeaders(output, if (range == null) 200 else 206, if (range == null) "OK" else "Partial Content", headers)
        try {
            if (head || length == 0L) {
                stream.close()
            } else {
                stream.use { positioned ->
                    val buffer = ByteArray(32 * 1024)
                    var remaining = length
                    var totalSent = 0L
                    while (remaining > 0) {
                        val read = positioned.read(buffer, 0, minOf(buffer.size.toLong(), remaining).toInt())
                        if (read <= 0) break
                        output.write(buffer, 0, read)
                        remaining -= read
                        totalSent += read
                    }
                    Log.d(TAG, "sendContent: sent $totalSent / $length bytes")
                }
            }
        } catch (e: Exception) {
            Log.e(TAG, "sendContent: stream error: ${e.javaClass.simpleName}: ${e.message}", e)
            return
        }
        output.flush()
    }

    private fun openStream(item: MediaItem): InputStream =
        context.contentResolver.openInputStream(item.uri()) ?: error("Unable to open media")

    private fun metadataJson(item: MediaItem): JSONObject = JSONObject().apply {
        put("id", item.id); put("name", item.displayName); put("mimeType", item.mimeType)
        put("size", item.size); put("seekable", item.seekable)
        put("projection", item.projection); put("fov", item.fov)
        put("stereo", item.stereo); put("eyeOrder", item.eyeOrder)
    }

    private fun sendJson(output: BufferedOutputStream, code: Int, body: String) {
        val bytes = body.toByteArray(StandardCharsets.UTF_8)
        writeHeaders(output, code, if (code == 200) "OK" else "Error", mapOf(
            "Content-Type" to "application/json; charset=utf-8", "Content-Length" to bytes.size.toString(), "Connection" to "close"
        ))
        output.write(bytes); output.flush()
    }

    private fun sendError(output: BufferedOutputStream, code: Int, message: String, contentRange: String? = null) {
        val headers = mutableMapOf("Content-Length" to "0", "Connection" to "close")
        if (contentRange != null) headers["Content-Range"] = contentRange
        writeHeaders(output, code, message, headers); output.flush()
    }

    private fun writeHeaders(output: BufferedOutputStream, code: Int, reason: String, headers: Map<String, String>) {
        output.write("HTTP/1.1 $code $reason\r\n".toByteArray(StandardCharsets.US_ASCII))
        headers.forEach { (key, value) -> output.write("$key: $value\r\n".toByteArray(StandardCharsets.US_ASCII)) }
        output.write("\r\n".toByteArray(StandardCharsets.US_ASCII))
    }

    private fun readLine(input: InputStream): String? {
        val bytes = java.io.ByteArrayOutputStream()
        while (bytes.size() < 16 * 1024) {
            val value = input.read()
            if (value < 0) return if (bytes.size() == 0) null else bytes.toString(Charsets.UTF_8.name())
            if (value == '\n'.code) break
            if (value != '\r'.code) bytes.write(value)
        }
        return bytes.toString(Charsets.UTF_8.name())
    }

    private fun skipFully(input: InputStream, count: Long): Boolean {
        var remaining = count
        while (remaining > 0) {
            val skipped = input.skip(remaining)
            if (skipped > 0) { remaining -= skipped; continue }
            if (input.read() < 0) return false
            remaining--
        }
        return true
    }

    private fun safeMime(mime: String): String = mime.replace(Regex("[^A-Za-z0-9!#$%&'*+.^_`|~/.\\-]"), "")
        .ifBlank { "video/mp4" }

    private data class Capability(val mediaId: String, val expiresAt: Long)

    companion object {
        const val DEFAULT_PORT = 8788
        private const val TOKEN_TTL_MS = 5 * 60 * 1000L
        private const val TAG = "QuestPhoneMedia"
    }
}

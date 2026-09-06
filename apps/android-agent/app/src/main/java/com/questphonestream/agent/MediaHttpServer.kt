package com.questphonestream.agent

import android.content.Context
import android.os.SystemClock
import android.util.Log
import org.json.JSONArray
import org.json.JSONObject
import java.io.BufferedInputStream
import java.io.BufferedOutputStream
import java.io.InputStream
import java.net.ServerSocket
import java.net.Socket
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
    @Volatile private var appliedConfig: AppliedConfig? = null
    private val nsdRegistration = MediaNsdRegistration(
        context,
        { port },
        { appliedConfig?.deviceId.orEmpty() },
        { appliedConfig?.signalingUrl.orEmpty() },
        spatialReadyProvider = { DeviceControlPlane.isSpatialReady }
    )
    private val running = AtomicBoolean(false)
    private val workers: ExecutorService = Executors.newCachedThreadPool()
    private val acceptThread = Thread({ acceptLoop() }, "quest-phone-media-accept")
    private val random = SecureRandom()
    private val capabilities = ConcurrentHashMap<String, Capability>()
    private val mediaLifecycle = MediaCapabilityLifecycle { state ->
        DeviceControlPlane.reportCapabilityState(state.name, state.available, state.authorized, state.active)
        CapabilityRuntime.setMediaCapability(state.name, state.available, state.authorized, state.active)
    }
    private var controlPlaneListenerAttached = false
    private var lastSpatialReady = false

    private val controlPlaneListener = object : SignalingClient.Listener {
        override fun onStateChanged(state: ConnectionState) {
            val ready = state == ConnectionState.CONNECTED
            if (ready == lastSpatialReady) return
            lastSpatialReady = ready
            nsdRegistration.refreshUnifiedAdvertisement()
        }
    }

    val port: Int get() = server.localPort

    fun start() {
        if (running.compareAndSet(false, true)) {
            DeviceControlPlane.acquire(DeviceControlPlane.Owner.MEDIA)
            if (!controlPlaneListenerAttached) {
                DeviceControlPlane.addListener(controlPlaneListener, replay = false)
                controlPlaneListenerAttached = true
            }
            val initial = AppliedConfigStore.initializeIfAbsent(
                signalingUrl = signalingEndpointProvider().trim(),
                token = pairingTokenProvider(),
                deviceId = streamIdProvider().trim().ifBlank { MediaDeviceIdentity.getOrCreateDeviceId(context) }
            )
            appliedConfig = initial
            DeviceControlPlane.configure(initial.signalingUrl, initial.token, initial.deviceId)
            mediaLifecycle.startServer()
            lastSpatialReady = DeviceControlPlane.isSpatialReady
            acceptThread.start()
            nsdRegistration.start()
        }
    }

    fun stop() {
        if (!running.compareAndSet(true, false)) return
        nsdRegistration.stop()
        capabilities.clear()
        mediaLifecycle.stopServer()
        if (controlPlaneListenerAttached) {
            DeviceControlPlane.removeListener(controlPlaneListener)
            controlPlaneListenerAttached = false
        }
        DeviceControlPlane.release(DeviceControlPlane.Owner.MEDIA)
        runCatching { server.close() }
        workers.shutdownNow()
    }

    /**
     * Explicit Save/Apply entrypoint. Draft providers are sampled only here, committed
     * into AppliedConfig, then the live control plane is reconfigured exactly once and
     * the unified NSD advertisement is refreshed exactly once.
     */
    fun refreshNsdMetadata() {
        val next = AppliedConfigStore.apply(
            signalingUrl = signalingEndpointProvider().trim(),
            token = pairingTokenProvider(),
            deviceId = streamIdProvider().trim().ifBlank { MediaDeviceIdentity.getOrCreateDeviceId(context) }
        )
        val previous = appliedConfig
        appliedConfig = next
        DeviceControlPlane.configure(next.signalingUrl, next.token, next.deviceId)
        if (previous?.token != null && previous.token != next.token) capabilities.clear()
        mediaLifecycle.resetPairingAuthorization()
        nsdRegistration.refreshUnifiedAdvertisement()
    }

    fun refreshUnifiedAdvertisement() = refreshNsdMetadata()

    /** Compatibility alias retained for callers using the older method name. */
    fun refreshDiscoveryMetadata() = refreshNsdMetadata()

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
                        ifAuthorized(MediaCapabilityLifecycle.MEDIA_LIST, headers, output) { sendCatalog(output) }
                    method == "GET" && path.matches(Regex("/v1/media/[^/]+")) ->
                        ifAuthorized(MediaCapabilityLifecycle.MEDIA_OPEN, headers, output) { sendMetadata(output, path.substringAfterLast('/')) }
                    method == "POST" && path.matches(Regex("/v1/media/[^/]+/play-token")) ->
                        ifAuthorized(MediaCapabilityLifecycle.MEDIA_OPEN, headers, output) { issueToken(output, path.split('/')[3]) }
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
        capabilityName: String,
        headers: Map<String, String>,
        output: BufferedOutputStream,
        action: () -> Unit
    ) {
        val token = appliedConfig?.token.orEmpty()
        val authorized = token.isNotEmpty() && MediaPairingAuth.isAuthorized(headers, token)
        Log.d(TAG, "ifAuthorized: capability=$capabilityName headersKeys=${headers.keys.joinToString(",")} result=$authorized")
        if (!authorized) {
            sendError(output, 401, "Unauthorized")
            return
        }
        mediaLifecycle.markPairingAuthorized()
        if (!mediaLifecycle.beginRequest(capabilityName)) {
            sendError(output, 503, "Service Unavailable")
            return
        }
        try { action() } finally { mediaLifecycle.endRequest(capabilityName) }
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

        // A valid short-lived play capability proves the request passed pairing when it
        // was minted, so media.publish is authorized for this request lifecycle.
        mediaLifecycle.markPairingAuthorized()
        if (!mediaLifecycle.beginRequest(MediaCapabilityLifecycle.MEDIA_PUBLISH)) {
            sendError(output, 503, "Service Unavailable")
            return
        }
        try {
            sendAuthorizedContent(output, head, item, rangeHeader)
        } finally {
            mediaLifecycle.endRequest(MediaCapabilityLifecycle.MEDIA_PUBLISH)
        }
    }

    private fun sendAuthorizedContent(output: BufferedOutputStream, head: Boolean, item: MediaItem, rangeHeader: String?) {
        Log.d(TAG, "sendContent: id=${item.id} name=${item.displayName} size=${item.size} range=$rangeHeader")
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

package com.questphonestream.agent

import android.content.Context
import java.io.ByteArrayOutputStream
import java.io.IOException
import java.net.InetSocketAddress
import java.net.Socket

/** One-time opt-in pull from Quest. Raw LAN TCP with Bearer secret; HTTP is NOT encrypted. */
internal object ScenePreviewLanImport {
    data class Endpoint(val host: String, val port: Int)

    fun parseEndpoint(address: String): Endpoint {
        val parts = address.trim().split(':')
        require(parts.size == 2 && parts[0].matches(Regex("[0-9.]{7,15}"))) {
            "Enter a private IPv4 address and port, e.g. 192.168.1.12:12345"
        }
        val octets = parts[0].split('.').map { it.toIntOrNull() ?: -1 }
        require(octets.size == 4 && octets.all { it in 0..255 }) { "Invalid IPv4 address" }
        require(octets[0] == 10 || (octets[0] == 172 && octets[1] in 16..31) ||
            (octets[0] == 192 && octets[1] == 168)) { "Only local private Wi-Fi addresses are allowed" }
        val port = parts[1].toIntOrNull() ?: throw IllegalArgumentException("Port required")
        require(port in 1024..65535) { "Invalid port" }
        return Endpoint(parts[0], port)
    }

    fun fetchJson(address: String, bearer: String): String {
        val endpoint = parseEndpoint(address)
        require(bearer.matches(Regex("[A-Za-z0-9_-]{32}"))) { "Invalid one-time Quest key" }
        Socket().use { socket ->
            socket.connect(InetSocketAddress(endpoint.host, endpoint.port), 4500)
            socket.soTimeout = 5500
            val input = socket.getInputStream()
            val output = socket.getOutputStream()
            output.write(("GET /v1/room-preview HTTP/1.1\r\n"+
                "Host: ${endpoint.host}\r\nAuthorization: Bearer $bearer\r\n"+
                "Accept: application/json\r\nConnection: close\r\n\r\n").toByteArray(Charsets.US_ASCII))
            output.flush()
            val header = ByteArrayOutputStream()
            var b: Int
            var last = 0
            while (header.size() < 3072) {
                b = input.read()
                if (b < 0) break
                header.write(b)
                last = ((last shl 8) or b) // CRLFCRLF
                if (last == 0x0D0A0D0A) break
            }
            val raw = header.toString("US-ASCII")
            require(raw.endsWith("\r\n\r\n") && raw.startsWith("HTTP/1.1 200 ")) {
                "Quest did not accept the one-time key or the offer expired"
            }
            val size = Regex("(?im)^Content-Length: (\\d+)\\r?$").find(raw)?.groupValues?.get(1)?.toIntOrNull()
                ?: throw IOException("Missing response length")
            require(size in 1..(1024 * 1024)) { "Room snapshot is too large" }
            val body = ByteArray(size)
            var offset = 0
            while (offset < size) {
                val read = input.read(body, offset, size - offset)
                if (read < 0) throw IOException("Incomplete room snapshot")
                offset += read
            }
            val json = String(body, Charsets.UTF_8)
            ScenePreviewStore.validate(json)
            return json
        }
    }

    fun import(context: Context, address: String, key: String): String =
        ScenePreviewStore.importJson(context, fetchJson(address, key))
}

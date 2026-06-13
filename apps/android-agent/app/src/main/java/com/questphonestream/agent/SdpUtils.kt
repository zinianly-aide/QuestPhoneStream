package com.questphonestream.agent

object SdpUtils {
    fun preferH264(sdp: String): String {
        val lines = sdp.split("\r\n").toMutableList()
        val h264Payloads = lines.mapNotNull { line ->
            val match = Regex("^a=rtpmap:(\\d+) H264/90000", RegexOption.IGNORE_CASE).find(line)
            match?.groupValues?.get(1)
        }
        if (h264Payloads.isEmpty()) return sdp

        val videoLineIndex = lines.indexOfFirst { it.startsWith("m=video ") }
        if (videoLineIndex < 0) return sdp

        val parts = lines[videoLineIndex].split(" ").toMutableList()
        val header = parts.take(3)
        val payloads = parts.drop(3)
        val reordered = h264Payloads + payloads.filterNot { it in h264Payloads }
        lines[videoLineIndex] = (header + reordered).joinToString(" ")
        return lines.joinToString("\r\n")
    }
}


package com.questphonestream.agent

data class ByteRange(val start: Long, val end: Long, val total: Long) {
    val length: Long get() = end - start + 1
}

object RangeParser {
    fun parse(value: String?, total: Long): ByteRange? {
        if (total < 0 || value.isNullOrBlank() || !value.startsWith("bytes=")) return null
        val spec = value.removePrefix("bytes=").trim()
        if (spec.contains(',')) return null // multipart ranges are intentionally not supported in MVP
        val parts = spec.split('-', limit = 2)
        if (parts.size != 2) return null
        return runCatching {
            val startText = parts[0].trim()
            val endText = parts[1].trim()
            val start: Long
            val end: Long
            if (startText.isEmpty()) {
                val suffix = endText.toLong()
                if (suffix <= 0) return null
                start = (total - suffix).coerceAtLeast(0)
                end = total - 1
            } else {
                start = startText.toLong()
                if (start < 0 || start >= total) return null
                end = if (endText.isEmpty()) total - 1 else endText.toLong().coerceAtMost(total - 1)
                if (end < start) return null
            }
            ByteRange(start, end, total)
        }.getOrNull()
    }
}

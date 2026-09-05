package com.questphonestream.agent

import java.nio.charset.StandardCharsets
import java.security.MessageDigest

/** Authentication for the short-lived local media API control plane. */
internal object MediaPairingAuth {
    const val HEADER_NAME = "authorization"

    fun isAuthorized(headers: Map<String, String>, expectedToken: String): Boolean {
        val expected = expectedToken.trim()
        if (expected.isEmpty()) return false
        val raw = headers.entries.firstOrNull { it.key.equals(HEADER_NAME, ignoreCase = true) }
            ?.value?.trim() ?: return false
        if (!raw.regionMatches(0, "Bearer ", 0, 7, ignoreCase = true)) return false
        val supplied = raw.substring(7).trim()
        if (supplied.isEmpty()) return false
        return MessageDigest.isEqual(
            supplied.toByteArray(StandardCharsets.UTF_8),
            expected.toByteArray(StandardCharsets.UTF_8)
        )
    }
}

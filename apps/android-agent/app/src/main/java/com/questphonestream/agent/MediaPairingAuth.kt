package com.questphonestream.agent

import android.util.Log
import java.nio.charset.StandardCharsets
import java.security.MessageDigest

/** Authentication for the short-lived local media API control plane. */
internal object MediaPairingAuth {
    const val HEADER_NAME = "authorization"
    private const val TAG = "QuestPhoneMedia"

    fun isAuthorized(headers: Map<String, String>, expectedToken: String): Boolean {
        val expected = expectedToken.trim()
        if (expected.isEmpty()) {
            Log.d(TAG, "isAuthorized: expected token is empty -> false")
            return false
        }
        val authEntry = headers.entries.firstOrNull { it.key.equals(HEADER_NAME, ignoreCase = true) }
        if (authEntry == null) {
            Log.d(TAG, "isAuthorized: no authorization header found, keys=${headers.keys.joinToString(",")} -> false")
            return false
        }
        val raw = authEntry.value.trim()
        if (!raw.regionMatches(0, "Bearer ", 0, 7, ignoreCase = true)) {
            Log.d(TAG, "isAuthorized: header doesn't start with 'Bearer ' -> false")
            return false
        }
        val supplied = raw.substring(7).trim()
        if (supplied.isEmpty()) {
            Log.d(TAG, "isAuthorized: supplied token is empty -> false")
            return false
        }
        val match = MessageDigest.isEqual(
            supplied.toByteArray(StandardCharsets.UTF_8),
            expected.toByteArray(StandardCharsets.UTF_8)
        )
        Log.d(TAG, "isAuthorized: token match=$match")
        return match
    }
}

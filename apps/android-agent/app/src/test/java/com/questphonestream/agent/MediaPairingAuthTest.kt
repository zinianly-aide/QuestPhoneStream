package com.questphonestream.agent

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class MediaPairingAuthTest {
    @Test fun bearerPairingTokenIsAccepted() {
        assertTrue(MediaPairingAuth.isAuthorized(mapOf("Authorization" to "Bearer pair-secret"), "pair-secret"))
    }

    @Test fun missingWrongOrMalformedPairingTokenIsRejected() {
        assertFalse(MediaPairingAuth.isAuthorized(emptyMap(), "pair-secret"))
        assertFalse(MediaPairingAuth.isAuthorized(mapOf("Authorization" to "Bearer wrong"), "pair-secret"))
        assertFalse(MediaPairingAuth.isAuthorized(mapOf("Authorization" to "pair-secret"), "pair-secret"))
        assertFalse(MediaPairingAuth.isAuthorized(mapOf("Authorization" to "Bearer pair-secret"), ""))
    }
}

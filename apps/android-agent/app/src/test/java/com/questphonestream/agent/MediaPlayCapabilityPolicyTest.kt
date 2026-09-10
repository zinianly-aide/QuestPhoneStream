package com.questphonestream.agent

import org.junit.Assert.assertTrue
import org.junit.Test

class MediaPlayCapabilityPolicyTest {
    @Test
    fun playTokenWindowCoversLongRunningLocalPlayback() {
        assertTrue(MediaHttpServer.TOKEN_TTL_MS >= 6L * 60 * 60 * 1000)
    }
}

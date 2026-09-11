package com.questphonestream.agent

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class MediaCapabilityLifecycleTest {
    @Test
    fun serverPairingAndRequestDriveRuntimeState() {
        val reported = mutableListOf<MediaCapabilitySnapshot>()
        val lifecycle = MediaCapabilityLifecycle(reported::add)

        lifecycle.startServer()
        var state = lifecycle.snapshot(MediaCapabilityLifecycle.MEDIA_LIST)
        assertTrue(state.available)
        assertFalse(state.authorized)
        assertFalse(state.active)

        lifecycle.markPairingAuthorized()
        state = lifecycle.snapshot(MediaCapabilityLifecycle.MEDIA_LIST)
        assertTrue(state.authorized)
        assertFalse(state.active)

        assertTrue(lifecycle.beginRequest(MediaCapabilityLifecycle.MEDIA_LIST))
        assertTrue(lifecycle.snapshot(MediaCapabilityLifecycle.MEDIA_LIST).active)
        lifecycle.endRequest(MediaCapabilityLifecycle.MEDIA_LIST)
        assertFalse(lifecycle.snapshot(MediaCapabilityLifecycle.MEDIA_LIST).active)

        lifecycle.resetPairingAuthorization()
        assertFalse(lifecycle.snapshot(MediaCapabilityLifecycle.MEDIA_OPEN).authorized)
        lifecycle.stopServer()
        assertFalse(lifecycle.snapshot(MediaCapabilityLifecycle.MEDIA_PUBLISH).available)
    }
}

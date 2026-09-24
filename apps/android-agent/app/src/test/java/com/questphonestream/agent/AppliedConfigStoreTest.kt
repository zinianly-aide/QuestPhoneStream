package com.questphonestream.agent

import org.junit.Assert.assertEquals
import org.junit.Test

class AppliedConfigStoreTest {
    @Test
    fun draftDoesNotAffectAppliedEndpointUntilExplicitApply() {
        AppliedConfigStore.resetForTests()
        val initial = AppliedConfigStore.initializeIfAbsent("ws://old:8787", "old-token", "old-device")
        val draft = StreamConfig(
            signalingUrl = "ws://new:8787",
            token = "new-token",
            deviceId = "new-device",
            questDeviceId = "quest",
            sessionId = "session"
        )

        val beforeApply = AppliedConfigStore.merge(draft)
        assertEquals(initial.signalingUrl, beforeApply.signalingUrl)
        assertEquals(initial.token, beforeApply.token)
        assertEquals(initial.deviceId, beforeApply.deviceId)

        AppliedConfigStore.apply(draft)
        val afterApply = AppliedConfigStore.merge(draft)
        assertEquals("ws://new:8787", afterApply.signalingUrl)
        assertEquals("new-token", afterApply.token)
        assertEquals("new-device", afterApply.deviceId)
        AppliedConfigStore.resetForTests()
    }
}

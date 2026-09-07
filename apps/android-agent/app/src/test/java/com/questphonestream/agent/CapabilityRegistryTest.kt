package com.questphonestream.agent

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class CapabilityRegistryTest {
    @Test
    fun androidRegistryOnlyAdvertisesImplementedCapabilities() {
        val registry = CapabilityRegistry.androidDefaults()
        assertEquals(
            setOf("display.publish", "display.control", "media.list", "media.open", "media.publish"),
            registry.all().map { it.name }.toSet()
        )
        assertTrue(registry.all().all { it.state.available })
        assertTrue(registry.all().none { it.name.startsWith("camera.") || it.name.startsWith("ai.") || it.name.contains("hand") })
    }

    @Test
    fun authorizationAndActiveStateAreIndependentAndEmitChanges() {
        val registry = CapabilityRegistry.androidDefaults()
        var notifications = 0
        registry.addChangedListener { notifications += 1 }
        assertTrue(registry.updateState("display.publish", authorized = true, active = true))
        val display = registry.all().single { it.name == "display.publish" }
        assertTrue(display.state.available)
        assertTrue(display.state.authorized)
        assertTrue(display.state.active)
        assertEquals(1, notifications)
        assertFalse(registry.updateState("display.publish", authorized = true, active = true))
        assertEquals(1, notifications)
    }
}

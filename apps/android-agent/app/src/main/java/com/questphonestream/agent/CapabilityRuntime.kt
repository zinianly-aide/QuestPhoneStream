package com.questphonestream.agent

data class CapabilityRuntimeState(
    val name: String,
    val available: Boolean,
    val authorized: Boolean,
    val active: Boolean,
    val transport: String
)

/** Process-local state only; it never changes Android secure settings or transport protocols. */
internal object CapabilityRuntime {
    @Volatile private var displayPublish = CapabilityRuntimeState("display.publish", true, false, false, "WebRTC video")
    @Volatile private var displayControl = CapabilityRuntimeState("display.control", false, false, false, "DataChannel")
    @Volatile private var mediaCatalog = CapabilityRuntimeState("media.catalog", false, false, false, "HTTP")
    @Volatile private var accessibilityAvailable = false
    @Volatile private var dataChannelAuthorized = false
    @Volatile private var dataChannelActive = false

    fun setDisplayPublish(authorized: Boolean, active: Boolean) {
        displayPublish = displayPublish.copy(authorized = authorized, active = active)
    }

    @Synchronized
    fun setAccessibilityAvailable(available: Boolean) {
        accessibilityAvailable = available
        updateDisplayControl()
    }

    @Synchronized
    fun setDisplayControl(authorized: Boolean, active: Boolean) {
        dataChannelAuthorized = authorized
        dataChannelActive = active
        updateDisplayControl()
    }

    fun setMediaCatalog(available: Boolean, authorized: Boolean, active: Boolean) {
        mediaCatalog = mediaCatalog.copy(available = available, authorized = authorized, active = active)
    }

    @Synchronized
    fun snapshot(): List<CapabilityRuntimeState> = listOf(displayPublish, displayControl, mediaCatalog)

    private fun updateDisplayControl() {
        displayControl = displayControl.copy(
            available = accessibilityAvailable,
            authorized = accessibilityAvailable && dataChannelAuthorized,
            active = accessibilityAvailable && dataChannelActive
        )
    }
}

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
    @Volatile private var mediaList = CapabilityRuntimeState("media.list", false, false, false, "HTTP")
    @Volatile private var mediaOpen = CapabilityRuntimeState("media.open", false, false, false, "HTTP")
    @Volatile private var mediaPublish = CapabilityRuntimeState("media.publish", false, false, false, "HTTP Range")
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

    @Synchronized
    fun setMediaCapability(name: String, available: Boolean, authorized: Boolean, active: Boolean) {
        val transport = if (name == "media.publish") "HTTP Range" else "HTTP"
        val state = CapabilityRuntimeState(name, available, authorized, active, transport)
        when (name) {
            "media.list" -> mediaList = state
            "media.open" -> mediaOpen = state
            "media.publish" -> mediaPublish = state
            else -> return
        }
        mediaCatalog = mediaCatalog.copy(
            available = mediaList.available || mediaOpen.available || mediaPublish.available,
            authorized = mediaList.authorized || mediaOpen.authorized || mediaPublish.authorized,
            active = mediaList.active || mediaOpen.active || mediaPublish.active
        )
    }

    @Synchronized
    fun setMediaCatalog(available: Boolean, authorized: Boolean, active: Boolean) {
        setMediaCapability("media.list", available, authorized, active)
        setMediaCapability("media.open", available, authorized, active)
        setMediaCapability("media.publish", available, authorized, active)
    }

    @Synchronized
    fun snapshot(): List<CapabilityRuntimeState> = listOf(
        displayPublish, displayControl, mediaCatalog, mediaList, mediaOpen, mediaPublish
    )

    private fun updateDisplayControl() {
        displayControl = displayControl.copy(
            available = accessibilityAvailable,
            authorized = accessibilityAvailable && dataChannelAuthorized,
            active = accessibilityAvailable && dataChannelActive
        )
    }
}

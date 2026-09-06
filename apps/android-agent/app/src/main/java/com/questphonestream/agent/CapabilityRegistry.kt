package com.questphonestream.agent

data class CapabilityState(
    val available: Boolean,
    val authorized: Boolean,
    val active: Boolean
)

data class CapabilityLimit(val name: String, val value: String)

data class CapabilityDescriptor(
    val name: String,
    val version: String = "1.0",
    val state: CapabilityState,
    val transports: List<String>,
    val features: List<String> = emptyList(),
    val limits: List<CapabilityLimit> = emptyList(),
    val permissions: List<String> = emptyList()
)

class CapabilityRegistry private constructor(capabilities: List<CapabilityDescriptor>) {
    private val values = LinkedHashMap<String, CapabilityDescriptor>()
    private val listeners = mutableListOf<(List<CapabilityDescriptor>) -> Unit>()

    init {
        capabilities.forEach { descriptor ->
            require(descriptor.name.matches(CAPABILITY_NAME)) { "Invalid capability name: ${descriptor.name}" }
            require(!values.containsKey(descriptor.name)) { "Duplicate capability: ${descriptor.name}" }
            values[descriptor.name] = descriptor
        }
    }

    @Synchronized
    fun all(): List<CapabilityDescriptor> = values.values.toList()

    @Synchronized
    fun addChangedListener(listener: (List<CapabilityDescriptor>) -> Unit) {
        listeners += listener
    }

    fun updateState(name: String, authorized: Boolean? = null, active: Boolean? = null): Boolean {
        val snapshot: List<CapabilityDescriptor>
        val callbacks: List<(List<CapabilityDescriptor>) -> Unit>
        synchronized(this) {
            val current = values[name] ?: return false
            val nextState = current.state.copy(
                authorized = authorized ?: current.state.authorized,
                active = active ?: current.state.active
            )
            if (nextState == current.state) return false
            values[name] = current.copy(state = nextState)
            snapshot = values.values.toList()
            callbacks = listeners.toList()
        }
        callbacks.forEach { it(snapshot) }
        return true
    }

    companion object {
        private val CAPABILITY_NAME = Regex("^(display|media|xr|camera|audio|spatial|ai|input)\\.[a-z0-9][a-z0-9._-]*$")

        fun androidDefaults(): CapabilityRegistry = CapabilityRegistry(listOf(
            CapabilityDescriptor(
                name = "display.publish",
                state = CapabilityState(available = true, authorized = false, active = false),
                transports = listOf("webrtc.video"),
                features = listOf("screen.capture"),
                permissions = listOf("android.media_projection")
            ),
            CapabilityDescriptor(
                name = "display.control",
                state = CapabilityState(available = true, authorized = false, active = false),
                transports = listOf("webrtc.datachannel"),
                features = listOf("pointer", "gesture", "text"),
                permissions = listOf("android.accessibility_service")
            ),
            CapabilityDescriptor(
                name = "media.list",
                state = CapabilityState(available = true, authorized = false, active = false),
                transports = listOf("http.range"),
                features = listOf("catalog"),
                permissions = listOf("qps.media.pairing")
            ),
            CapabilityDescriptor(
                name = "media.open",
                state = CapabilityState(available = true, authorized = false, active = false),
                transports = listOf("http.range"),
                features = listOf("metadata", "range"),
                permissions = listOf("qps.media.pairing")
            ),
            CapabilityDescriptor(
                name = "media.publish",
                state = CapabilityState(available = true, authorized = false, active = false),
                transports = listOf("http.range"),
                features = listOf("user_selected_media"),
                permissions = listOf("qps.media.pairing")
            )
        ))
    }
}

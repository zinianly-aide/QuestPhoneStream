package com.questphonestream.agent

internal data class MediaCapabilitySnapshot(
    val name: String,
    val available: Boolean,
    val authorized: Boolean,
    val active: Boolean
)

/**
 * Pure lifecycle model for media Spatial capabilities. Server lifetime controls
 * availability, a successful pairing/play capability controls authorization, and
 * in-flight HTTP requests control active state.
 */
internal class MediaCapabilityLifecycle(
    private val reporter: (MediaCapabilitySnapshot) -> Unit
) {
    private var serverRunning = false
    private var pairingAuthorized = false
    private val activeRequests = mutableMapOf(
        MEDIA_LIST to 0,
        MEDIA_OPEN to 0,
        MEDIA_PUBLISH to 0
    )

    @Synchronized
    fun startServer() {
        serverRunning = true
        pairingAuthorized = false
        activeRequests.keys.forEach { activeRequests[it] = 0 }
        publishAll()
    }

    @Synchronized
    fun stopServer() {
        serverRunning = false
        pairingAuthorized = false
        activeRequests.keys.forEach { activeRequests[it] = 0 }
        publishAll()
    }

    @Synchronized
    fun markPairingAuthorized() {
        if (pairingAuthorized) return
        pairingAuthorized = true
        publishAll()
    }

    @Synchronized
    fun resetPairingAuthorization() {
        pairingAuthorized = false
        activeRequests.keys.forEach { activeRequests[it] = 0 }
        publishAll()
    }

    @Synchronized
    fun beginRequest(name: String): Boolean {
        if (!serverRunning || !activeRequests.containsKey(name)) return false
        activeRequests[name] = (activeRequests[name] ?: 0) + 1
        publish(name)
        return true
    }

    @Synchronized
    fun endRequest(name: String) {
        if (!activeRequests.containsKey(name)) return
        activeRequests[name] = ((activeRequests[name] ?: 0) - 1).coerceAtLeast(0)
        publish(name)
    }

    @Synchronized
    fun snapshot(name: String): MediaCapabilitySnapshot = snapshotUnlocked(name)

    private fun snapshotUnlocked(name: String) = MediaCapabilitySnapshot(
        name = name,
        available = serverRunning,
        authorized = serverRunning && pairingAuthorized,
        active = serverRunning && pairingAuthorized && (activeRequests[name] ?: 0) > 0
    )

    private fun publish(name: String) = reporter(snapshotUnlocked(name))
    private fun publishAll() = activeRequests.keys.forEach(::publish)

    companion object {
        const val MEDIA_LIST = "media.list"
        const val MEDIA_OPEN = "media.open"
        const val MEDIA_PUBLISH = "media.publish"
    }
}

package com.questphonestream.agent

import org.json.JSONObject

/**
 * Process-wide owner of the Android signaling/control plane.
 *
 * NSD/media discovery and screen capture share one registered device identity so
 * Spatial capability discovery is available before MediaProjection starts and
 * starting/stopping a screen stream never replaces the signaling socket.
 */
interface StreamSignaling {
    fun sendSdp(type: String, session: StreamSession, sdp: String)
    fun sendIce(session: StreamSession, candidate: IceCandidateMessage)
    fun updateCapabilityState(name: String, authorized: Boolean? = null, active: Boolean? = null): Boolean
}

object DeviceControlPlane : StreamSignaling {
    enum class Owner { MEDIA, STREAM }

    private data class Endpoint(
        val url: String,
        val token: String,
        val deviceId: String
    )

    private val listeners = LinkedHashSet<SignalingClient.Listener>()
    private val ownerCounts = mutableMapOf<Owner, Int>()
    private var endpoint: Endpoint? = null
    private var client: SignalingClient? = null
    private var registry = CapabilityRegistry.androidDefaults()
    private var generation = 0
    private var activeSession: StreamSession? = null
    private var state: ConnectionState = ConnectionState.IDLE
    private var controlAuthorized = false
    private var controlTransportActive = false

    val currentState: ConnectionState
        @Synchronized get() = state

    val currentSession: StreamSession?
        @Synchronized get() = activeSession

    val isSpatialReady: Boolean
        @Synchronized get() = state == ConnectionState.CONNECTED

    @Synchronized
    fun acquire(owner: Owner) {
        ownerCounts[owner] = (ownerCounts[owner] ?: 0) + 1
    }

    fun release(owner: Owner) {
        var closing: SignalingClient? = null
        synchronized(this) {
            val remaining = (ownerCounts[owner] ?: 0) - 1
            if (remaining > 0) ownerCounts[owner] = remaining else ownerCounts.remove(owner)
            if (ownerCounts.values.sum() > 0) return
            generation += 1
            closing = client
            client = null
            activeSession = null
            state = ConnectionState.CLOSED
            controlTransportActive = false
            registry.updateState("display.publish", active = false)
            registry.updateState("display.control", active = false)
        }
        closing?.close()
    }

    /**
     * Reconfigure the single signaling socket when endpoint/identity changes.
     * Incomplete settings disable the control plane so NSD cannot claim Spatial readiness.
     */
    fun configure(url: String, token: String, deviceId: String, forceReconnect: Boolean = false): Boolean {
        val nextEndpoint = Endpoint(url.trim(), token, deviceId.trim())
        if (nextEndpoint.url.isBlank() || nextEndpoint.token.isBlank() || nextEndpoint.deviceId.isBlank()) {
            disableForInvalidConfiguration()
            return false
        }

        var previous: SignalingClient? = null
        var next: SignalingClient? = null
        var hadSession = false
        synchronized(this) {
            if (!forceReconnect && endpoint == nextEndpoint && client != null) return true
            generation += 1
            val nextGeneration = generation
            previous = client
            hadSession = activeSession != null
            activeSession = null
            endpoint = nextEndpoint
            state = ConnectionState.IDLE
            registry = CapabilityRegistry.androidDefaults().also {
                it.updateState(
                    "display.control",
                    authorized = controlAuthorized,
                    active = controlAuthorized && controlTransportActive
                )
            }
            next = SignalingClient(
                url = nextEndpoint.url,
                token = nextEndpoint.token,
                role = "android",
                deviceId = nextEndpoint.deviceId,
                listener = bridge(nextGeneration),
                capabilityRegistry = registry
            )
            client = next
        }

        // The old client callback belongs to an obsolete generation, so explicitly
        // invalidate any stream session before closing it.
        if (hadSession) notifyListeners { it.onSessionEnded() }
        previous?.close()
        next?.connect()
        return true
    }

    fun addListener(listener: SignalingClient.Listener, replay: Boolean = true) {
        val replayState: ConnectionState
        val replaySession: StreamSession?
        synchronized(this) {
            listeners += listener
            replayState = state
            replaySession = activeSession
        }
        if (replay) {
            listener.onStateChanged(replayState)
            replaySession?.let(listener::onSessionCreated)
        }
    }

    @Synchronized
    fun removeListener(listener: SignalingClient.Listener) {
        listeners -= listener
    }

    fun requestSession(sessionId: String, androidDeviceId: String, questDeviceId: String) {
        val current = synchronized(this) { client }
        current?.createSession(sessionId, androidDeviceId, questDeviceId)
    }

    override fun sendSdp(type: String, session: StreamSession, sdp: String) {
        val current = synchronized(this) { client }
        current?.sendSdp(type, session, sdp)
    }

    override fun sendIce(session: StreamSession, candidate: IceCandidateMessage) {
        val current = synchronized(this) { client }
        current?.sendIce(session, candidate)
    }

    override fun updateCapabilityState(name: String, authorized: Boolean?, active: Boolean?): Boolean {
        val currentRegistry = synchronized(this) { registry }
        return currentRegistry.updateState(name, authorized, active)
    }

    fun reportCapabilityState(name: String, available: Boolean, authorized: Boolean, active: Boolean): Boolean {
        val currentRegistry = synchronized(this) { registry }
        return currentRegistry.updateState(
            name,
            authorized = authorized,
            active = active,
            available = available
        )
    }

    fun setControlAuthorized(authorized: Boolean) {
        val currentRegistry: CapabilityRegistry
        val transportActive: Boolean
        synchronized(this) {
            controlAuthorized = authorized
            transportActive = controlTransportActive
            currentRegistry = registry
        }
        currentRegistry.updateState(
            "display.control",
            authorized = authorized,
            active = authorized && transportActive
        )
    }

    fun setControlTransportActive(active: Boolean) {
        val currentRegistry: CapabilityRegistry
        val authorized: Boolean
        synchronized(this) {
            controlTransportActive = active
            authorized = controlAuthorized
            currentRegistry = registry
        }
        currentRegistry.updateState("display.control", active = authorized && active)
    }

    private fun disableForInvalidConfiguration() {
        var closing: SignalingClient? = null
        var hadSession = false
        var shouldNotifyState = false
        synchronized(this) {
            if (client == null && state == ConnectionState.IDLE && endpoint == null) return
            generation += 1
            closing = client
            client = null
            endpoint = null
            hadSession = activeSession != null
            activeSession = null
            shouldNotifyState = state != ConnectionState.IDLE
            state = ConnectionState.IDLE
            controlTransportActive = false
            registry.updateState("display.publish", active = false)
            registry.updateState("display.control", active = false)
        }
        if (hadSession) notifyListeners { it.onSessionEnded() }
        closing?.close()
        if (shouldNotifyState) notifyListeners { it.onStateChanged(ConnectionState.IDLE) }
    }

    private fun bridge(expectedGeneration: Int) = object : SignalingClient.Listener {
        private fun isCurrent(): Boolean = synchronized(this@DeviceControlPlane) {
            expectedGeneration == generation
        }

        override fun onOpen() {
            if (isCurrent()) notifyListeners { it.onOpen() }
        }

        override fun onRegistered() {
            if (isCurrent()) notifyListeners { it.onRegistered() }
        }

        override fun onSessionCreated(session: StreamSession) {
            if (!isCurrent()) return
            synchronized(this@DeviceControlPlane) { activeSession = session }
            notifyListeners { it.onSessionCreated(session) }
        }

        override fun onRemoteDescription(session: StreamSession, type: String, sdp: String) {
            if (isCurrent()) notifyListeners { it.onRemoteDescription(session, type, sdp) }
        }

        override fun onIceCandidate(session: StreamSession, candidate: IceCandidateMessage) {
            if (isCurrent()) notifyListeners { it.onIceCandidate(session, candidate) }
        }

        override fun onSessionEnded() {
            if (!isCurrent()) return
            synchronized(this@DeviceControlPlane) { activeSession = null }
            notifyListeners { it.onSessionEnded() }
        }

        override fun onSpatialCapabilities(source: String, changed: Boolean, payload: JSONObject) {
            if (isCurrent()) notifyListeners { it.onSpatialCapabilities(source, changed, payload) }
        }

        override fun onStateChanged(state: ConnectionState) {
            if (!isCurrent()) return
            synchronized(this@DeviceControlPlane) { this@DeviceControlPlane.state = state }
            notifyListeners { it.onStateChanged(state) }
        }

        override fun onError(message: String) {
            if (isCurrent()) notifyListeners { it.onError(message) }
        }
    }

    private fun notifyListeners(block: (SignalingClient.Listener) -> Unit) {
        val snapshot = synchronized(this) { listeners.toList() }
        snapshot.forEach(block)
    }
}

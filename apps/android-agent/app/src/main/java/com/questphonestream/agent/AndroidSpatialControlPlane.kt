package com.questphonestream.agent

import android.util.Log

/**
 * Owns Android signaling independently from the MediaProjection service.
 * ScreenStreamService attaches a publisher only while a capture is active;
 * the signaling/session protocol itself remains unchanged.
 */
internal object AndroidSpatialControlPlane {
    private var config: StreamConfig? = null
    private var signaling: SignalingClient? = null
    private var streamer: WebRtcStreamer? = null
    private var pendingSession: StreamSession? = null

    @Synchronized
    fun start(requested: StreamConfig): SignalingClient {
        if (config != requested || signaling == null) {
            signaling?.close()
            config = requested
            pendingSession = null
            signaling = SignalingClient(
                url = requested.signalingUrl,
                token = requested.token,
                role = "android",
                deviceId = requested.deviceId,
                listener = listenerFor(requested)
            )
            signaling!!.connect()
        }
        return signaling!!
    }

    @Synchronized
    fun attach(publisher: WebRtcStreamer) {
        streamer = publisher
        pendingSession?.let {
            pendingSession = null
            publisher.startSession(it)
        }
    }

    @Synchronized
    fun detach(publisher: WebRtcStreamer) {
        if (streamer === publisher) streamer = null
    }

    private fun listenerFor(requested: StreamConfig) = object : SignalingClient.Listener {
        override fun onSessionCreated(session: StreamSession) {
            synchronized(AndroidSpatialControlPlane) {
                val current = streamer
                if (current == null) pendingSession = session else current.startSession(session)
            }
        }

        override fun onRemoteDescription(session: StreamSession, type: String, sdp: String) {
            synchronized(AndroidSpatialControlPlane) {
                streamer?.setRemoteDescription(session, type, sdp)
            }
        }

        override fun onIceCandidate(session: StreamSession, candidate: IceCandidateMessage) {
            synchronized(AndroidSpatialControlPlane) {
                streamer?.addIceCandidate(session, candidate)
            }
        }

        override fun onRegistered() {
            synchronized(AndroidSpatialControlPlane) {
                signaling?.createSession(requested.sessionId, requested.deviceId, requested.questDeviceId)
            }
        }

        override fun onSessionEnded() {
            synchronized(AndroidSpatialControlPlane) { streamer?.resetPeer() }
        }

        override fun onError(message: String) {
            Log.w(TAG, "Spatial signaling: $message")
        }
    }

    private const val TAG = "QuestPhoneStreamSpatial"
}

package com.questphonestream.agent

import android.content.Context
import android.content.Intent
import android.os.Handler
import android.os.Looper
import android.util.Log
import org.webrtc.*

class WebRtcStreamer(
    private val context: Context,
    private val config: StreamConfig,
    private val resultCode: Int,
    private val projectionData: Intent,
    private val signaling: StreamSignaling
) {
    private val main = Handler(Looper.getMainLooper())
    private val eglBase = EglBase.create()
    private val factory: PeerConnectionFactory
    private val videoCapturer: VideoCapturer
    private val videoSource: VideoSource
    private val videoTrack: VideoTrack
    private val audioSource: AudioSource
    private val audioTrack: AudioTrack
    private val surfaceTextureHelper: SurfaceTextureHelper
    private var peerConnection: PeerConnection? = null
    private var controlChannel: DataChannel? = null
    private var activeSession: StreamSession? = null
    private var generation = 0
    private var disposed = false
    private var resourcesDisposed = false
    private var remoteReady = false
    private val pendingIce = ArrayDeque<IceCandidateMessage>()
    private val peerDisposals = DeferredDisposalQueue(
        delayMillis = 250L,
        schedule = { delay, action -> main.postDelayed({ action() }, delay) },
        dispose = { peer: PeerConnection ->
            runCatching { peer.close() }
            runCatching { peer.dispose() }
        },
        onDrained = ::disposeCaptureResources
    )

    // Session switches are coalesced through a short debounce: during a Quest
    // reconnect storm the server may emit several session_created messages in a
    // row. Tearing down and recreating the PeerConnection for each one aborts the
    // native signaling thread (libjingle_peerconnection_so). We instead apply at
    // most one switch per debounce window and defer the old peer's destruction.
    private var restartDebounce = false
    private var pendingSession: StreamSession? = null

    init {
        PeerConnectionFactory.initialize(PeerConnectionFactory.InitializationOptions.builder(context)
            .createInitializationOptions())
        factory = PeerConnectionFactory.builder()
            .setVideoEncoderFactory(DefaultVideoEncoderFactory(eglBase.eglBaseContext, true, true))
            .setVideoDecoderFactory(DefaultVideoDecoderFactory(eglBase.eglBaseContext))
            .createPeerConnectionFactory()
        // Capture belongs to the user-authorized projection, not an individual peer negotiation.
        videoCapturer = ScreenCapturerAndroid(projectionData, object : android.media.projection.MediaProjection.Callback() {
            override fun onStop() { main.post { dispose() } }
        })
        videoSource = factory.createVideoSource(true)
        surfaceTextureHelper = SurfaceTextureHelper.create("ScreenCaptureThread", eglBase.eglBaseContext)
        videoCapturer.initialize(surfaceTextureHelper, context, videoSource.capturerObserver)
        videoCapturer.startCapture(config.width, config.height, config.fps)
        // Publish the encoding resolution so the accessibility service can scale
        // incoming touch coordinates from video-space to the real screen resolution.
        VideoResolutionHolder.width = config.width
        VideoResolutionHolder.height = config.height
        Log.i(TAG, "Video capture started at ${config.width}x${config.height}@${config.fps}fps")
        videoTrack = factory.createVideoTrack("screen-video", videoSource)
        audioSource = factory.createAudioSource(MediaConstraints())
        audioTrack = factory.createAudioTrack("silent-audio", audioSource)
        audioTrack.setEnabled(false)
    }

    fun startSession(session: StreamSession) {
        if (disposed || activeSession == session) return
        pendingSession = session
        if (restartDebounce) return
        restartDebounce = true
        main.postDelayed({ applyPendingSession() }, 200)
    }

    private fun applyPendingSession() {
        restartDebounce = false
        val session = pendingSession ?: return
        pendingSession = null
        if (disposed || session == activeSession) return
        teardownPeer()
        doStartSession(session)
    }

    private fun doStartSession(session: StreamSession) {
        activeSession = session
        val epoch = generation
        val iceServers = listOf(PeerConnection.IceServer.builder("stun:stun.l.google.com:19302").createIceServer())
        val peer = factory.createPeerConnection(PeerConnection.RTCConfiguration(iceServers).apply {
            sdpSemantics = PeerConnection.SdpSemantics.UNIFIED_PLAN
            continualGatheringPolicy = PeerConnection.ContinualGatheringPolicy.GATHER_CONTINUALLY
        }, object : PeerConnection.Observer {
            override fun onIceCandidate(candidate: IceCandidate) {
                main.post {
                    if (isCurrent(epoch)) signaling.sendIce(session,
                        IceCandidateMessage(candidate.sdp, candidate.sdpMid, candidate.sdpMLineIndex))
                }
            }
            override fun onDataChannel(channel: DataChannel) { main.post { runCatching { channel.close() } } }
            override fun onIceCandidatesRemoved(candidates: Array<out IceCandidate>) = Unit
            override fun onSignalingChange(state: PeerConnection.SignalingState) = Unit
            override fun onIceConnectionChange(state: PeerConnection.IceConnectionState) = Unit
            override fun onIceConnectionReceivingChange(receiving: Boolean) = Unit
            override fun onIceGatheringChange(state: PeerConnection.IceGatheringState) = Unit
            override fun onAddStream(stream: MediaStream) = Unit
            override fun onRemoveStream(stream: MediaStream) = Unit
            override fun onRenegotiationNeeded() = Unit
            override fun onAddTrack(receiver: RtpReceiver, streams: Array<out MediaStream>) = Unit
        }) ?: error("Failed to create PeerConnection")
        peerConnection = peer
        val channel = peer.createDataChannel("control", DataChannel.Init())
        controlChannel = channel
        DeviceControlPlane.setControlTransportActive(false)
        channel.registerObserver(object : DataChannel.Observer {
            override fun onBufferedAmountChange(previousAmount: Long) = Unit
            override fun onStateChange() {
                val state = channel.state()
                Log.i(TAG, "Control channel state: $state")
                main.post {
                    if (!isCurrent(epoch) || controlChannel !== channel) return@post
                    DeviceControlPlane.setControlTransportActive(state == DataChannel.State.OPEN)
                }
            }
            override fun onMessage(buffer: DataChannel.Buffer) {
                Log.i(TAG, "Control message received: binary=${buffer.binary} size=${buffer.data.remaining()}")
                if (buffer.data.remaining() > 65536 || buffer.data.remaining() == 0) return
                val bytes = ByteArray(buffer.data.remaining())
                buffer.data.get(bytes)
                main.post {
                    if (isCurrent(epoch)) {
                        ControlCommandDispatcher.dispatch(String(bytes, Charsets.UTF_8))
                    } else {
                        Log.w(TAG, "Control message dropped: stale epoch=$epoch current=$generation")
                    }
                }
            }
        })
        peer.addTrack(videoTrack, listOf("screen"))
        peer.addTrack(audioTrack, listOf("screen"))
        createOffer(peer, session, epoch)
    }

    private fun createOffer(peer: PeerConnection, session: StreamSession, epoch: Int) {
        val constraints = MediaConstraints().apply {
            mandatory.add(MediaConstraints.KeyValuePair("OfferToReceiveAudio", "false"))
            mandatory.add(MediaConstraints.KeyValuePair("OfferToReceiveVideo", "false"))
        }
        peer.createOffer(object : SimpleSdpObserver() {
            override fun onCreateSuccess(description: SessionDescription) {
                main.post {
                    if (!isCurrent(epoch)) return@post
                    val preferred = SessionDescription(description.type, SdpUtils.preferH264(description.description))
                    peer.setLocalDescription(object : SimpleSdpObserver() {
                        override fun onSetSuccess() {
                            main.post {
                                if (isCurrent(epoch)) signaling.sendSdp("offer", session, preferred.description)
                            }
                        }
                    }, preferred)
                }
            }
        }, constraints)
    }

    fun setRemoteDescription(session: StreamSession, type: String, sdp: String) {
        if (session != activeSession || disposed || type != "answer" || remoteReady) return
        val peer = peerConnection ?: return
        val epoch = generation
        peer.setRemoteDescription(object : SimpleSdpObserver() {
            override fun onSetSuccess() {
                main.post {
                    if (!isCurrent(epoch)) return@post
                    remoteReady = true
                    while (pendingIce.isNotEmpty()) addIceCandidate(session, pendingIce.removeFirst())
                }
            }
        }, SessionDescription(SessionDescription.Type.ANSWER, sdp))
    }

    fun addIceCandidate(session: StreamSession, candidate: IceCandidateMessage) {
        if (session != activeSession || disposed) return
        if (!remoteReady) {
            if (pendingIce.size >= 256) { teardownPeer(); return }
            pendingIce.addLast(candidate)
            return
        }
        peerConnection?.addIceCandidate(IceCandidate(candidate.sdpMid, candidate.sdpMLineIndex, candidate.candidate))
    }

    private fun isCurrent(epoch: Int): Boolean = !disposed && epoch == generation && peerConnection != null

    fun resetPeer() {
        // Public session-end hook (e.g. peer_unavailable). Also crash-safe:
        // detaches immediately, destroys the native peer after its callbacks drain.
        if (!disposed) teardownPeer()
    }

    private fun teardownPeer() {
        peerDisposals.defer(detachCurrentPeer())
    }

    private fun detachCurrentPeer(): PeerConnection? {
        ++generation
        activeSession = null
        remoteReady = false
        pendingIce.clear()
        DeviceControlPlane.setControlTransportActive(false)
        controlChannel?.unregisterObserver()
        val oldChannel = controlChannel
        controlChannel = null
        CapabilityRuntime.setDisplayControl(authorized = false, active = false)
        val oldPeer = peerConnection
        peerConnection = null
        // DataChannel is owned by PeerConnection. Closing it is enough here;
        // disposing it separately can race the peer/factory native teardown.
        runCatching { oldChannel?.close() }
        return oldPeer
    }

    fun dispose() {
        if (disposed) return
        disposed = true
        restartDebounce = true
        pendingSession = null
        DeviceControlPlane.setControlTransportActive(false)
        runCatching { videoCapturer.stopCapture() }
        peerDisposals.defer(detachCurrentPeer())
        peerDisposals.finishWhenDrained()
    }

    private fun disposeCaptureResources() {
        if (resourcesDisposed) return
        resourcesDisposed = true
        runCatching { videoCapturer.dispose() }
        runCatching { videoTrack.dispose() }
        runCatching { audioTrack.dispose() }
        runCatching { videoSource.dispose() }
        runCatching { audioSource.dispose() }
        runCatching { surfaceTextureHelper.dispose() }
        runCatching { factory.dispose() }
        runCatching { eglBase.release() }
    }
}

open class SimpleSdpObserver : SdpObserver {
    override fun onCreateSuccess(description: SessionDescription) = Unit
    override fun onSetSuccess() = Unit
    override fun onCreateFailure(error: String) { Log.e(TAG, "SDP create failed") }
    override fun onSetFailure(error: String) { Log.e(TAG, "SDP set failed") }
}

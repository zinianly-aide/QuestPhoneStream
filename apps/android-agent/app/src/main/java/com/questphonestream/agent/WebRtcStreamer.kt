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
    private val signaling: SignalingClient
) {
    private val main = Handler(Looper.getMainLooper())
    private lateinit var eglBase: EglBase
    private lateinit var factory: PeerConnectionFactory
    private lateinit var videoCapturer: VideoCapturer
    private lateinit var videoSource: VideoSource
    private lateinit var videoTrack: VideoTrack
    private lateinit var audioSource: AudioSource
    private lateinit var audioTrack: AudioTrack
    private lateinit var surfaceTextureHelper: SurfaceTextureHelper
    private var peerConnection: PeerConnection? = null
    private var controlChannel: DataChannel? = null
    private var activeSession: StreamSession? = null
    private var generation = 0
    private var disposed = false
    private var remoteReady = false
    private val pendingIce = ArrayDeque<IceCandidateMessage>()

    init {
        try {
            PeerConnectionFactory.initialize(PeerConnectionFactory.InitializationOptions.builder(context)
                .createInitializationOptions())
            eglBase = EglBase.create()
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
            videoTrack = factory.createVideoTrack("screen-video", videoSource)
            audioSource = factory.createAudioSource(MediaConstraints())
            audioTrack = factory.createAudioTrack("silent-audio", audioSource)
            audioTrack.setEnabled(false)
        } catch (error: Throwable) {
            disposeInitializedResources()
            throw error
        }
    }

    fun startSession(session: StreamSession) {
        if (disposed || activeSession == session) return
        resetPeer()
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
            override fun onDataChannel(channel: DataChannel) { main.post { channel.close(); channel.dispose() } }
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
        controlChannel = peer.createDataChannel("control", DataChannel.Init()).apply {
            registerObserver(object : DataChannel.Observer {
                override fun onBufferedAmountChange(previousAmount: Long) = Unit
                override fun onStateChange() = Unit
                override fun onMessage(buffer: DataChannel.Buffer) {
                    if (buffer.binary || buffer.data.remaining() > 65536) return
                    val bytes = ByteArray(buffer.data.remaining())
                    buffer.data.get(bytes)
                    main.post {
                        if (isCurrent(epoch)) ControlCommandDispatcher.dispatch(String(bytes, Charsets.UTF_8))
                    }
                }
            })
        }
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
            if (pendingIce.size >= 256) { resetPeer(); return }
            pendingIce.addLast(candidate)
            return
        }
        peerConnection?.addIceCandidate(IceCandidate(candidate.sdpMid, candidate.sdpMLineIndex, candidate.candidate))
    }

    private fun isCurrent(epoch: Int): Boolean = !disposed && epoch == generation && peerConnection != null

    fun resetPeer() {
        ++generation
        activeSession = null
        remoteReady = false
        pendingIce.clear()
        controlChannel?.unregisterObserver()
        controlChannel?.close()
        controlChannel?.dispose()
        controlChannel = null
        peerConnection?.close()
        peerConnection?.dispose()
        peerConnection = null
    }

    fun dispose() {
        if (disposed) return
        disposed = true
        disposeInitializedResources()
    }

    private fun disposeInitializedResources() {
        resetPeer()
        if (::videoCapturer.isInitialized) runCatching { videoCapturer.stopCapture() }
        if (::videoCapturer.isInitialized) runCatching { videoCapturer.dispose() }
        if (::videoTrack.isInitialized) runCatching { videoTrack.dispose() }
        if (::audioTrack.isInitialized) runCatching { audioTrack.dispose() }
        if (::videoSource.isInitialized) runCatching { videoSource.dispose() }
        if (::audioSource.isInitialized) runCatching { audioSource.dispose() }
        if (::surfaceTextureHelper.isInitialized) runCatching { surfaceTextureHelper.dispose() }
        if (::factory.isInitialized) runCatching { factory.dispose() }
        if (::eglBase.isInitialized) runCatching { eglBase.release() }
    }
}

open class SimpleSdpObserver : SdpObserver {
    override fun onCreateSuccess(description: SessionDescription) = Unit
    override fun onSetSuccess() = Unit
    override fun onCreateFailure(error: String) { Log.e(TAG, "SDP create failed") }
    override fun onSetFailure(error: String) { Log.e(TAG, "SDP set failed") }
}

package com.questphonestream.agent

import android.content.Context
import android.content.Intent
import android.util.Log
import org.webrtc.AudioSource
import org.webrtc.AudioTrack
import org.webrtc.DataChannel
import org.webrtc.DefaultVideoDecoderFactory
import org.webrtc.DefaultVideoEncoderFactory
import org.webrtc.EglBase
import org.webrtc.IceCandidate
import org.webrtc.MediaConstraints
import org.webrtc.MediaStream
import org.webrtc.PeerConnection
import org.webrtc.PeerConnectionFactory
import org.webrtc.RtpReceiver
import org.webrtc.ScreenCapturerAndroid
import org.webrtc.SdpObserver
import org.webrtc.SessionDescription
import org.webrtc.SurfaceTextureHelper
import org.webrtc.VideoCapturer
import org.webrtc.VideoSource
import org.webrtc.VideoTrack

class WebRtcStreamer(
    private val context: Context,
    private val config: StreamConfig,
    private val resultCode: Int,
    private val projectionData: Intent,
    private val signaling: SignalingClient
) {
    private val eglBase = EglBase.create()
    private val factory: PeerConnectionFactory
    private val peerConnection: PeerConnection
    private val videoCapturer: VideoCapturer
    private val videoSource: VideoSource
    private val controlChannel: DataChannel

    init {
        PeerConnectionFactory.initialize(
            PeerConnectionFactory.InitializationOptions.builder(context)
                .setEnableInternalTracer(true)
                .createInitializationOptions()
        )
        val encoderFactory = DefaultVideoEncoderFactory(eglBase.eglBaseContext, true, true)
        val decoderFactory = DefaultVideoDecoderFactory(eglBase.eglBaseContext)
        factory = PeerConnectionFactory.builder()
            .setVideoEncoderFactory(encoderFactory)
            .setVideoDecoderFactory(decoderFactory)
            .createPeerConnectionFactory()

        val iceServers = listOf(
            PeerConnection.IceServer.builder("stun:stun.l.google.com:19302").createIceServer()
        )
        peerConnection = factory.createPeerConnection(
            PeerConnection.RTCConfiguration(iceServers).apply {
                sdpSemantics = PeerConnection.SdpSemantics.UNIFIED_PLAN
                continualGatheringPolicy = PeerConnection.ContinualGatheringPolicy.GATHER_CONTINUALLY
            },
            object : PeerConnection.Observer {
                override fun onIceCandidate(candidate: IceCandidate) {
                    signaling.sendIce(
                        config.sessionId,
                        config.deviceId,
                        config.questDeviceId,
                        IceCandidateMessage(candidate.sdp, candidate.sdpMid, candidate.sdpMLineIndex)
                    )
                }

                override fun onDataChannel(channel: DataChannel) = Unit
                override fun onIceCandidatesRemoved(candidates: Array<out IceCandidate>) = Unit
                override fun onSignalingChange(state: PeerConnection.SignalingState) = Log.i(TAG, "Signaling $state")
                override fun onIceConnectionChange(state: PeerConnection.IceConnectionState) = Log.i(TAG, "ICE $state")
                override fun onIceConnectionReceivingChange(receiving: Boolean) = Unit
                override fun onIceGatheringChange(state: PeerConnection.IceGatheringState) = Unit
                override fun onAddStream(stream: MediaStream) = Unit
                override fun onRemoveStream(stream: MediaStream) = Unit
                override fun onRenegotiationNeeded() = Unit
                override fun onAddTrack(receiver: RtpReceiver, streams: Array<out MediaStream>) = Unit
            }
        ) ?: error("Failed to create PeerConnection")

        controlChannel = peerConnection.createDataChannel("control", DataChannel.Init()).apply {
            registerObserver(object : DataChannel.Observer {
                override fun onBufferedAmountChange(previousAmount: Long) = Unit
                override fun onStateChange() = Log.i(TAG, "Control DataChannel ${state()}")
                override fun onMessage(buffer: DataChannel.Buffer) {
                    val bytes = ByteArray(buffer.data.remaining())
                    buffer.data.get(bytes)
                    ControlCommandDispatcher.dispatch(String(bytes, Charsets.UTF_8))
                }
            })
        }

        videoCapturer = ScreenCapturerAndroid(projectionData, object : android.media.projection.MediaProjection.Callback() {
            override fun onStop() {
                Log.i(TAG, "MediaProjection stopped")
                dispose()
            }
        })
        videoSource = factory.createVideoSource(false)
        val surfaceTextureHelper = SurfaceTextureHelper.create("ScreenCaptureThread", eglBase.eglBaseContext)
        videoCapturer.initialize(surfaceTextureHelper, context, videoSource.capturerObserver)
        videoCapturer.startCapture(config.width, config.height, config.fps)

        val videoTrack: VideoTrack = factory.createVideoTrack("screen-video", videoSource)
        peerConnection.addTrack(videoTrack, listOf("screen"))

        val audioSource: AudioSource = factory.createAudioSource(MediaConstraints())
        val audioTrack: AudioTrack = factory.createAudioTrack("silent-audio", audioSource)
        audioTrack.setEnabled(false)
        peerConnection.addTrack(audioTrack, listOf("screen"))
    }

    fun createOffer() {
        val constraints = MediaConstraints().apply {
            mandatory.add(MediaConstraints.KeyValuePair("OfferToReceiveAudio", "false"))
            mandatory.add(MediaConstraints.KeyValuePair("OfferToReceiveVideo", "false"))
        }
        peerConnection.createOffer(object : SimpleSdpObserver() {
            override fun onCreateSuccess(description: SessionDescription) {
                val preferred = SessionDescription(description.type, SdpUtils.preferH264(description.description))
                peerConnection.setLocalDescription(SimpleSdpObserver(), preferred)
                signaling.sendSdp("offer", config.sessionId, config.deviceId, config.questDeviceId, preferred.description)
            }
        }, constraints)
    }

    fun setRemoteDescription(type: String, sdp: String) {
        val sdpType = SessionDescription.Type.fromCanonicalForm(type)
        peerConnection.setRemoteDescription(SimpleSdpObserver(), SessionDescription(sdpType, sdp))
    }

    fun addIceCandidate(candidate: IceCandidateMessage) {
        peerConnection.addIceCandidate(IceCandidate(candidate.sdpMid, candidate.sdpMLineIndex, candidate.candidate))
    }

    fun dispose() {
        try {
            videoCapturer.stopCapture()
        } catch (_: Exception) {
        }
        controlChannel.dispose()
        videoSource.dispose()
        peerConnection.dispose()
        factory.dispose()
        eglBase.release()
    }
}

open class SimpleSdpObserver : SdpObserver {
    open override fun onCreateSuccess(description: SessionDescription) = Unit
    open override fun onSetSuccess() = Unit
    open override fun onCreateFailure(error: String) {
        Log.e(TAG, "SDP create failure: $error")
    }
    open override fun onSetFailure(error: String) {
        Log.e(TAG, "SDP set failure: $error")
    }
}

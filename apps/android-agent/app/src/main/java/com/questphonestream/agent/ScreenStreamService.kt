package com.questphonestream.agent

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.Service
import android.content.Context
import android.content.Intent
import android.os.Build
import android.os.IBinder
import android.util.Log
import androidx.core.app.NotificationCompat
import androidx.core.content.ContextCompat

class ScreenStreamService : Service() {
    private var streamer: WebRtcStreamer? = null
    private var activeConfig: StreamConfig? = null
    private var listenerAttached = false

    private val controlPlaneListener = object : SignalingClient.Listener {
        override fun onSessionCreated(session: StreamSession) {
            val config = activeConfig ?: return
            if (session.androidDeviceId == config.deviceId && session.questDeviceId == config.questDeviceId) {
                streamer?.startSession(session)
                DeviceControlPlane.updateCapabilityState("display.publish", authorized = true, active = true)
            }
        }

        override fun onRemoteDescription(session: StreamSession, type: String, sdp: String) {
            streamer?.setRemoteDescription(session, type, sdp)
        }

        override fun onIceCandidate(session: StreamSession, candidate: IceCandidateMessage) {
            streamer?.addIceCandidate(session, candidate)
        }

        override fun onRegistered() {
            val config = activeConfig ?: return
            DeviceControlPlane.updateCapabilityState("display.publish", authorized = true, active = false)
            DeviceControlPlane.requestSession(config.sessionId, config.deviceId, config.questDeviceId)
        }

        override fun onSessionEnded() {
            DeviceControlPlane.updateCapabilityState("display.publish", authorized = true, active = false)
            streamer?.resetPeer()
        }
    }

    override fun onCreate() {
        super.onCreate()
        DeviceControlPlane.acquire(DeviceControlPlane.Owner.STREAM)
        createNotificationChannel()
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        if (intent == null) return START_NOT_STICKY
        startForeground(NOTIFICATION_ID, notification("Starting stream"))

        val resultCode = intent.getIntExtra("resultCode", 0)
        val projectionData = intent.getParcelableExtra<Intent>("projectionData")
            ?: return START_NOT_STICKY
        val config = StreamConfig.from(intent)

        streamer?.dispose()
        activeConfig = config
        DeviceControlPlane.configure(config.signalingUrl, config.token, config.deviceId)
        DeviceControlPlane.updateCapabilityState("display.publish", authorized = true, active = false)

        streamer = WebRtcStreamer(
            context = applicationContext,
            config = config,
            resultCode = resultCode,
            projectionData = projectionData,
            signaling = DeviceControlPlane
        )

        if (!listenerAttached) {
            DeviceControlPlane.addListener(controlPlaneListener, replay = true)
            listenerAttached = true
        }
        DeviceControlPlane.requestSession(config.sessionId, config.deviceId, config.questDeviceId)
        Log.i(TAG, "Screen stream service attached to device control plane for ${config.sessionId}")
        return START_STICKY
    }

    override fun onDestroy() {
        DeviceControlPlane.updateCapabilityState("display.publish", authorized = false, active = false)
        DeviceControlPlane.setControlTransportActive(false)
        if (listenerAttached) {
            DeviceControlPlane.removeListener(controlPlaneListener)
            listenerAttached = false
        }
        streamer?.dispose()
        streamer = null
        activeConfig = null
        DeviceControlPlane.release(DeviceControlPlane.Owner.STREAM)
        super.onDestroy()
    }

    override fun onBind(intent: Intent?): IBinder? = null

    private fun createNotificationChannel() {
        if (Build.VERSION.SDK_INT >= 26) {
            val manager = getSystemService(NotificationManager::class.java)
            manager.createNotificationChannel(
                NotificationChannel(CHANNEL_ID, "QuestPhoneStream", NotificationManager.IMPORTANCE_LOW)
            )
        }
    }

    private fun notification(text: String): Notification =
        NotificationCompat.Builder(this, CHANNEL_ID)
            .setSmallIcon(android.R.drawable.presence_video_online)
            .setContentTitle("QuestPhoneStream")
            .setContentText(text)
            .setOngoing(true)
            .build()

    companion object {
        private const val CHANNEL_ID = "screen_stream"
        private const val NOTIFICATION_ID = 41

        fun start(context: Context, resultCode: Int, data: Intent, config: StreamConfig) {
            // MediaProjection may be granted after the user has edited the fields again.
            // Only the explicitly applied endpoint identity is allowed to reach signaling.
            val effectiveConfig = AppliedConfigStore.merge(config)
            val intent = Intent(context, ScreenStreamService::class.java).apply {
                putExtra("resultCode", resultCode)
                putExtra("projectionData", data)
                effectiveConfig.writeTo(this)
            }
            ContextCompat.startForegroundService(context, intent)
        }
    }
}

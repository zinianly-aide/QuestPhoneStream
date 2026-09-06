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
    private var signalingClient: SignalingClient? = null

    override fun onCreate() {
        super.onCreate()
        createNotificationChannel()
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        if (intent == null) return START_NOT_STICKY
        startForeground(NOTIFICATION_ID, notification("Starting stream"))

        val resultCode = intent.getIntExtra("resultCode", 0)
        val projectionData = intent.getParcelableExtra<Intent>("projectionData")
            ?: return START_NOT_STICKY
        val config = StreamConfig.from(intent)

        signalingClient?.close()
        streamer?.dispose()

        signalingClient = SignalingClient(
            url = config.signalingUrl,
            token = config.token,
            role = "android",
            deviceId = config.deviceId,
            listener = object : SignalingClient.Listener {
                override fun onSessionCreated(session: StreamSession) {
                    if (session.androidDeviceId == config.deviceId && session.questDeviceId == config.questDeviceId) {
                        streamer?.startSession(session)
                        signalingClient?.updateCapabilityState("display.publish", authorized = true, active = true)
                    }
                }

                override fun onRemoteDescription(session: StreamSession, type: String, sdp: String) {
                    streamer?.setRemoteDescription(session, type, sdp)
                }

                override fun onIceCandidate(session: StreamSession, candidate: IceCandidateMessage) {
                    streamer?.addIceCandidate(session, candidate)
                }

                override fun onRegistered() {
                    signalingClient?.updateCapabilityState("display.publish", authorized = true, active = false)
                    signalingClient?.createSession(config.sessionId, config.deviceId, config.questDeviceId)
                }

                override fun onSessionEnded() {
                    signalingClient?.updateCapabilityState("display.publish", authorized = true, active = false)
                    streamer?.resetPeer()
                }
            }
        )

        streamer = WebRtcStreamer(
            context = applicationContext,
            config = config,
            resultCode = resultCode,
            projectionData = projectionData,
            signaling = signalingClient!!
        )
        signalingClient?.connect()
        Log.i(TAG, "Screen stream service started for ${config.sessionId}")
        return START_STICKY
    }

    override fun onDestroy() {
        signalingClient?.updateCapabilityState("display.publish", authorized = false, active = false)
        streamer?.dispose()
        signalingClient?.close()
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
            val intent = Intent(context, ScreenStreamService::class.java).apply {
                putExtra("resultCode", resultCode)
                putExtra("projectionData", data)
                config.writeTo(this)
            }
            ContextCompat.startForegroundService(context, intent)
        }
    }
}

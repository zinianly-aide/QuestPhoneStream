package com.questphonestream.agent

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.Service
import android.content.Context
import android.content.Intent
import android.content.pm.ServiceInfo
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

        return try {
            startProjectionForeground()

            val resultCode = intent.getIntExtra("resultCode", 0)
            val projectionData = intent.getParcelableExtra<Intent>("projectionData")
            if (projectionData == null) {
                Log.e(TAG, "Missing media projection permission data")
                stopStreaming(startId)
                START_NOT_STICKY
            } else {
                val config = StreamConfig.from(intent)

                disposeStreamingObjects()

                signalingClient = SignalingClient(
                    url = config.signalingUrl,
                    token = config.token,
                    role = "android",
                    deviceId = config.deviceId,
                    listener = object : SignalingClient.Listener {
                        override fun onSessionCreated(session: StreamSession) {
                            if (session.androidDeviceId == config.deviceId && session.questDeviceId == config.questDeviceId) {
                                streamer?.startSession(session)
                            }
                        }

                        override fun onRemoteDescription(session: StreamSession, type: String, sdp: String) {
                            streamer?.setRemoteDescription(session, type, sdp)
                        }

                        override fun onIceCandidate(session: StreamSession, candidate: IceCandidateMessage) {
                            streamer?.addIceCandidate(session, candidate)
                        }

                        override fun onRegistered() {
                            signalingClient?.createSession(config.sessionId, config.deviceId, config.questDeviceId)
                        }

                        override fun onSessionEnded() {
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
                START_STICKY
            }
        } catch (error: Throwable) {
            Log.e(TAG, "Failed to start screen stream", error)
            stopStreaming(startId)
            START_NOT_STICKY
        }
    }

    override fun onDestroy() {
        disposeStreamingObjects()
        super.onDestroy()
    }

    override fun onBind(intent: Intent?): IBinder? = null

    private fun startProjectionForeground() {
        val foregroundNotification = notification("Starting stream")
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            startForeground(
                NOTIFICATION_ID,
                foregroundNotification,
                ServiceInfo.FOREGROUND_SERVICE_TYPE_MEDIA_PROJECTION
            )
        } else {
            startForeground(NOTIFICATION_ID, foregroundNotification)
        }
    }

    private fun stopStreaming(startId: Int) {
        disposeStreamingObjects()
        runCatching { stopForeground(STOP_FOREGROUND_REMOVE) }
        stopSelf(startId)
    }

    private fun disposeStreamingObjects() {
        runCatching { streamer?.dispose() }
        streamer = null
        runCatching { signalingClient?.close() }
        signalingClient = null
    }

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

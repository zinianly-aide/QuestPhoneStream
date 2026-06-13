package com.questphonestream.agent

import android.content.Intent

data class StreamConfig(
    val signalingUrl: String,
    val token: String,
    val deviceId: String,
    val questDeviceId: String,
    val sessionId: String,
    val width: Int = 1280,
    val height: Int = 720,
    val fps: Int = 30
) {
    fun writeTo(intent: Intent) {
        intent.putExtra("signalingUrl", signalingUrl)
        intent.putExtra("token", token)
        intent.putExtra("deviceId", deviceId)
        intent.putExtra("questDeviceId", questDeviceId)
        intent.putExtra("sessionId", sessionId)
        intent.putExtra("width", width)
        intent.putExtra("height", height)
        intent.putExtra("fps", fps)
    }

    companion object {
        fun from(intent: Intent): StreamConfig = StreamConfig(
            signalingUrl = intent.getStringExtra("signalingUrl") ?: error("missing signalingUrl"),
            token = intent.getStringExtra("token") ?: error("missing token"),
            deviceId = intent.getStringExtra("deviceId") ?: error("missing deviceId"),
            questDeviceId = intent.getStringExtra("questDeviceId") ?: error("missing questDeviceId"),
            sessionId = intent.getStringExtra("sessionId") ?: error("missing sessionId"),
            width = intent.getIntExtra("width", 1280),
            height = intent.getIntExtra("height", 720),
            fps = intent.getIntExtra("fps", 30)
        )
    }
}


package com.questphonestream.agent

import android.util.Log
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.Response
import okhttp3.WebSocket
import okhttp3.WebSocketListener
import org.json.JSONObject
import java.util.Timer
import java.util.TimerTask
import java.util.concurrent.TimeUnit

data class IceCandidateMessage(
    val candidate: String,
    val sdpMid: String?,
    val sdpMLineIndex: Int
)

class SignalingClient(
    private val url: String,
    private val token: String,
    private val role: String,
    private val deviceId: String,
    private val listener: Listener
) {
    interface Listener {
        fun onOpen() {}
        fun onSessionCreated(sessionId: String, androidDeviceId: String, questDeviceId: String) {}
        fun onRemoteDescription(type: String, sdp: String) {}
        fun onIceCandidate(candidate: IceCandidateMessage) {}
        fun onStateChanged(state: ConnectionState) {}
        fun onError(message: String) {}
    }

    private val client = OkHttpClient.Builder()
        .pingInterval(15, TimeUnit.SECONDS)
        .build()
    private var socket: WebSocket? = null
    private var heartbeat: Timer? = null

    val currentState: ConnectionState
        get() = _state

    private var _state: ConnectionState = ConnectionState.IDLE
        set(value) {
            field = value
            listener.onStateChanged(value)
        }

    fun connect() {
        _state = ConnectionState.CONNECTING
        val request = Request.Builder().url(url).build()
        socket = client.newWebSocket(request, object : WebSocketListener() {
            override fun onOpen(webSocket: WebSocket, response: Response) {
                Log.i(TAG, "Signaling connected to $url")
                _state = ConnectionState.CONNECTED
                send(
                    JSONObject()
                        .put("type", "register")
                        .put("token", token)
                        .put("role", role)
                        .put("deviceId", deviceId)
                )
                startHeartbeat()
                listener.onOpen()
            }

            override fun onMessage(webSocket: WebSocket, text: String) {
                handleMessage(JSONObject(text))
            }

            override fun onFailure(webSocket: WebSocket, t: Throwable, response: Response?) {
                val msg = "Signaling failure: ${t.message}"
                Log.e(TAG, msg, t)
                _state = ConnectionState.FAILED
                listener.onError(msg)
            }

            override fun onClosed(webSocket: WebSocket, code: Int, reason: String) {
                Log.i(TAG, "Signaling closed: $code $reason")
                _state = ConnectionState.CLOSED
            }
        })
    }

    fun createSession(sessionId: String, androidDeviceId: String, questDeviceId: String) {
        send(
            JSONObject()
                .put("type", "create_session")
                .put("token", token)
                .put("sessionId", sessionId)
                .put("androidDeviceId", androidDeviceId)
                .put("questDeviceId", questDeviceId)
        )
    }

    fun sendSdp(type: String, sessionId: String, from: String, to: String, sdp: String) {
        send(
            JSONObject()
                .put("type", type)
                .put("token", token)
                .put("sessionId", sessionId)
                .put("from", from)
                .put("to", to)
                .put("sdp", sdp)
        )
    }

    fun sendIce(sessionId: String, from: String, to: String, candidate: IceCandidateMessage) {
        send(
            JSONObject()
                .put("type", "ice")
                .put("token", token)
                .put("sessionId", sessionId)
                .put("from", from)
                .put("to", to)
                .put(
                    "candidate",
                    JSONObject()
                        .put("candidate", candidate.candidate)
                        .put("sdpMid", candidate.sdpMid)
                        .put("sdpMLineIndex", candidate.sdpMLineIndex)
                )
        )
    }

    fun close() {
        heartbeat?.cancel()
        socket?.close(1000, "closed")
        _state = ConnectionState.CLOSED
        client.dispatcher.executorService.shutdown()
    }

    private fun handleMessage(message: JSONObject) {
        when (message.optString("type")) {
            "registered" -> Log.i(TAG, "Registered as $deviceId")
            "session_created" -> listener.onSessionCreated(
                message.getString("sessionId"),
                message.getString("androidDeviceId"),
                message.getString("questDeviceId")
            )
            "answer", "offer" -> listener.onRemoteDescription(
                message.getString("type"),
                message.getString("sdp")
            )
            "ice" -> {
                val candidate = message.getJSONObject("candidate")
                listener.onIceCandidate(
                    IceCandidateMessage(
                        candidate = candidate.getString("candidate"),
                        sdpMid = candidate.optString("sdpMid", null),
                        sdpMLineIndex = candidate.optInt("sdpMLineIndex", 0)
                    )
                )
            }
            "error" -> {
                val errMsg = "Signaling error: ${message.optString("code")} ${message.optString("message")}"
                Log.e(TAG, errMsg)
                listener.onError(errMsg)
            }
        }
    }

    private fun send(payload: JSONObject) {
        socket?.send(payload.toString())
    }

    private fun startHeartbeat() {
        heartbeat?.cancel()
        heartbeat = Timer("quest-phone-heartbeat", true).apply {
            scheduleAtFixedRate(object : TimerTask() {
                override fun run() {
                    send(
                        JSONObject()
                            .put("type", "heartbeat")
                            .put("token", token)
                            .put("deviceId", deviceId)
                            .put("timestamp", System.currentTimeMillis())
                    )
                }
            }, 5000, 15000)
        }
    }
}

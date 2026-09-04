package com.questphonestream.agent

import android.os.Handler
import android.os.Looper
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

data class IceCandidateMessage(val candidate: String, val sdpMid: String?, val sdpMLineIndex: Int)

data class StreamSession(
    val sessionId: String,
    val androidDeviceId: String,
    val questDeviceId: String,
    val negotiationId: String?
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
        fun onRegistered() {}
        fun onSessionCreated(session: StreamSession) {}
        fun onRemoteDescription(session: StreamSession, type: String, sdp: String) {}
        fun onIceCandidate(session: StreamSession, candidate: IceCandidateMessage) {}
        fun onSessionEnded() {}
        fun onStateChanged(state: ConnectionState) {}
        fun onError(message: String) {}
    }

    private val main = Handler(Looper.getMainLooper())
    private val client = OkHttpClient.Builder().pingInterval(15, TimeUnit.SECONDS).build()
    private var socket: WebSocket? = null
    private var heartbeat: Timer? = null
    private var activeSession: StreamSession? = null
    private var closed = false
    val currentState: ConnectionState get() = state
    private var state = ConnectionState.IDLE
        set(value) { field = value; listener.onStateChanged(value) }

    fun connect() {
        state = ConnectionState.CONNECTING
        socket = client.newWebSocket(Request.Builder().url(url).build(), object : WebSocketListener() {
            override fun onOpen(webSocket: WebSocket, response: Response) {
                main.post {
                    if (closed || socket !== webSocket) return@post
                    send(JSONObject().put("type", "register").put("token", token).put("role", role).put("deviceId", deviceId))
                    listener.onOpen()
                }
            }
            override fun onMessage(webSocket: WebSocket, text: String) {
                main.post {
                    if (closed || socket !== webSocket) return@post
                    runCatching { handleMessage(JSONObject(text)) }.onFailure {
                        listener.onError("Invalid signaling message")
                    }
                }
            }
            override fun onFailure(webSocket: WebSocket, t: Throwable, response: Response?) {
                main.post {
                    if (closed || socket !== webSocket) return@post
                    endSession()
                    heartbeat?.cancel()
                    state = ConnectionState.FAILED
                    listener.onError("Signaling connection failed")
                    scheduleReconnect()
                }
            }
            override fun onClosing(webSocket: WebSocket, code: Int, reason: String) {
                webSocket.close(code, null)
            }
            override fun onClosed(webSocket: WebSocket, code: Int, reason: String) {
                main.post {
                    if (closed || socket !== webSocket) return@post
                    endSession()
                    heartbeat?.cancel()
                    state = ConnectionState.CLOSED
                    scheduleReconnect()
                }
            }
        })
    }

    // Self-healing: the LAN WebSocket is not guaranteed to stay up, and without a
    // reconnect the streamer stays offline forever after a drop. Retry on any
    // unexpected close/failure until the client is explicitly closed.
    private fun scheduleReconnect() {
        if (closed) return
        main.postDelayed({
            if (closed) return@postDelayed
            state = ConnectionState.CONNECTING
            listener.onStateChanged(state)
            connect()
        }, 3000)
    }

    fun createSession(sessionId: String, androidDeviceId: String, questDeviceId: String) {
        send(JSONObject().put("type", "create_session").put("token", token)
            .put("sessionId", sessionId).put("androidDeviceId", androidDeviceId).put("questDeviceId", questDeviceId))
    }

    private fun relay(type: String, session: StreamSession): JSONObject =
        JSONObject().put("type", type).put("token", token).put("sessionId", session.sessionId)
            .put("negotiationId", session.negotiationId).put("from", deviceId).put("to", session.questDeviceId)

    fun sendSdp(type: String, session: StreamSession, sdp: String) {
        if (session == activeSession && !closed) send(relay(type, session).put("sdp", sdp))
    }

    fun sendIce(session: StreamSession, candidate: IceCandidateMessage) {
        if (session != activeSession || closed) return
        send(relay("ice", session).put("candidate", JSONObject()
            .put("candidate", candidate.candidate).put("sdpMid", candidate.sdpMid).put("sdpMLineIndex", candidate.sdpMLineIndex)))
    }

    private fun handleMessage(message: JSONObject) {
        when (message.optString("type")) {
            "registered" -> {
                if (message.optString("deviceId") != deviceId || message.optString("role") != role) return
                state = ConnectionState.CONNECTED
                startHeartbeat()
                listener.onRegistered()
            }
            "session_created" -> {
                if (message.optString("androidDeviceId") != deviceId) return
                val session = StreamSession(message.getString("sessionId"), deviceId,
                    message.getString("questDeviceId"), message.optString("negotiationId").ifEmpty { null })
                if (session == activeSession) return
                activeSession = session
                listener.onSessionCreated(session)
            }
            "answer", "ice" -> {
                val session = activeSession ?: return
                if (!matches(message, session) || message.optString("from") != session.questDeviceId ||
                    message.optString("to") != deviceId) return
                if (message.optString("type") == "answer")
                    listener.onRemoteDescription(session, "answer", message.getString("sdp"))
                else {
                    val ice = message.getJSONObject("candidate")
                    listener.onIceCandidate(session, IceCandidateMessage(ice.getString("candidate"),
                        ice.optString("sdpMid", null), ice.optInt("sdpMLineIndex", 0)))
                }
            }
            "peer_unavailable" -> {
                val session = activeSession ?: return
                if (matches(message, session)) endSession()
            }
            "error" -> {
                val session = activeSession
                if (message.has("sessionId") && (session == null || !matches(message, session))) return
                val code = message.optString("code")
                if (code == "session_replaced" || code == "unauthorized") endSession()
                // Only fixed diagnostic text; never echo a server payload or credential.
                listener.onError(if (code == "unauthorized") "Authentication failed" else "Signaling request rejected")
            }
        }
    }

    private fun matches(message: JSONObject, session: StreamSession): Boolean =
        message.optString("sessionId") == session.sessionId &&
            message.optString("negotiationId").ifEmpty { null } == session.negotiationId

    private fun endSession() {
        activeSession = null
        listener.onSessionEnded()
    }

    fun close() {
        closed = true
        heartbeat?.cancel()
        socket?.close(1000, "closed")
        endSession()
        state = ConnectionState.CLOSED
        client.dispatcher.executorService.shutdown()
    }

    private fun send(payload: JSONObject) { if (!closed) socket?.send(payload.toString()) }

    private fun startHeartbeat() {
        heartbeat?.cancel()
        heartbeat = Timer("quest-phone-heartbeat", true).apply {
            scheduleAtFixedRate(object : TimerTask() {
                override fun run() {
                    send(JSONObject().put("type", "heartbeat").put("token", token)
                        .put("deviceId", deviceId).put("timestamp", System.currentTimeMillis()))
                }
            }, 5000, 15000)
        }
    }
}

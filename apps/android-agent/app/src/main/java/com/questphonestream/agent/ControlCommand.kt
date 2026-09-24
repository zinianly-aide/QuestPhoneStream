package com.questphonestream.agent

import android.accessibilityservice.AccessibilityService
import android.accessibilityservice.GestureDescription
import android.graphics.Path
import android.os.Bundle
import android.util.Log
import org.json.JSONObject

data class ControlCommand(
    val version: String,
    val type: String,
    val sessionId: String,
    val deviceId: String,
    val x: Int = 0,
    val y: Int = 0,
    val startX: Int = 0,
    val startY: Int = 0,
    val endX: Int = 0,
    val endY: Int = 0,
    val durationMs: Long = 100,
    val text: String = "",
    val timestamp: Long = 0
) {
    companion object {
        fun fromJson(json: String): ControlCommand {
            val obj = JSONObject(json)
            return ControlCommand(
                version = obj.getString("version"),
                type = obj.getString("type"),
                sessionId = obj.getString("sessionId"),
                deviceId = obj.getString("deviceId"),
                x = obj.optInt("x", 0),
                y = obj.optInt("y", 0),
                startX = obj.optInt("startX", 0),
                startY = obj.optInt("startY", 0),
                endX = obj.optInt("endX", 0),
                endY = obj.optInt("endY", 0),
                durationMs = obj.optLong("durationMs", 100),
                text = obj.optString("text", ""),
                timestamp = obj.optLong("timestamp", 0)
            )
        }
    }
}

object ControlCommandDispatcher {
    private var service: ControlAccessibilityService? = null

    fun attach(service: ControlAccessibilityService) {
        this.service = service
    }

    fun detach(service: ControlAccessibilityService) {
        if (this.service === service) this.service = null
    }

    fun dispatch(json: String) {
        val command = runCatching { ControlCommand.fromJson(json) }
            .onFailure { Log.e(TAG, "Invalid control command: $json", it) }
            .getOrNull() ?: return
        Log.i(TAG, "Dispatching command: type=${command.type} x=${command.x} y=${command.y} start=(${command.startX},${command.startY}) end=(${command.endX},${command.endY})")
        service?.execute(command) ?: Log.w(TAG, "Accessibility service is not enabled")
    }
}

data class VideoResolution(val width: Int, val height: Int)

/**
 * Holds the current video encoding resolution so the accessibility service can
 * scale incoming touch coordinates from video-space to real screen pixels.
 * The immutable snapshot prevents a rotation from exposing mixed old/new axes.
 */
object VideoResolutionHolder {
    @Volatile private var value = VideoResolution(720, 1280)

    val current: VideoResolution get() = value
    val width: Int get() = value.width
    val height: Int get() = value.height

    fun update(width: Int, height: Int) {
        if (width <= 0 || height <= 0) return
        value = VideoResolution(width, height)
    }
}

class ControlAccessibilityService : AccessibilityService() {
    override fun onServiceConnected() {
        super.onServiceConnected()
        ControlCommandDispatcher.attach(this)
        DeviceControlPlane.setControlAuthorized(true)
        Log.i(TAG, "Control accessibility service connected")
    }

    override fun onDestroy() {
        DeviceControlPlane.setControlAuthorized(false)
        ControlCommandDispatcher.detach(this)
        CapabilityRuntime.setAccessibilityAvailable(false)
        super.onDestroy()
    }

    override fun onAccessibilityEvent(event: android.view.accessibility.AccessibilityEvent?) = Unit
    override fun onInterrupt() = Unit

    fun execute(command: ControlCommand) {
        Log.i(TAG, "Execute command: type=${command.type}")
        val video = VideoResolutionHolder.current
        when (command.type) {
            "click" -> gesture(scaleX(command.x, video), scaleY(command.y, video), scaleX(command.x, video), scaleY(command.y, video), 1, 80, video)
            "long_press" -> gesture(scaleX(command.x, video), scaleY(command.y, video), scaleX(command.x, video), scaleY(command.y, video), 1, command.durationMs.coerceAtLeast(500), video)
            "swipe" -> gesture(
                scaleX(command.startX, video), scaleY(command.startY, video),
                scaleX(command.endX, video), scaleY(command.endY, video),
                0, command.durationMs.coerceAtLeast(100), video
            )
            "back" -> performGlobalAction(GLOBAL_ACTION_BACK)
            "home" -> performGlobalAction(GLOBAL_ACTION_HOME)
            "text_input" -> inputText(command.text)
            else -> Log.w(TAG, "Unsupported command: ${command.type}")
        }
    }

    /** Scale an x coordinate from video-resolution space to current real screen pixels. */
    private fun scaleX(x: Int, video: VideoResolution): Int {
        val screenWidth = resources.displayMetrics.widthPixels.coerceAtLeast(1)
        return ((x.toLong() * screenWidth) / video.width.coerceAtLeast(1))
            .toInt().coerceIn(0, screenWidth)
    }

    /** Scale a y coordinate from video-resolution space to current real screen pixels. */
    private fun scaleY(y: Int, video: VideoResolution): Int {
        val screenHeight = resources.displayMetrics.heightPixels.coerceAtLeast(1)
        return ((y.toLong() * screenHeight) / video.height.coerceAtLeast(1))
            .toInt().coerceIn(0, screenHeight)
    }

    private fun gesture(
        startX: Int,
        startY: Int,
        endX: Int,
        endY: Int,
        startTime: Long,
        durationMs: Long,
        video: VideoResolution
    ) {
        Log.i(
            TAG,
            "Gesture: ($startX,$startY)→($endX,$endY) dur=${durationMs}ms " +
                "screen=${resources.displayMetrics.widthPixels}x${resources.displayMetrics.heightPixels} " +
                "video=${video.width}x${video.height}"
        )
        val path = Path().apply {
            moveTo(startX.toFloat(), startY.toFloat())
            if (startX != endX || startY != endY) lineTo(endX.toFloat(), endY.toFloat())
        }
        val gesture = GestureDescription.Builder()
            .addStroke(GestureDescription.StrokeDescription(path, startTime, durationMs))
            .build()
        dispatchGesture(gesture, null, null)
    }

    private fun inputText(text: String) {
        val focused = rootInActiveWindow?.findFocus(android.view.accessibility.AccessibilityNodeInfo.FOCUS_INPUT)
        if (focused == null) {
            Log.w(TAG, "No focused input node")
            return
        }
        focused.performAction(
            android.view.accessibility.AccessibilityNodeInfo.ACTION_SET_TEXT,
            Bundle().apply {
                putCharSequence(android.view.accessibility.AccessibilityNodeInfo.ACTION_ARGUMENT_SET_TEXT_CHARSEQUENCE, text)
            }
        )
    }
}

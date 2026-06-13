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
        service?.execute(command) ?: Log.w(TAG, "Accessibility service is not enabled")
    }
}

class ControlAccessibilityService : AccessibilityService() {
    override fun onServiceConnected() {
        super.onServiceConnected()
        ControlCommandDispatcher.attach(this)
        Log.i(TAG, "Control accessibility service connected")
    }

    override fun onDestroy() {
        ControlCommandDispatcher.detach(this)
        super.onDestroy()
    }

    override fun onAccessibilityEvent(event: android.view.accessibility.AccessibilityEvent?) = Unit
    override fun onInterrupt() = Unit

    fun execute(command: ControlCommand) {
        when (command.type) {
            "click" -> gesture(command.x, command.y, command.x, command.y, 1, 80)
            "long_press" -> gesture(command.x, command.y, command.x, command.y, 1, command.durationMs.coerceAtLeast(500))
            "swipe" -> gesture(command.startX, command.startY, command.endX, command.endY, 0, command.durationMs.coerceAtLeast(100))
            "back" -> performGlobalAction(GLOBAL_ACTION_BACK)
            "home" -> performGlobalAction(GLOBAL_ACTION_HOME)
            "text_input" -> inputText(command.text)
            else -> Log.w(TAG, "Unsupported command: ${command.type}")
        }
    }

    private fun gesture(startX: Int, startY: Int, endX: Int, endY: Int, startTime: Long, durationMs: Long) {
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


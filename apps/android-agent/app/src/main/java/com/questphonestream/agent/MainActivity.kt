package com.questphonestream.agent

import android.Manifest
import android.app.Activity
import android.content.Context
import android.content.Intent
import android.media.projection.MediaProjectionManager
import android.net.Uri
import android.os.Build
import android.os.Bundle
import android.provider.Settings
import android.view.Gravity
import android.widget.Button
import android.widget.EditText
import android.widget.LinearLayout
import android.widget.TextView
import androidx.activity.result.contract.ActivityResultContracts
import androidx.appcompat.app.AppCompatActivity
import androidx.core.content.ContextCompat

class MainActivity : AppCompatActivity() {
    private lateinit var statusView: TextView
    private lateinit var signalingUrl: EditText
    private lateinit var token: EditText
    private lateinit var deviceId: EditText
    private lateinit var questDeviceId: EditText
    private lateinit var sessionId: EditText

    private val projectionLauncher = registerForActivityResult(
        ActivityResultContracts.StartActivityForResult()
    ) { result ->
        if (result.resultCode == Activity.RESULT_OK && result.data != null) {
            ScreenStreamService.start(
                context = this,
                resultCode = result.resultCode,
                data = result.data!!,
                config = StreamConfig(
                    signalingUrl = signalingUrl.text.toString(),
                    token = token.text.toString(),
                    deviceId = deviceId.text.toString(),
                    questDeviceId = questDeviceId.text.toString(),
                    sessionId = sessionId.text.toString()
                )
            )
            statusView.text = "Streaming service started"
        } else {
            statusView.text = "Screen capture permission denied"
        }
    }

    private val notificationPermissionLauncher =
        registerForActivityResult(ActivityResultContracts.RequestPermission()) {}

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        maybeRequestNotificationPermission()

        statusView = TextView(this).apply { text = "Idle" }
        signalingUrl = field("ws://192.168.1.10:8787")
        token = field("dev-token")
        deviceId = field("android-phone-001")
        questDeviceId = field("quest-3s-001")
        sessionId = field("local-session-001")

        val startButton = Button(this).apply {
            text = "Start Screen Stream"
            setOnClickListener { requestMediaProjection() }
        }
        val stopButton = Button(this).apply {
            text = "Stop"
            setOnClickListener {
                stopService(Intent(this@MainActivity, ScreenStreamService::class.java))
                statusView.text = "Stopped"
            }
        }
        val accessibilityButton = Button(this).apply {
            text = "Open Accessibility Settings"
            setOnClickListener { startActivity(Intent(Settings.ACTION_ACCESSIBILITY_SETTINGS)) }
        }
        val appSettingsButton = Button(this).apply {
            text = "Open App Settings"
            setOnClickListener {
                startActivity(Intent(Settings.ACTION_APPLICATION_DETAILS_SETTINGS).apply {
                    data = Uri.parse("package:$packageName")
                })
            }
        }

        setContentView(
            LinearLayout(this).apply {
                orientation = LinearLayout.VERTICAL
                gravity = Gravity.CENTER_HORIZONTAL
                setPadding(32, 48, 32, 32)
                addView(label("Signaling URL")); addView(signalingUrl)
                addView(label("Token")); addView(token)
                addView(label("Android Device ID")); addView(deviceId)
                addView(label("Quest Device ID")); addView(questDeviceId)
                addView(label("Session ID")); addView(sessionId)
                addView(startButton); addView(stopButton); addView(accessibilityButton); addView(appSettingsButton)
                addView(statusView)
            }
        )
    }

    private fun requestMediaProjection() {
        // Android 14+ requires a fresh consent Intent for each MediaProjection session.
        val manager = getSystemService(Context.MEDIA_PROJECTION_SERVICE) as MediaProjectionManager
        projectionLauncher.launch(manager.createScreenCaptureIntent())
    }

    private fun maybeRequestNotificationPermission() {
        if (Build.VERSION.SDK_INT >= 33) {
            notificationPermissionLauncher.launch(Manifest.permission.POST_NOTIFICATIONS)
        }
    }

    private fun field(defaultValue: String) = EditText(this).apply {
        setSingleLine(true)
        setText(defaultValue)
    }

    private fun label(text: String) = TextView(this).apply { this.text = text }
}


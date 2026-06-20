package com.questphonestream.agent

import android.Manifest
import android.app.Activity
import android.app.AlertDialog
import android.content.ClipData
import android.content.ClipboardManager
import android.content.Context
import android.content.Intent
import android.graphics.Color
import android.graphics.Typeface
import android.media.projection.MediaProjectionManager
import android.net.Uri
import android.os.Build
import android.os.Bundle
import android.provider.Settings
import android.text.InputType
import android.util.TypedValue
import android.view.Gravity
import android.view.View
import android.widget.Button
import android.widget.EditText
import android.widget.HorizontalScrollView
import android.widget.LinearLayout
import android.widget.ScrollView
import android.widget.TextView
import androidx.activity.result.contract.ActivityResultContracts
import androidx.appcompat.app.AppCompatActivity
import androidx.core.content.ContextCompat
import androidx.core.view.setPadding
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

class MainActivity : AppCompatActivity() {

    // --- Config fields ---
    private lateinit var signalingUrlField: EditText
    private lateinit var tokenField: EditText
    private lateinit var deviceIdField: EditText
    private lateinit var questDeviceIdField: EditText
    private lateinit var sessionIdField: EditText

    // --- Status views ---
    private lateinit var signalingStatusView: TextView
    private lateinit var webrtcStatusView: TextView
    private lateinit var screenCaptureStatusView: TextView
    private lateinit var currentSessionIdView: TextView
    private lateinit var urlModeIndicator: TextView

    // --- Log ---
    private val logEntries = mutableListOf<String>()
    private lateinit var logContainer: LinearLayout

    // --- State ---
    private var isStreaming = false
    private var currentSignalingState = ConnectionState.IDLE

    private val projectionLauncher = registerForActivityResult(
        ActivityResultContracts.StartActivityForResult()
    ) { result ->
        if (result.resultCode == Activity.RESULT_OK && result.data != null) {
            ScreenStreamService.start(
                context = this,
                resultCode = result.resultCode,
                data = result.data!!,
                config = StreamConfig(
                    signalingUrl = signalingUrlField.text.toString(),
                    token = tokenField.text.toString(),
                    deviceId = deviceIdField.text.toString(),
                    questDeviceId = questDeviceIdField.text.toString(),
                    sessionId = sessionIdField.text.toString()
                )
            )
            isStreaming = true
            updateScreenCaptureStatus()
            addLog("Streaming service started, session=${sessionIdField.text}")
        } else {
            screenCaptureStatusView.setText("Denied", TextView.BufferType.NORMAL)
            screenCaptureStatusView.setTextColor(color(R.color.status_error))
            addLog("Screen capture permission denied")
        }
    }

    private val notificationPermissionLauncher =
        registerForActivityResult(ActivityResultContracts.RequestPermission()) {}

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        maybeRequestNotificationPermission()

        val root = ScrollView(this).apply { layoutParams = LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.MATCH_PARENT) }
        val container = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setPadding(dp(16))
        }

        // ── 1. Title ──
        container.addView(sectionTitle("Quest Phone Stream Agent"))

        // ── 2. Status Card ──
        container.addView(sectionLabel("STATUS"))
        val statusCard = cardLayout()
        signalingStatusView = statusRow(statusCard, "Signaling", "Idle")
        webrtcStatusView = statusRow(statusCard, "WebRTC", "Idle")
        screenCaptureStatusView = statusRow(statusCard, "Screen Capture", "Idle")
        currentSessionIdView = statusRow(statusCard, "Session ID", "—")
        container.addView(statusCard)

        // ── URL mode indicator ──
        urlModeIndicator = TextView(this).apply {
            setPadding(dp(12), dp(4), dp(12), dp(4))
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 12f)
            setTypeface(null, Typeface.BOLD)
        }
        container.addView(urlModeIndicator)

        // ── 3. Config ──
        container.addView(sectionLabel("CONFIGURATION"))
        val configCard = cardLayout()
        signalingUrlField = configRow(configCard, "Signaling URL", "ws://192.168.1.10:8787")
        tokenField = configRow(configCard, "Token", "dev-token")
        deviceIdField = configRow(configCard, "Android Device ID", "android-phone-001")
        questDeviceIdField = configRow(configCard, "Quest Device ID", "quest-3s-001")
        sessionIdField = configRow(configCard, "Session ID", "local-session-001")
        container.addView(configCard)

        signalingUrlField.setOnFocusChangeListener { _, _ -> updateUrlModeIndicator() }

        // ── 4. Actions ──
        container.addView(sectionLabel("ACTIONS"))
        val actionsCard = cardLayout()

        val testBtn = actionButton("🔌 Test Connection", R.color.colorPrimary) { testConnection() }
        actionsCard.addView(testBtn)

        val startBtn = actionButton("▶ Start Screen Stream", R.color.status_ok) { startStream() }
        actionsCard.addView(startBtn)

        val stopBtn = actionButton("⏹ Stop", R.color.status_error) { stopStream() }
        actionsCard.addView(stopBtn)

        val certBtn = actionButton("🔐 Install / Trust Certificate", R.color.btn_cert) { showCertDialog() }
        actionsCard.addView(certBtn)

        val a11yBtn = actionButton("♿ Open Accessibility Settings", R.color.status_idle) {
            startActivity(Intent(Settings.ACTION_ACCESSIBILITY_SETTINGS))
        }
        actionsCard.addView(a11yBtn)

        val appSettingsBtn = actionButton("⚙ Open App Settings", R.color.status_idle) {
            val settingsIntent = Intent(Settings.ACTION_APPLICATION_DETAILS_SETTINGS)
            settingsIntent.data = Uri.parse("package:$packageName")
            startActivity(settingsIntent)
        }
        actionsCard.addView(appSettingsBtn)

        container.addView(actionsCard)

        // ── 5. Log ──
        container.addView(sectionLabel("LOG"))
        val logCard = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setPadding(dp(8))
            setBackgroundColor(color(R.color.log_bg))
        }
        val logScroll = ScrollView(this).apply {
            layoutParams = LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, dp(200))
        }
        logContainer = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
        }
        logScroll.addView(logContainer)
        logCard.addView(logScroll)
        container.addView(logCard)

        val clearLogBtn = Button(this).apply {
            text = "Clear Log"
            setTextColor(Color.WHITE)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 12f)
            setBackgroundColor(Color.TRANSPARENT)
            setPadding(0, dp(4), 0, dp(4))
            setOnClickListener {
                logEntries.clear()
                logContainer.removeAllViews()
            }
        }
        logCard.addView(clearLogBtn)
        container.addView(logCard)

        // ── Bottom spacing ──
        container.addView(View(this), LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, dp(48)))

        root.addView(container)
        setContentView(root)

        updateUrlModeIndicator()
        addLog("App started")
    }

    // ─── Actions ───

    private fun testConnection() {
        val url = signalingUrlField.text.toString().trim()
        if (url.isEmpty()) {
            addLog("ERROR: Signaling URL is empty")
            return
        }
        addLog("ws connecting to $url ...")
        signalingStatusView.setText("Connecting...", TextView.BufferType.NORMAL)
        signalingStatusView.setTextColor(color(R.color.status_warn))

        val testClient = SignalingClient(
            url = url,
            token = tokenField.text.toString(),
            role = "android",
            deviceId = deviceIdField.text.toString(),
            listener = object : SignalingClient.Listener {
                override fun onOpen() {
                    runOnUiThread {
                        addLog("ws connected ✓")
                        signalingStatusView.setText("Connected (test)", TextView.BufferType.NORMAL)
                        signalingStatusView.setTextColor(color(R.color.status_ok))
                    }
                }

                override fun onStateChanged(state: ConnectionState) {
                    runOnUiThread {
                        updateSignalingStatus(state)
                    }
                }

                override fun onError(message: String) {
                    runOnUiThread {
                        addLog("ws failed: $message")
                        signalingStatusView.setText("Failed", TextView.BufferType.NORMAL)
                        signalingStatusView.setTextColor(color(R.color.status_error))
                    }
                }
            }
        )
        testClient.connect()
        // Auto-close after 5s
        android.os.Handler(mainLooper).postDelayed({
            testClient.close()
            if (currentSignalingState != ConnectionState.CONNECTED) {
                signalingStatusView.setText("Idle", TextView.BufferType.NORMAL)
                signalingStatusView.setTextColor(color(R.color.status_idle))
            }
        }, 5000)
    }

    private fun startStream() {
        val url = signalingUrlField.text.toString().trim()
        if (url.isEmpty()) {
            addLog("ERROR: Signaling URL is empty, cannot start")
            return
        }
        addLog("Requesting screen capture permission ...")
        val manager = getSystemService(Context.MEDIA_PROJECTION_SERVICE) as MediaProjectionManager
        projectionLauncher.launch(manager.createScreenCaptureIntent())
    }

    private fun stopStream() {
        val serviceIntent = Intent()
        serviceIntent.setClassName(packageName, "com.questphonestream.agent.ScreenStreamService")
        stopService(serviceIntent)
        isStreaming = false
        updateScreenCaptureStatus()
        signalingStatusView.setText("Idle", TextView.BufferType.NORMAL)
        signalingStatusView.setTextColor(color(R.color.status_idle))
        webrtcStatusView.setText("Idle", TextView.BufferType.NORMAL)
        webrtcStatusView.setTextColor(color(R.color.status_idle))
        currentSessionIdView.setText("—", TextView.BufferType.NORMAL)
        addLog("Streaming stopped")
    }

    // ─── Cert Dialog ───

    private fun showCertDialog() {
        val url = signalingUrlField.text.toString().trim()
        val isWss = url.startsWith("wss://", ignoreCase = true)

        val message = buildString {
            if (!isWss) {
                append("当前使用 ws:// 明文连接，无需安装证书。\n\n")
            } else {
                append("当前使用 wss:// TLS 连接，需要 CA 证书受信任。\n\n")
            }
            append("📋 证书说明：\n")
            append("• 仅 wss:// 自签名证书时需要安装 CA\n")
            append("• ws:// 局域网调试不需要证书\n")
            append("• Android 不允许 App 静默安装 CA\n")
            append("• 需要用户手动安装\n\n")
            append("🔧 安装路径：\n")
            append("设置 → 安全 → 加密与凭据 → 安装证书 → CA 证书")
        }

        AlertDialog.Builder(this)
            .setTitle("🔐 安装/信任证书")
            .setMessage(message)
            .setPositiveButton("打开安全设置") { _, _ ->
                // Try to open the security settings directly
                try {
                    startActivity(Intent("android.settings.SECURITY_SETTINGS"))
                } catch (_: Exception) {
                    startActivity(Intent(Settings.ACTION_SETTINGS))
                }
            }
            .setNeutralButton("复制 Signaling URL") { _, _ ->
                val clipboard = getSystemService(CLIPBOARD_SERVICE) as ClipboardManager
                clipboard.setPrimaryClip(ClipData.newPlainText("Signaling URL", url))
                addLog("URL copied: $url")
            }
            .setNegativeButton("我已安装，测试连接") { _, _ ->
                testConnection()
            }
            .show()
    }

    // ─── UI Helpers ───

    private fun updateUrlModeIndicator() {
        val url = signalingUrlField.text.toString().trim()
        if (url.isEmpty()) {
            urlModeIndicator.text = "⚠ 未配置 Signaling URL"
            urlModeIndicator.setTextColor(color(R.color.status_error))
        } else if (url.startsWith("ws://", ignoreCase = true)) {
            urlModeIndicator.text = "🔓 局域网明文调试模式 (ws://)"
            urlModeIndicator.setTextColor(color(R.color.status_warn))
        } else if (url.startsWith("wss://", ignoreCase = true)) {
            urlModeIndicator.text = "🔒 TLS/WSS 模式 — 需要证书受信任"
            urlModeIndicator.setTextColor(color(R.color.status_ok))
        } else {
            urlModeIndicator.text = "⚠ URL 格式异常"
            urlModeIndicator.setTextColor(color(R.color.status_error))
        }
    }

    private fun updateSignalingStatus(state: ConnectionState) {
        currentSignalingState = state
        when (state) {
            ConnectionState.IDLE -> {
                signalingStatusView.setText("Idle", TextView.BufferType.NORMAL)
                signalingStatusView.setTextColor(color(R.color.status_idle))
            }
            ConnectionState.CONNECTING -> {
                signalingStatusView.setText("Connecting...", TextView.BufferType.NORMAL)
                signalingStatusView.setTextColor(color(R.color.status_warn))
            }
            ConnectionState.CONNECTED -> {
                signalingStatusView.setText("Connected", TextView.BufferType.NORMAL)
                signalingStatusView.setTextColor(color(R.color.status_ok))
            }
            ConnectionState.FAILED -> {
                signalingStatusView.setText("Failed", TextView.BufferType.NORMAL)
                signalingStatusView.setTextColor(color(R.color.status_error))
            }
            ConnectionState.CLOSED -> {
                signalingStatusView.setText("Closed", TextView.BufferType.NORMAL)
                signalingStatusView.setTextColor(color(R.color.status_idle))
            }
        }
    }

    private fun updateScreenCaptureStatus() {
        if (isStreaming) {
            screenCaptureStatusView.setText("Streaming", TextView.BufferType.NORMAL)
            screenCaptureStatusView.setTextColor(color(R.color.status_ok))
        } else {
            screenCaptureStatusView.setText("Idle", TextView.BufferType.NORMAL)
            screenCaptureStatusView.setTextColor(color(R.color.status_idle))
        }
    }

    @Synchronized
    private fun addLog(message: String) {
        val timeFmt = SimpleDateFormat("HH:mm:ss", Locale.getDefault())
        val entry = "[${timeFmt.format(Date())}] $message"
        logEntries.add(entry)
        if (logEntries.size > 20) logEntries.removeAt(0)

        val tv = TextView(this).apply {
            text = entry
            setTextColor(color(R.color.log_text))
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 11f)
            setTypeface(Typeface.MONOSPACE)
        }
        logContainer.addView(tv)

        // Remove excess views
        while (logContainer.childCount > 20) {
            logContainer.removeViewAt(0)
        }

        // Auto-scroll
        (logContainer.parent as? ScrollView)?.post {
            (logContainer.parent as? ScrollView)?.fullScroll(View.FOCUS_DOWN)
        }
    }

    // ─── Layout Builders ───

    private fun sectionTitle(text: String): TextView = TextView(this).apply {
        this.text = text
        setTextSize(TypedValue.COMPLEX_UNIT_SP, 20f)
        setTypeface(null, Typeface.BOLD)
        setTextColor(color(R.color.colorPrimaryDark))
        gravity = Gravity.CENTER_HORIZONTAL
        setPadding(0, dp(8), 0, dp(16))
    }

    private fun sectionLabel(text: String): TextView = TextView(this).apply {
        this.text = text
        setTextSize(TypedValue.COMPLEX_UNIT_SP, 12f)
        setTypeface(null, Typeface.BOLD)
        setTextColor(color(R.color.colorPrimary))
        setPadding(0, dp(16), 0, dp(4))
    }

    private fun cardLayout(): LinearLayout = LinearLayout(this).apply {
        orientation = LinearLayout.VERTICAL
        setPadding(dp(12))
        setBackgroundColor(color(R.color.card_bg))
        val margin = LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT)
        margin.setMargins(0, 0, 0, dp(8))
        layoutParams = margin
    }

    private fun statusRow(parent: LinearLayout, label: String, initialValue: String): TextView {
        val row = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            layoutParams = LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT)
        }
        val labelView = TextView(this).apply {
            text = "$label:"
            setTypeface(null, Typeface.BOLD)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 14f)
            layoutParams = LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1f)
        }
        val valueView = TextView(this).apply {
            text = initialValue
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 14f)
            setTextColor(color(R.color.status_idle))
            layoutParams = LinearLayout.LayoutParams(LinearLayout.LayoutParams.WRAP_CONTENT, LinearLayout.LayoutParams.WRAP_CONTENT)
        }
        row.addView(labelView)
        row.addView(valueView)
        parent.addView(row)
        return valueView
    }

    private fun configRow(parent: LinearLayout, label: String, defaultValue: String): EditText {
        val labelView = TextView(this).apply {
            text = label
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 12f)
            setTextColor(color(R.color.status_idle))
            setPadding(0, dp(8), 0, 0)
        }
        parent.addView(labelView)
        val field = EditText(this).apply {
            setSingleLine(true)
            setText(defaultValue)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 14f)
            if (label.contains("Token", ignoreCase = true)) {
                inputType = InputType.TYPE_CLASS_TEXT or InputType.TYPE_TEXT_VARIATION_PASSWORD
            }
            layoutParams = LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT)
        }
        parent.addView(field)
        return field
    }

    private fun actionButton(text: String, colorRes: Int, onClick: () -> Unit): Button {
        return Button(this).apply {
            this.text = text
            setTextColor(Color.WHITE)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 14f)
            setBackgroundColor(color(colorRes))
            setPadding(dp(16), dp(8), dp(16), dp(8))
            val params = LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT)
            params.setMargins(0, dp(4), 0, dp(4))
            layoutParams = params
            setOnClickListener { onClick() }
        }
    }

    // ─── Util ───

    private fun dp(value: Int): Int = TypedValue.applyDimension(
        TypedValue.COMPLEX_UNIT_DIP, value.toFloat(), resources.displayMetrics
    ).toInt()

    private fun color(resId: Int): Int = ContextCompat.getColor(this, resId)

    private fun maybeRequestNotificationPermission() {
        if (Build.VERSION.SDK_INT >= 33) {
            notificationPermissionLauncher.launch(Manifest.permission.POST_NOTIFICATIONS)
        }
    }
}

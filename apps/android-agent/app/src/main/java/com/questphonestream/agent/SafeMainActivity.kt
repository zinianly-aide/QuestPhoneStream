package com.questphonestream.agent

import android.content.Intent
import android.os.Build
import android.os.Bundle
import android.provider.Settings
import android.graphics.Color
import android.view.Gravity
import android.widget.Button
import android.widget.LinearLayout
import android.widget.TextView
import androidx.appcompat.app.AppCompatActivity

class SafeMainActivity : AppCompatActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        val container = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            gravity = Gravity.CENTER_HORIZONTAL
            setPadding(48, 64, 48, 48)
        }

        container.addView(TextView(this).apply {
            text = "QuestPhoneStream Safe Start"
            textSize = 22f
            setTextColor(Color.BLACK)
        })

        container.addView(TextView(this).apply {
            text = buildString {
                append("Android ")
                append(Build.VERSION.RELEASE)
                append(" (API ")
                append(Build.VERSION.SDK_INT)
                append(")\n")
                append(Build.MANUFACTURER)
                append(" ")
                append(Build.MODEL)
            }
            textSize = 15f
            setPadding(0, 24, 0, 32)
            setTextColor(Color.DKGRAY)
        })

        container.addView(Button(this).apply {
            text = "Open full app UI"
            setOnClickListener {
                startActivity(Intent(this@SafeMainActivity, MainActivity::class.java))
            }
        })

        container.addView(Button(this).apply {
            text = "Open app settings"
            setOnClickListener {
                startActivity(Intent(Settings.ACTION_APPLICATION_DETAILS_SETTINGS).apply {
                    data = android.net.Uri.parse("package:$packageName")
                })
            }
        })

        container.addView(TextView(this).apply {
            text = "If this page opens normally but the full UI crashes, the fault is inside MainActivity startup. If this page also crashes, capture adb logcat because the problem is below the app UI layer."
            textSize = 13f
            setPadding(0, 32, 0, 0)
            setTextColor(Color.DKGRAY)
        })

        setContentView(container)
    }
}

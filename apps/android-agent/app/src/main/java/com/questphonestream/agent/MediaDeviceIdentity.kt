package com.questphonestream.agent

import android.content.Context
import android.os.Build
import java.util.UUID

/** Stable identity used only for local media-service discovery. */
internal object MediaDeviceIdentity {
    private const val PREFS = "quest_phone_stream_media_identity"
    private const val DEVICE_ID_KEY = "device_id"

    fun getOrCreateDeviceId(context: Context): String {
        val preferences = context.getSharedPreferences(PREFS, Context.MODE_PRIVATE)
        val existing = preferences.getString(DEVICE_ID_KEY, null)?.trim()
        if (!existing.isNullOrEmpty()) return existing
        return UUID.randomUUID().toString().also { id ->
            preferences.edit().putString(DEVICE_ID_KEY, id).apply()
        }
    }

    fun displayName(): String = Build.MODEL?.trim().takeUnless { it.isNullOrEmpty() } ?: "Android phone"
}

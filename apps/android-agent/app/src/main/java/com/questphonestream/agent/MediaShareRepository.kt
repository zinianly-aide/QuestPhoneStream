package com.questphonestream.agent

import android.content.Context
import android.net.Uri
import android.system.Os
import android.system.OsConstants
import org.json.JSONArray
import org.json.JSONObject
import java.security.SecureRandom

/** Small persisted repository for media explicitly selected through SAF. */
class MediaShareRepository(context: Context?) {
    private val appContext = context?.applicationContext
    private val preferences = appContext?.getSharedPreferences(PREFERENCES, Context.MODE_PRIVATE)
    private val items = LinkedHashMap<String, MediaItem>()
    private val random = SecureRandom()

    init { load() }

    @Synchronized
    fun add(uri: Uri, mimeType: String?, displayName: String? = null, size: Long? = null): MediaItem {
        val name = displayName ?: uri.lastPathSegment?.substringAfterLast('/') ?: "media"
        val actualSize = size ?: -1L
        val id = newId()
        val spatialFormat = inferSpatialFormat(name, mimeType)
        val item = MediaItem(
            id, name, mimeType ?: "video/*", actualSize, uri.toString(), detectSeekable(uri), true,
            spatialFormat = spatialFormat
        )
        items[id] = item
        persist()
        return item
    }

    @Synchronized
    fun remove(id: String): Boolean {
        val removed = items.remove(id) != null
        if (removed) persist()
        return removed
    }

    @Synchronized
    fun setShared(id: String, shared: Boolean): MediaItem? {
        val old = items[id] ?: return null
        val updated = old.copy(shared = shared)
        items[id] = updated
        persist()
        return updated
    }

    @Synchronized
    fun setSpatialMetadata(
        id: String,
        spatialFormat: String,
        manifestUrl: String = "",
        referenceSpace: String = "local",
        spatialBounds: SpatialMediaBounds? = null
    ): MediaItem? {
        val old = items[id] ?: return null
        val updated = old.copy(
            spatialFormat = spatialFormat.trim(),
            manifestUrl = manifestUrl.trim(),
            referenceSpace = referenceSpace.trim().ifBlank { "local" },
            spatialBounds = spatialBounds
        )
        items[id] = updated
        persist()
        return updated
    }

    @Synchronized fun get(id: String): MediaItem? = items[id]
    @Synchronized fun all(): List<MediaItem> = items.values.toList()
    @Synchronized fun shared(): List<MediaItem> = items.values.filter { it.shared }

    private fun newId(): String {
        val bytes = ByteArray(10)
        random.nextBytes(bytes)
        return "media_" + bytes.joinToString("") { "%02x".format(it) }
    }

    private fun detectSeekable(uri: Uri): Boolean {
        val context = appContext ?: return true
        return runCatching {
            context.contentResolver.openFileDescriptor(uri, "r")?.use {
                if (!it.fileDescriptor.valid()) return@use false
                Os.lseek(it.fileDescriptor, 0L, OsConstants.SEEK_CUR)
                true
            } ?: false
        }.getOrDefault(false)
    }

    private fun load() {
        val raw = preferences?.getString(KEY_ITEMS, null) ?: return
        runCatching {
            val json = JSONArray(raw)
            for (i in 0 until json.length()) {
                val o = json.getJSONObject(i)
                val bounds = o.optJSONObject("spatialBounds")?.let { b ->
                    SpatialMediaBounds(
                        b.optDouble("centerX", 0.0).toFloat(), b.optDouble("centerY", 0.0).toFloat(), b.optDouble("centerZ", 0.0).toFloat(),
                        b.optDouble("sizeX", 0.0).toFloat(), b.optDouble("sizeY", 0.0).toFloat(), b.optDouble("sizeZ", 0.0).toFloat()
                    )
                }
                val item = MediaItem(
                    o.getString("id"), o.getString("name"), o.getString("mimeType"),
                    o.optLong("size", -1), o.getString("contentUri"), o.optBoolean("seekable", false),
                    o.optBoolean("shared", false), o.optString("projection", "flat"),
                    o.optInt("fov", 360), o.optString("stereo", "mono"), o.optString("eyeOrder", "lr"),
                    o.optString("spatialFormat", ""), o.optString("manifestUrl", ""),
                    o.optString("referenceSpace", "local"), bounds
                )
                items[item.id] = item
            }
        }
    }

    private fun persist() {
        val json = JSONArray()
        items.values.forEach { item ->
            json.put(JSONObject().apply {
                put("id", item.id)
                put("name", item.displayName)
                put("mimeType", item.mimeType)
                put("size", item.size)
                put("contentUri", item.contentUri)
                put("seekable", item.seekable)
                put("shared", item.shared)
                put("projection", item.projection)
                put("fov", item.fov)
                put("stereo", item.stereo)
                put("eyeOrder", item.eyeOrder)
                put("spatialFormat", item.spatialFormat)
                put("manifestUrl", item.manifestUrl)
                put("referenceSpace", item.referenceSpace)
                item.spatialBounds?.let { bounds ->
                    put("spatialBounds", JSONObject().apply {
                        put("centerX", bounds.centerX); put("centerY", bounds.centerY); put("centerZ", bounds.centerZ)
                        put("sizeX", bounds.sizeX); put("sizeY", bounds.sizeY); put("sizeZ", bounds.sizeZ)
                    })
                }
            })
        }
        preferences?.edit()?.putString(KEY_ITEMS, json.toString())?.apply()
    }

    private fun inferSpatialFormat(name: String, mimeType: String?): String {
        val lower = name.lowercase()
        val mime = mimeType?.lowercase().orEmpty()
        return when {
            lower.endsWith(".ply") || mime == "application/ply" || mime == "application/x-ply" -> "ply-splat"
            lower.endsWith(".v3c") -> "v3c"
            lower.endsWith(".6dof") -> "6dof"
            else -> ""
        }
    }

    companion object {
        private const val PREFERENCES = "QuestPhoneStreamMedia"
        private const val KEY_ITEMS = "items"
    }
}

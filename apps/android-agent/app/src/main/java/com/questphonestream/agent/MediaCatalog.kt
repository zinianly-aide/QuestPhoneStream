package com.questphonestream.agent

import android.content.Context
import android.net.Uri
import android.provider.OpenableColumns

class MediaCatalog(context: Context?) {
    private val appContext = context?.applicationContext
    private val repository = MediaShareRepository(context)

    fun addSelectedVideo(uri: Uri, mimeType: String? = null): MediaItem {
        var name: String? = null
        var size: Long? = null
        val ctx = appContext
        if (ctx != null) {
            ctx.contentResolver.query(uri, arrayOf(OpenableColumns.DISPLAY_NAME, OpenableColumns.SIZE), null, null, null)
                ?.use { cursor ->
                    if (cursor.moveToFirst()) {
                        name = cursor.getStringOrNull(0)
                        size = if (cursor.isNull(1)) null else cursor.getLong(1)
                    }
                }
            if (size == null) {
                size = runCatching { ctx.contentResolver.openAssetFileDescriptor(uri, "r")?.use { it.length.takeIf { length -> length >= 0 } } }.getOrNull()
            }
        }
        return repository.add(uri, mimeType, name, size)
    }

    fun remove(id: String): Boolean = repository.remove(id)
    fun setShared(id: String, shared: Boolean): MediaItem? = repository.setShared(id, shared)
    fun setSpatialMetadata(
        id: String,
        spatialFormat: String,
        manifestUrl: String = "",
        referenceSpace: String = "local",
        spatialBounds: SpatialMediaBounds? = null
    ): MediaItem? = repository.setSpatialMetadata(id, spatialFormat, manifestUrl, referenceSpace, spatialBounds)
    fun get(id: String): MediaItem? = repository.get(id)
    fun all(): List<MediaItem> = repository.all()
    fun shared(): List<MediaItem> = repository.shared()

    private fun android.database.Cursor.getStringOrNull(index: Int): String? = if (isNull(index)) null else getString(index)
}

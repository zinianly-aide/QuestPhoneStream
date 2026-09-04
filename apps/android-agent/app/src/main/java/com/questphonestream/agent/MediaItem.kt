package com.questphonestream.agent

import android.net.Uri

/** A user-approved local video. The content URI never leaves the Android app. */
data class MediaItem(
    val id: String,
    val displayName: String,
    val mimeType: String,
    val size: Long,
    val contentUri: String,
    val seekable: Boolean,
    val shared: Boolean
) {
    fun publicMetadata(): Map<String, Any> = mapOf(
        "id" to id,
        "name" to displayName,
        "mimeType" to mimeType,
        "size" to size,
        "seekable" to seekable
    )

    fun uri(): Uri = Uri.parse(contentUri)
}

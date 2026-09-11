package com.questphonestream.agent

import android.net.Uri

/** Bounds are expressed in the declared Spatial reference space, in meters. */
data class SpatialMediaBounds(
    val centerX: Float = 0f,
    val centerY: Float = 0f,
    val centerZ: Float = 0f,
    val sizeX: Float = 0f,
    val sizeY: Float = 0f,
    val sizeZ: Float = 0f
)

/** A user-approved local media resource. The content URI never leaves the Android app. */
data class MediaItem(
    val id: String,
    val displayName: String,
    val mimeType: String,
    val size: Long,
    val contentUri: String,
    val seekable: Boolean,
    val shared: Boolean,
    val projection: String = "flat",
    val fov: Int = 360,
    val stereo: String = "mono",
    val eyeOrder: String = "lr",
    val spatialFormat: String = "",
    val manifestUrl: String = "",
    val referenceSpace: String = "local",
    val spatialBounds: SpatialMediaBounds? = null
) {
    fun publicMetadata(): Map<String, Any> = linkedMapOf<String, Any>(
        "id" to id,
        "name" to displayName,
        "mimeType" to mimeType,
        "size" to size,
        "seekable" to seekable,
        "projection" to projection,
        "fov" to fov,
        "stereo" to stereo,
        "eyeOrder" to eyeOrder,
        "spatialFormat" to spatialFormat,
        "manifestUrl" to manifestUrl,
        "referenceSpace" to referenceSpace
    ).apply {
        spatialBounds?.let { bounds ->
            put("spatialBounds", mapOf(
                "centerX" to bounds.centerX, "centerY" to bounds.centerY, "centerZ" to bounds.centerZ,
                "sizeX" to bounds.sizeX, "sizeY" to bounds.sizeY, "sizeZ" to bounds.sizeZ
            ))
        }
    }

    fun uri(): Uri = Uri.parse(contentUri)
}

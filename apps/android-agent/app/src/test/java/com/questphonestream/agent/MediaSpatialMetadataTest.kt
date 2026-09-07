package com.questphonestream.agent

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class MediaSpatialMetadataTest {
    @Test
    fun spatialMetadataIsIncludedInPublicMetadata() {
        val item = MediaItem(
            id = "media_test",
            displayName = "scene.ply",
            mimeType = "application/x-ply",
            size = 1234,
            contentUri = "content://test/scene.ply",
            seekable = true,
            shared = true,
            spatialFormat = "ply-splat",
            manifestUrl = "manifest/scene.json",
            referenceSpace = "local",
            spatialBounds = SpatialMediaBounds(1f, 2f, 3f, 4f, 5f, 6f)
        )

        val metadata = item.publicMetadata()
        assertEquals("ply-splat", metadata["spatialFormat"])
        assertEquals("manifest/scene.json", metadata["manifestUrl"])
        assertEquals("local", metadata["referenceSpace"])
        val bounds = metadata["spatialBounds"] as Map<*, *>
        assertEquals(1f, bounds["centerX"])
        assertEquals(6f, bounds["sizeZ"])
        assertTrue(metadata.keys.containsAll(listOf("spatialFormat", "manifestUrl", "referenceSpace", "spatialBounds")))
    }
}

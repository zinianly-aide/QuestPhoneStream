package com.questphonestream.agent

import android.net.Uri
import org.junit.Assert.*
import org.junit.Test

class MediaRepositoryTest {
    @Test fun idsAreOpaqueAndRemovalRevokesSharing() {
        val catalog = MediaCatalog(null)
        val item = catalog.addSelectedVideo(Uri.parse("content://test/video"), "video/mp4")
        assertTrue(item.id.startsWith("media_"))
        assertFalse(item.id.contains("content"))
        assertEquals(1, catalog.shared().size)
        assertTrue(catalog.remove(item.id))
        assertNull(catalog.get(item.id))
        assertTrue(catalog.shared().isEmpty())
    }
}

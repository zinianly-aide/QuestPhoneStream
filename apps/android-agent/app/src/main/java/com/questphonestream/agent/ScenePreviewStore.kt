package com.questphonestream.agent

import android.content.Context
import android.net.Uri
import org.json.JSONObject
import java.io.File
import java.io.IOException

/** Generic device-private room previews; never upload personal scans without explicit user action. */
internal object ScenePreviewStore {
    const val SCHEMA = "qps-room-preview-v1"
    private val safeId = Regex("[A-Za-z0-9_-]{1,80}")
    private val shapes = setOf("floor", "ceiling", "wall", "volume", "furniture", "box")

    private fun finiteVec(item: JSONObject, key: String, positive: Boolean = false) {
        val v = item.getJSONObject(key)
        for (axis in arrayOf("x", "y", "z")) {
            val x = v.getDouble(axis)
            require(x.isFinite() && (if (positive) x in 0.01..100.0 else x in -100.0..100.0)) {
                "Invalid $key.$axis"
            }
        }
    }

    fun validate(raw: String): JSONObject {
        require(raw.length <= 1024 * 1024) { "Scene metadata over 1 MB" }
        val doc = JSONObject(raw)
        require(doc.getString("format") == SCHEMA && doc.getInt("schemaVersion") == 1)
        val id = doc.getString("sceneId")
        require(safeId.matches(id)) { "Invalid scene identifier" }
        require(doc.getString("name").length in 1..100) { "Invalid scene name" }
        require(doc.optDouble("metersPerUnit", 0.0) == 1.0) { "Only meter coordinates supported" }
        val nodes = doc.getJSONArray("objects")
        require(nodes.length() in 1..512) { "Room preview must contain 1-512 objects" }
        val ids = HashSet<String>()
        for (i in 0 until nodes.length()) {
            val node = nodes.getJSONObject(i)
            val key = node.getString("id")
            require(safeId.matches(key) && ids.add(key)) { "Invalid/duplicate object identifier" }
            require(node.getString("kind") in shapes) { "Unsupported preview geometry" }
            finiteVec(node, "position")
            finiteVec(node, "size", positive = true)
            if (node.has("rotation")) {
                val q = node.getJSONObject("rotation")
                var norm = 0.0
                for (axis in listOf("x", "y", "z", "w")) {
                    val x = q.getDouble(axis)
                    require(x.isFinite() && x in -1.0..1.0)
                    norm += x * x
                }
                require(norm in 0.9..1.1) { "Invalid room orientation" }
            }
        }
        return doc
    }

    fun importDocument(context: Context, uri: Uri): String {
        // Explicit Android document picker, not broad media/scene storage permissions.
        val bytes = context.contentResolver.openInputStream(uri)?.use { input ->
            // Read at most 1 MiB + sentinel, never buffer arbitrary user files.
            val bounded = ByteArray(1024 * 1024 + 1)
            var pos = 0
            while (pos < bounded.size) {
                val n = input.read(bounded, pos, bounded.size - pos)
                if (n < 0) break
                if (n == 0) break
                pos += n
            }
            require(pos in 1..(1024 * 1024)) { "Room metadata must be <= 1 MiB" }
            bounded.copyOf(pos)
        } ?: throw IOException("Selected document unavailable")
        return importJson(context, bytes.toString(Charsets.UTF_8))
    }

    fun importJson(context: Context, raw: String): String {
        val doc = validate(raw)
        val id = doc.getString("sceneId")
        val dir = File(context.getExternalFilesDir(null) ?: context.filesDir, "ScenePreviews")
        require(dir.isDirectory || dir.mkdirs())
        val target = File(dir, "$id.json")
        val temp = File(dir, "$id.json.tmp")
        temp.writeText(raw, Charsets.UTF_8)
        if (target.isFile) {
            val backup = File(dir, "$id.json.bak")
            if (!target.copyTo(backup, overwrite = true).isFile) throw IOException("Backup failed")
            if (!target.delete()) throw IOException("Cannot replace existing preview")
        }
        if (!temp.renameTo(target)) {
            temp.delete()
            throw IOException("Could not save imported scene")
        }
        return id
    }

    fun entries(context: Context): List<String> {
        val dir = File(context.getExternalFilesDir(null) ?: context.filesDir, "ScenePreviews")
        return dir.listFiles()?.asSequence()?.filter { it.isFile && it.name.endsWith(".json") &&
            safeId.matches(it.name.removeSuffix(".json")) }?.map { it.nameWithoutExtension }?.sorted()?.toList()
            ?: emptyList()
    }
}

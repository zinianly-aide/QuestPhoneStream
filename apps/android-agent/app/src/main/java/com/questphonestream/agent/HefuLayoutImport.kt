package com.questphonestream.agent

import android.content.Context
import android.net.Uri
import org.json.JSONObject
import java.io.File
import java.io.IOException

/** Explicit one-time migration from HefuVR's separate Android package. */
internal object HefuLayoutImport {
    const val NAME = "hefu-layout-v3.json"
    fun validate(data: String) {
        require(data.length in 10..(256 * 1024)) { "Unexpected layout file size" }
        val root = JSONObject(data)
        require(root.getInt("schemaVersion") == 3)
        require(root.getString("modelVersion") == "cad-v0.2-sideboard-drawing-v1")
        require(root.getString("unit") == "meter")
        val entries = root.getJSONArray("furniture")
        require(entries.length() in 1..256)
        val seen = HashSet<String>()
        for (i in 0 until entries.length()) {
            val item = entries.getJSONObject(i)
            val id = item.getString("id")
            require(id.length in 1..96 && seen.add(id))
            for (key in listOf("position", "rotation", "localScale")) {
                val vec = item.getJSONObject(key)
                for (axis in if (key == "rotation") listOf("x", "y", "z", "w") else listOf("x", "y", "z")) {
                    val value = vec.getDouble(axis)
                    require(value.isFinite() && value in -100.0..100.0)
                    if (key == "localScale") require(value in 0.01..5.0)
                }
            }
        }
    }
    fun importDocument(context: Context, source: Uri) {
        val bytes = context.contentResolver.openInputStream(source)?.use { input ->
            val buffer = ByteArray(256 * 1024 + 1)
            var count = 0
            while (count < buffer.size) {
                val n = input.read(buffer, count, buffer.size - count)
                if (n <= 0) break
                count += n
            }
            require(count in 1..(256 * 1024))
            buffer.copyOf(count)
        } ?: throw IOException("Hefu document not readable")
        validate(bytes.toString(Charsets.UTF_8))
        val dir = context.getExternalFilesDir(null) ?: context.filesDir
        val dest = File(dir, NAME)
        val tmp = File(dir, "$NAME.import-tmp")
        val backup = File(dir, "$NAME.pre-import")
        tmp.writeBytes(bytes)
        if (dest.isFile && !dest.copyTo(backup, overwrite = true).isFile)
            throw IOException("Could not back up prior home layout")
        if (!tmp.renameTo(dest)) {
            tmp.delete()
            throw IOException("Could not complete home layout import")
        }
    }
}

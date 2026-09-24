package com.questphonestream.agent

data class DisplayGeometry(
    val displayWidth: Int,
    val displayHeight: Int,
    val captureWidth: Int,
    val captureHeight: Int
) {
    val isLandscape: Boolean get() = displayWidth >= displayHeight
}

object DisplayGeometryCalculator {
    /**
     * Fit the real display into an encoder-friendly even-sized frame while preserving
     * the display aspect ratio. The configured stream size is treated as a maximum
     * capture edge rather than a fixed canvas, avoiding letterboxing on tall phones.
     */
    fun fit(displayWidth: Int, displayHeight: Int, maxCaptureEdge: Int): DisplayGeometry {
        require(displayWidth > 0 && displayHeight > 0) { "display dimensions must be positive" }

        val limit = even(maxCaptureEdge.coerceAtLeast(2))
        val captureWidth: Int
        val captureHeight: Int

        if (displayWidth >= displayHeight) {
            captureWidth = even(minOf(displayWidth, limit))
            captureHeight = even(
                (displayHeight.toDouble() * captureWidth / displayWidth).toInt().coerceAtLeast(2)
            )
        } else {
            captureHeight = even(minOf(displayHeight, limit))
            captureWidth = even(
                (displayWidth.toDouble() * captureHeight / displayHeight).toInt().coerceAtLeast(2)
            )
        }

        return DisplayGeometry(displayWidth, displayHeight, captureWidth, captureHeight)
    }

    private fun even(value: Int): Int {
        val positive = value.coerceAtLeast(2)
        return if (positive % 2 == 0) positive else positive - 1
    }
}

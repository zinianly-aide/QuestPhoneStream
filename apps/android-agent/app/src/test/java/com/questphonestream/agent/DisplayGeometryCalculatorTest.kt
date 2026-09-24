package com.questphonestream.agent

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class DisplayGeometryCalculatorTest {
    @Test
    fun tallPortraitUsesDisplayAspectInsteadOfFixedNineBySixteenCanvas() {
        val geometry = DisplayGeometryCalculator.fit(1080, 2412, 1280)

        assertEquals(572, geometry.captureWidth)
        assertEquals(1280, geometry.captureHeight)
        assertTrue(!geometry.isLandscape)
        assertAspectClose(1080, 2412, geometry.captureWidth, geometry.captureHeight)
    }

    @Test
    fun rotationSwapsCaptureOrientationWithoutChangingLongEdgeBudget() {
        val portrait = DisplayGeometryCalculator.fit(1080, 2412, 1280)
        val landscape = DisplayGeometryCalculator.fit(2412, 1080, 1280)

        assertEquals(portrait.captureHeight, landscape.captureWidth)
        assertEquals(portrait.captureWidth, landscape.captureHeight)
        assertTrue(landscape.isLandscape)
    }

    @Test
    fun standardSixteenByNineStillMapsToExpectedEncoderSize() {
        val geometry = DisplayGeometryCalculator.fit(1920, 1080, 1280)

        assertEquals(1280, geometry.captureWidth)
        assertEquals(720, geometry.captureHeight)
    }

    @Test
    fun outputDimensionsAreAlwaysEvenForCodecCompatibility() {
        val geometry = DisplayGeometryCalculator.fit(1179, 2556, 1281)

        assertEquals(0, geometry.captureWidth % 2)
        assertEquals(0, geometry.captureHeight % 2)
        assertTrue(maxOf(geometry.captureWidth, geometry.captureHeight) <= 1280)
    }

    private fun assertAspectClose(
        displayWidth: Int,
        displayHeight: Int,
        captureWidth: Int,
        captureHeight: Int
    ) {
        val displayAspect = displayWidth.toDouble() / displayHeight
        val captureAspect = captureWidth.toDouble() / captureHeight
        assertTrue(kotlin.math.abs(displayAspect - captureAspect) < 0.002)
    }
}

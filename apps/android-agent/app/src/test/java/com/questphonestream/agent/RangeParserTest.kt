package com.questphonestream.agent

import org.junit.Assert.*
import org.junit.Test

class RangeParserTest {
    @Test fun openEndedStartsAtZero() = assertEquals(ByteRange(0, 999, 1000), RangeParser.parse("bytes=0-", 1000))
    @Test fun openEndedMiddle() = assertEquals(ByteRange(100, 999, 1000), RangeParser.parse("bytes=100-", 1000))
    @Test fun boundedRange() = assertEquals(ByteRange(100, 199, 1000), RangeParser.parse("bytes=100-199", 1000))
    @Test fun endIsClampedToFile() = assertEquals(ByteRange(900, 999, 1000), RangeParser.parse("bytes=900-2000", 1000))
    @Test fun suffixRange() = assertEquals(ByteRange(900, 999, 1000), RangeParser.parse("bytes=-100", 1000))
    @Test fun malformedAndOutOfBoundsAreRejected() {
        assertNull(RangeParser.parse("bytes=abc", 1000))
        assertNull(RangeParser.parse("bytes=1000-", 1000))
        assertNull(RangeParser.parse("bytes=200-100", 1000))
        assertNull(RangeParser.parse("bytes=0-1,4-5", 1000))
    }
}

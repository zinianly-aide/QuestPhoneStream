package com.questphonestream.agent

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class DeferredDisposalQueueTest {
    @Test
    fun finalReleaseWaitsForScheduledPeerDisposal() {
        val scheduled = mutableListOf<() -> Unit>()
        val disposed = mutableListOf<String>()
        var finalReleases = 0
        val queue = DeferredDisposalQueue(
            delayMillis = 250,
            schedule = { delay, action ->
                assertEquals(250L, delay)
                scheduled += action
            },
            dispose = disposed::add,
            onDrained = { finalReleases++ }
        )

        queue.defer("peer-1")
        queue.finishWhenDrained()

        assertTrue(disposed.isEmpty())
        assertEquals(0, finalReleases)
        scheduled.single().invoke()
        assertEquals(listOf("peer-1"), disposed)
        assertEquals(1, finalReleases)
    }

    @Test
    fun duplicatePeerIsScheduledAndDisposedOnce() {
        val scheduled = mutableListOf<() -> Unit>()
        val disposed = mutableListOf<String>()
        val queue = DeferredDisposalQueue(
            delayMillis = 250,
            schedule = { _, action -> scheduled += action },
            dispose = disposed::add,
            onDrained = {}
        )

        queue.defer("peer-1")
        queue.defer("peer-1")

        assertEquals(1, scheduled.size)
        scheduled.single().invoke()
        assertEquals(listOf("peer-1"), disposed)
    }

    @Test
    fun finalReleaseRunsImmediatelyForAnEmptyQueue() {
        var finalReleases = 0
        val queue = DeferredDisposalQueue<String>(
            delayMillis = 250,
            schedule = { _, _ -> error("Nothing should be scheduled") },
            dispose = { error("Nothing should be disposed") },
            onDrained = { finalReleases++ }
        )

        queue.finishWhenDrained()
        queue.finishWhenDrained()

        assertEquals(1, finalReleases)
    }
}

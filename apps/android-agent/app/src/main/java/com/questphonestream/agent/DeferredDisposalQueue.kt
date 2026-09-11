package com.questphonestream.agent

/**
 * Defers destruction of callback-owning native objects and runs a final release
 * exactly once after every deferred object has drained. All calls are expected
 * on the same (main) thread as [schedule].
 */
internal class DeferredDisposalQueue<T>(
    private val delayMillis: Long,
    private val schedule: (Long, () -> Unit) -> Unit,
    private val dispose: (T) -> Unit,
    private val onDrained: () -> Unit
) {
    private val pending = linkedSetOf<T>()
    private var drainRequested = false
    private var drained = false

    fun defer(value: T?) {
        if (value == null || drained || !pending.add(value)) return
        schedule(delayMillis) scheduled@{
            if (!pending.remove(value)) return@scheduled
            try {
                dispose(value)
            } finally {
                drainIfReady()
            }
        }
    }

    fun finishWhenDrained() {
        if (drained) return
        drainRequested = true
        drainIfReady()
    }

    private fun drainIfReady() {
        if (!drainRequested || drained || pending.isNotEmpty()) return
        drained = true
        onDrained()
    }
}

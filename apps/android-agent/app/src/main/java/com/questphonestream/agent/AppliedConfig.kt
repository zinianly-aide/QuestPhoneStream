package com.questphonestream.agent

/**
 * Endpoint identity that is allowed to affect the process-wide signaling/media control plane.
 * UI draft values never enter this store until the user explicitly saves/applies them.
 */
data class AppliedConfig(
    val signalingUrl: String,
    val token: String,
    val deviceId: String
) {
    fun mergeInto(config: StreamConfig): StreamConfig = config.copy(
        signalingUrl = signalingUrl,
        token = token,
        deviceId = deviceId
    )
}

internal object AppliedConfigStore {
    @Volatile private var applied: AppliedConfig? = null

    @Synchronized
    fun initializeIfAbsent(signalingUrl: String, token: String, deviceId: String): AppliedConfig {
        applied?.let { return it }
        return make(signalingUrl, token, deviceId).also { applied = it }
    }

    @Synchronized
    fun apply(config: StreamConfig): AppliedConfig =
        apply(config.signalingUrl, config.token, config.deviceId)

    @Synchronized
    fun apply(signalingUrl: String, token: String, deviceId: String): AppliedConfig =
        make(signalingUrl, token, deviceId).also { applied = it }

    @Synchronized
    fun current(): AppliedConfig? = applied

    @Synchronized
    fun merge(config: StreamConfig): StreamConfig {
        val current = applied ?: return config
        return current.mergeInto(config)
    }

    @Synchronized
    internal fun resetForTests() {
        applied = null
    }

    private fun make(signalingUrl: String, token: String, deviceId: String) = AppliedConfig(
        signalingUrl = signalingUrl.trim(),
        token = token,
        deviceId = deviceId.trim()
    )
}

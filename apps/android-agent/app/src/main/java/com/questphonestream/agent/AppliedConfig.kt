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
        return AppliedConfig(signalingUrl.trim(), token, deviceId.trim()).also { applied = it }
    }

    @Synchronized
    fun apply(config: StreamConfig): AppliedConfig = AppliedConfig(
        signalingUrl = config.signalingUrl.trim(),
        token = config.token,
        deviceId = config.deviceId.trim()
    ).also { applied = it }

    @Synchronized
    fun current(): AppliedConfig? = applied

    @Synchronized
    fun merge(config: StreamConfig): StreamConfig = (applied ?: return config).mergeInto(config)

    @Synchronized
    internal fun resetForTests() {
        applied = null
    }
}

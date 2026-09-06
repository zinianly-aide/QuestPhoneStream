package com.questphonestream.agent

/**
 * Compatibility facade for older UI call sites. Spatial/signaling ownership lives in
 * DeviceControlPlane; this object only commits an explicit Save/Apply into AppliedConfig.
 */
internal object AndroidSpatialControlPlane {
    @Synchronized
    fun start(requested: StreamConfig) {
        val applied = AppliedConfigStore.apply(requested)
        DeviceControlPlane.configure(applied.signalingUrl, applied.token, applied.deviceId)
    }

    /** Legacy no-op hooks retained so older callers compile while the process-wide
     * DeviceControlPlane owns the actual WebRTC publisher lifecycle. */
    fun attach(publisher: WebRtcStreamer) = Unit
    fun detach(publisher: WebRtcStreamer) = Unit
}

package com.questphonestream.agent

/**
 * Compatibility facade for the legacy Save call site. The actual signaling reconfigure
 * is performed once by MediaHttpServer.refreshNsdMetadata() after AppliedConfig is
 * committed, so editing fields never changes the live control plane.
 */
internal object AndroidSpatialControlPlane {
    @Synchronized
    fun start(requested: StreamConfig) {
        AppliedConfigStore.apply(requested)
    }

    /** Legacy no-op hooks retained so older callers compile while the process-wide
     * DeviceControlPlane owns the actual WebRTC publisher lifecycle. */
    fun attach(publisher: WebRtcStreamer) = Unit
    fun detach(publisher: WebRtcStreamer) = Unit
}

package com.questphonestream.agent

import org.json.JSONArray
import org.json.JSONObject
import java.util.UUID

internal object SpatialProtocol {
    const val VERSION = "1.0"
    val MESSAGE_TYPES = setOf(
        "device.hello",
        "device.capabilities.get",
        "device.capabilities.result",
        "device.capabilities.changed",
        "subscription.create",
        "subscription.created",
        "subscription.cancel",
        "subscription.closed",
        "protocol.error"
    )

    fun isSpatialType(type: String): Boolean = type in MESSAGE_TYPES

    fun validateEnvelope(message: JSONObject): String? {
        if (message.optString("v") != VERSION) return "unsupported_spatial_version"
        if (!isSpatialType(message.optString("type"))) return "unknown_spatial_type"
        for (key in listOf("id", "source", "target", "sessionId", "streamId", "correlationId")) {
            if (!message.has(key) || message.opt(key) !is String) return "invalid_$key"
        }
        if (message.optString("id").isEmpty() || message.optString("source").isEmpty() || message.optString("target").isEmpty())
            return "invalid_spatial_identity"
        if (!message.has("timestamp") || message.opt("timestamp") !is Number) return "invalid_timestamp"
        if (message.optJSONObject("payload") == null) return "invalid_payload"
        return null
    }

    fun envelope(
        type: String,
        source: String,
        target: String,
        payload: JSONObject = JSONObject(),
        sessionId: String = "",
        streamId: String = "",
        correlationId: String = ""
    ): JSONObject = JSONObject()
        .put("v", VERSION)
        .put("id", UUID.randomUUID().toString())
        .put("type", type)
        .put("source", source)
        .put("target", target)
        .put("sessionId", sessionId)
        .put("streamId", streamId)
        .put("correlationId", correlationId)
        .put("timestamp", System.currentTimeMillis())
        .put("payload", payload)

    fun helloPayload(deviceId: String, selectedVersion: String? = null): JSONObject = JSONObject().apply {
        if (selectedVersion == null) put("supportedVersions", JSONArray().put(VERSION))
        else put("selectedVersion", selectedVersion)
        put("device", JSONObject()
            .put("id", deviceId)
            .put("name", deviceId)
            .put("protocolVersions", JSONArray().put(VERSION)))
    }

    fun capabilitiesPayload(capabilities: List<CapabilityDescriptor>): JSONObject = JSONObject()
        .put("capabilities", JSONArray().apply { capabilities.forEach { put(capabilityToJson(it)) } })

    fun negotiateVersion(payload: JSONObject): String? {
        val selected = payload.optString("selectedVersion")
        if (selected.isNotEmpty()) return if (selected == VERSION) VERSION else null
        val offered = payload.optJSONArray("supportedVersions") ?: return null
        for (index in 0 until offered.length()) if (offered.optString(index) == VERSION) return VERSION
        return null
    }

    fun errorPayload(code: String, message: String, retryable: Boolean = false): JSONObject = JSONObject()
        .put("code", code)
        .put("message", message)
        .put("retryable", retryable)

    private fun capabilityToJson(capability: CapabilityDescriptor): JSONObject = JSONObject()
        .put("name", capability.name)
        .put("version", capability.version)
        .put("state", JSONObject()
            .put("available", capability.state.available)
            .put("authorized", capability.state.authorized)
            .put("active", capability.state.active))
        .put("transports", JSONArray(capability.transports))
        .put("features", JSONArray(capability.features))
        .put("limits", JSONArray().apply {
            capability.limits.forEach { put(JSONObject().put("name", it.name).put("value", it.value)) }
        })
        .put("permissions", JSONArray(capability.permissions))
}

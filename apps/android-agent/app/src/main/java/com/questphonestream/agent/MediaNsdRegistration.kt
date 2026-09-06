package com.questphonestream.agent

import android.content.Context
import android.net.wifi.WifiManager
import android.net.nsd.NsdManager
import android.net.nsd.NsdServiceInfo
import android.os.Build
import android.os.Handler
import android.os.Looper
import android.util.Log

/** Registers the currently running media HTTP server on the local network. */
internal class MediaNsdRegistration(
    context: Context,
    private val portProvider: () -> Int,
    private val streamIdProvider: () -> String,
    private val signalingEndpointProvider: () -> String
) {
    private val nsdManager = context.getSystemService(Context.NSD_SERVICE) as NsdManager
    private val wifiManager = context.applicationContext.getSystemService(Context.WIFI_SERVICE) as? WifiManager
    private val deviceId = MediaDeviceIdentity.getOrCreateDeviceId(context)
    private val deviceName = MediaDeviceIdentity.displayName()
    private val advertisements = listOf(
        Advertisement(UNIFIED_SERVICE_TYPE, "media,screen,control"),
        Advertisement(LEGACY_SERVICE_TYPE, "media")
    )
    private var started = false
    private val registeredTypes = mutableSetOf<String>()
    private val pendingTypes = mutableSetOf<String>()
    private val retryAttempts = mutableMapOf<String, Int>()
    private val retryRunnables = mutableMapOf<String, Runnable>()
    private val mainHandler = Handler(Looper.getMainLooper())
    private var multicastLock: WifiManager.MulticastLock? = null
    private var multicastLockHeld = false
    private val registrationListeners = mutableMapOf<String, NsdManager.RegistrationListener>()

    fun start() {
        if (started) return
        started = true
        acquireMulticastLock()
        advertisements.forEach(::registerAdvertisement)
    }

    fun stop() {
        if (!started && registeredTypes.isEmpty() && multicastLock == null) return
        started = false
        try {
            retryRunnables.values.forEach(mainHandler::removeCallbacks)
            retryRunnables.clear()
            retryAttempts.clear()
            pendingTypes.clear()
            unregisterRegisteredServices()
        } catch (error: Exception) {
            Log.w(TAG, "Media NSD stop failed: ${error.javaClass.simpleName}: ${error.message}")
        } finally {
            registeredTypes.clear()
            releaseMulticastLock()
        }
    }

    private fun registerAdvertisement(advertisement: Advertisement) {
        if (!started || registeredTypes.contains(advertisement.type) || pendingTypes.contains(advertisement.type)) return
        try {
            val serviceInfo = NsdServiceInfo().apply {
                serviceName = "QuestPhoneStream"
                serviceType = advertisement.type
                port = portProvider()
                setAttribute("v", "1")
                setAttribute("id", deviceId)
                setAttribute("name", deviceName)
                setAttribute("caps", advertisement.capabilities)
                setAttribute("capv", "1")
                setAttribute("spatial", "1")
                if (advertisement.type == UNIFIED_SERVICE_TYPE) {
                    setAttribute("streamId", streamIdProvider().ifBlank { deviceId })
                    setAttribute("signalingUrl", signalingEndpointProvider())
                }
            }
            val listener = createRegistrationListener(advertisement.type)
            registrationListeners[advertisement.type] = listener
            pendingTypes += advertisement.type
            nsdManager.registerService(serviceInfo, NsdManager.PROTOCOL_DNS_SD, listener)
        } catch (error: Exception) {
            pendingTypes -= advertisement.type
            registrationListeners.remove(advertisement.type)
            scheduleRetry(advertisement.type, "sync exception: ${error.javaClass.simpleName}")
            Log.e(TAG, "Media NSD registration dispatch failed type=${advertisement.type}: ${error.message}")
        }
    }

    private fun scheduleRetry(type: String, reason: String) {
        if (!started || registeredTypes.contains(type) || retryRunnables.containsKey(type)) return
        val attempt = (retryAttempts[type] ?: 0) + 1
        retryAttempts[type] = attempt
        if (attempt > MAX_RETRY_ATTEMPTS) {
            Log.e(TAG, "Media NSD registration retry exhausted type=$type reason=$reason")
            return
        }
        val advertisement = advertisements.firstOrNull { it.type == type } ?: return
        val delayMs = RETRY_DELAY_MS * attempt
        val retry = Runnable {
            retryRunnables.remove(type)
            registerAdvertisement(advertisement)
        }
        retryRunnables[type] = retry
        mainHandler.postDelayed(retry, delayMs)
        Log.w(TAG, "Media NSD registration retry scheduled type=$type attempt=$attempt delayMs=$delayMs reason=$reason")
    }

    private fun createRegistrationListener(type: String) = object : NsdManager.RegistrationListener {
        override fun onServiceRegistered(serviceInfo: NsdServiceInfo) {
            if (!started) {
                runCatching { nsdManager.unregisterService(this) }
                return
            }
            pendingTypes -= type
            registeredTypes += type
            retryAttempts.remove(type)
            Log.i(TAG, "Media NSD registered name=${serviceInfo.serviceName} type=${serviceInfo.serviceType} port=${serviceInfo.port}")
        }

        override fun onRegistrationFailed(serviceInfo: NsdServiceInfo, errorCode: Int) {
            pendingTypes -= type
            registeredTypes -= type
            Log.e(TAG, "Media NSD registration failed type=$type error=$errorCode")
            scheduleRetry(type, "callback error=$errorCode")
        }

        override fun onServiceUnregistered(serviceInfo: NsdServiceInfo) {
            registeredTypes -= type
            if (!started && registeredTypes.isEmpty()) releaseMulticastLock()
            Log.i(TAG, "Media NSD unregistered name=${serviceInfo.serviceName} type=$type")
        }

        override fun onUnregistrationFailed(serviceInfo: NsdServiceInfo, errorCode: Int) {
            registeredTypes -= type
            if (!started && registeredTypes.isEmpty()) releaseMulticastLock()
            Log.w(TAG, "Media NSD unregistration failed type=$type error=$errorCode")
        }
    }

    private fun unregisterRegisteredServices() {
        registeredTypes.toList().forEach { type ->
            registrationListeners.remove(type)?.let { listener ->
                runCatching { nsdManager.unregisterService(listener) }
            }
        }
    }

    private fun acquireMulticastLock() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S || multicastLock != null) return
        try {
            val lock = wifiManager?.createMulticastLock("QuestPhoneStreamNSD") ?: return
            lock.setReferenceCounted(false)
            multicastLock = lock
            lock.acquire()
            multicastLockHeld = true
        } catch (error: Exception) {
            Log.w(TAG, "Media NSD multicast lock acquire failed: ${error.javaClass.simpleName}: ${error.message}")
            releaseMulticastLock()
        }
    }

    private fun releaseMulticastLock() {
        val lock = multicastLock ?: return
        try {
            if (multicastLockHeld) lock.release()
        } catch (error: Exception) {
            Log.w(TAG, "Media NSD multicast lock release failed: ${error.javaClass.simpleName}: ${error.message}")
        } finally {
            multicastLockHeld = false
            multicastLock = null
        }
    }

    companion object {
        const val UNIFIED_SERVICE_TYPE = "_qps-device._tcp."
        const val LEGACY_SERVICE_TYPE = "_qps-media._tcp."
        const val SERVICE_TYPE = LEGACY_SERVICE_TYPE
        private const val MAX_RETRY_ATTEMPTS = 3
        private const val RETRY_DELAY_MS = 500L
        private const val TAG = "QuestPhoneStreamNSD"
    }

    private data class Advertisement(val type: String, val capabilities: String)
}

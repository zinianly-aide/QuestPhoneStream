package com.questphonestream.agent

import android.content.Context
import android.net.wifi.WifiManager
import android.net.nsd.NsdManager
import android.net.nsd.NsdServiceInfo
import android.os.Build
import android.util.Log

/** Registers the currently running media HTTP server on the local network. */
internal class MediaNsdRegistration(
    context: Context,
    private val portProvider: () -> Int
) {
    private val nsdManager = context.getSystemService(Context.NSD_SERVICE) as NsdManager
    private val wifiManager = context.applicationContext.getSystemService(Context.WIFI_SERVICE) as? WifiManager
    private val deviceId = MediaDeviceIdentity.getOrCreateDeviceId(context)
    private val deviceName = MediaDeviceIdentity.displayName()
    private var started = false
    private var registered = false
    private var multicastLock: WifiManager.MulticastLock? = null
    private var multicastLockHeld = false

    private val registrationListener = object : NsdManager.RegistrationListener {
        override fun onServiceRegistered(serviceInfo: NsdServiceInfo) {
            if (!started) {
                runCatching { nsdManager.unregisterService(this) }
                return
            }
            registered = true
            Log.i(TAG, "Media NSD registered name=${serviceInfo.serviceName} type=${serviceInfo.serviceType} port=${serviceInfo.port}")
        }

        override fun onRegistrationFailed(serviceInfo: NsdServiceInfo, errorCode: Int) {
            registered = false
            started = false
            releaseMulticastLock()
            Log.e(TAG, "Media NSD registration failed error=$errorCode")
        }

        override fun onServiceUnregistered(serviceInfo: NsdServiceInfo) {
            registered = false
            started = false
            releaseMulticastLock()
            Log.i(TAG, "Media NSD unregistered name=${serviceInfo.serviceName}")
        }

        override fun onUnregistrationFailed(serviceInfo: NsdServiceInfo, errorCode: Int) {
            registered = false
            started = false
            releaseMulticastLock()
            Log.w(TAG, "Media NSD unregistration failed error=$errorCode")
        }
    }

    fun start() {
        if (started) return
        started = true
        acquireMulticastLock()
        runCatching {
            val serviceInfo = NsdServiceInfo().apply {
                serviceName = "QuestPhoneStream"
                serviceType = SERVICE_TYPE
                port = portProvider()
                setAttribute("v", "1")
                setAttribute("id", deviceId)
                setAttribute("name", deviceName)
                setAttribute("caps", "media")
            }
            nsdManager.registerService(serviceInfo, NsdManager.PROTOCOL_DNS_SD, registrationListener)
        }.onFailure {
            started = false
            registered = false
            releaseMulticastLock()
            Log.e(TAG, "Media NSD start failed: ${it.javaClass.simpleName}: ${it.message}")
        }
    }

    fun stop() {
        if (!started && !registered && multicastLock == null) return
        started = false
        try {
            if (registered) nsdManager.unregisterService(registrationListener)
        } catch (error: Exception) {
            Log.w(TAG, "Media NSD stop failed: ${error.javaClass.simpleName}: ${error.message}")
        } finally {
            registered = false
            releaseMulticastLock()
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
        const val SERVICE_TYPE = "_qps-media._tcp."
        private const val TAG = "QuestPhoneStreamNSD"
    }
}

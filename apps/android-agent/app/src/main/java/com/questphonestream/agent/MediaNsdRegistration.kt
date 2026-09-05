package com.questphonestream.agent

import android.content.Context
import android.net.nsd.NsdManager
import android.net.nsd.NsdServiceInfo
import android.util.Log

/** Registers the currently running media HTTP server on the local network. */
internal class MediaNsdRegistration(
    context: Context,
    private val portProvider: () -> Int
) {
    private val nsdManager = context.getSystemService(Context.NSD_SERVICE) as NsdManager
    private val deviceId = MediaDeviceIdentity.getOrCreateDeviceId(context)
    private val deviceName = MediaDeviceIdentity.displayName()
    private var started = false
    private var registered = false

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
            Log.e(TAG, "Media NSD registration failed error=$errorCode")
        }

        override fun onServiceUnregistered(serviceInfo: NsdServiceInfo) {
            registered = false
            Log.i(TAG, "Media NSD unregistered name=${serviceInfo.serviceName}")
        }

        override fun onUnregistrationFailed(serviceInfo: NsdServiceInfo, errorCode: Int) {
            registered = false
            Log.w(TAG, "Media NSD unregistration failed error=$errorCode")
        }
    }

    fun start() {
        if (started) return
        started = true
        val serviceInfo = NsdServiceInfo().apply {
            serviceName = "QuestPhoneStream"
            serviceType = SERVICE_TYPE
            port = portProvider()
            setAttribute("v", "1")
            setAttribute("id", deviceId)
            setAttribute("name", deviceName)
            setAttribute("caps", "media")
        }
        runCatching {
            nsdManager.registerService(serviceInfo, NsdManager.PROTOCOL_DNS_SD, registrationListener)
        }.onFailure {
            started = false
            Log.e(TAG, "Media NSD start failed: ${it.javaClass.simpleName}: ${it.message}")
        }
    }

    fun stop() {
        if (!started && !registered) return
        started = false
        if (registered) {
            runCatching { nsdManager.unregisterService(registrationListener) }
        }
        registered = false
    }

    companion object {
        const val SERVICE_TYPE = "_qps-media._tcp."
        private const val TAG = "QuestPhoneStreamNSD"
    }
}

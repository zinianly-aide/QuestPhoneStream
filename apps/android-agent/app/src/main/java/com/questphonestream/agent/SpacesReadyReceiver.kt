package com.questphonestream.agent

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.util.Log

/** Internal-only Unity :spaces -> sender process readiness handshake. */
class SpacesReadyReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) {
        if (intent.action != "com.questphonestream.agent.SPACES_READY") return
        // Receiver runs in the main app process, preserving SharedPreferences
        // consistency instead of reading stale caches across separate processes.
        context.getSharedPreferences("qps_spaces_launch", Context.MODE_PRIVATE)
            .edit().putBoolean("pending", false).apply()
        Log.i("QPS3D", "Unity Hefu scene ready; handoff complete")
    }
}

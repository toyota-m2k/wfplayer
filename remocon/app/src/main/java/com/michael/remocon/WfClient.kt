package com.michael.remocon

import android.content.Context
import android.util.Log
import androidx.preference.PreferenceManager
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import okhttp3.*
import java.io.IOException
import java.lang.ref.WeakReference
import kotlin.coroutines.resume
import kotlin.coroutines.resumeWithException
import kotlin.coroutines.suspendCoroutine

class WfClient() {
    private val httpClient = OkHttpClient()

    private var mContext: WeakReference<Context>? = null
    var context : Context
        get() = mContext?.get()!!
        private set(value) {
            mContext = WeakReference(value)
        }
    fun attach(activity: MainActivity) {
        context = activity
    }
    /**
     * coroutineを利用し、スレッドをブロックしないで同期的な通信を可能にする拡張メソッド
     * OkHttpのnewCall().execute()を置き換えるだけで使える。
     */
    private suspend fun Call.executeAsync() : Response {
        return suspendCoroutine {cont ->
            try {
                enqueue(object : Callback {
                    override fun onFailure(call: Call, e: IOException) {
                        cont.resumeWithException(e)
                    }

                    override fun onResponse(call: Call, response: Response) {
                        cont.resume(response)
                    }
                })
            } catch(e:Throwable) {
                cont.resumeWithException(e)
            }
        }
    }

    private val serverAddress:String
        get() {
            val key = context.getString(R.string.key_server_address)
            return PreferenceManager.getDefaultSharedPreferences(context).getString(key, null) ?: "192.168.0.152"
        }


    private suspend fun sendCommand(command:String) : Boolean {
        return try {
            val req = Request.Builder()
                .url("http://$serverAddress/wfplayer/cmd/$command")
                .build()
            httpClient.newCall(req)
                .executeAsync()
                .close()
            true
        } catch(e:Throwable) {
            Log.e("WfClient", e.message ?: "error");
            false
        }
    }

    fun postCommand(command:String) {
        CoroutineScope(Dispatchers.Default).launch {
            sendCommand(command)
        }
    }
}
package com.michael.wfpremocon

import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import okhttp3.*
import java.io.IOException
import kotlin.coroutines.resume
import kotlin.coroutines.resumeWithException
import kotlin.coroutines.suspendCoroutine

class WfClient {
    val httpClient = OkHttpClient()

    /**
     * Coroutineを利用し、スレッドをブロックしないで同期的な通信を可能にする拡張メソッド
     * OkHttpのnewCall().execute()を置き換えるだけで使える。
     */
    suspend fun Call.executeAsync() : Response {
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

    suspend fun sendCommand(command:String) : Response? {
        try {
            val req = Request.Builder()
                .url("http://192.168.0.5/wfplayer/cmd/$command")
                .build()
            val res = httpClient.newCall(req)
                .executeAsync()
            return res
        } catch(e:Throwable) {
            return null;
        }
    }

    fun postCommand(command:String) {
        CoroutineScope(Dispatchers.Default).launch {
            sendCommand(command)
        }
    }
}
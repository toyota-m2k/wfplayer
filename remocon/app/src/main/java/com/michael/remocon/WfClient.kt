package com.michael.remocon

import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import okhttp3.*
import java.io.IOException
import kotlin.coroutines.resume
import kotlin.coroutines.resumeWithException
import kotlin.coroutines.suspendCoroutine

class WfClient {
    private val httpClient = OkHttpClient()

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

    private suspend fun sendCommand(command:String) : Response? {
        return try {
            val req = Request.Builder()
                .url("http://192.168.0.6/wfplayer/cmd/$command")
                .build()
            val res = httpClient.newCall(req)
                .executeAsync()
            res
        } catch(e:Throwable) {
            null
        }
    }

    fun postCommand(command:String) {
        CoroutineScope(Dispatchers.Default).launch {
            sendCommand(command)
        }
    }
}
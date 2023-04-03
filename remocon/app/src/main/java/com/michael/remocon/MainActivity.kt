package com.michael.remocon

import android.content.Intent
import android.content.pm.PackageManager
import android.os.Bundle
import android.util.Log
import android.view.Menu
import android.view.MenuItem
import android.widget.Button
import android.widget.ImageButton
import androidx.appcompat.app.AppCompatActivity

class MainActivity : AppCompatActivity() {

    private val mClient = WfClient()
    private var mLockSystemCommands = true

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_main)
        setSupportActionBar(findViewById(R.id.toolbar))
        mClient.attach(this)

//        fab.setOnClickListener { view ->
//            Snackbar.make(view, "Replace with your own action", Snackbar.LENGTH_LONG)
//                .setAction("Action", null).show()
//        }

//        val toolbar = findViewById<Toolbar>(R.id.toolbar)
//        toolbar.setLogo(R.mipmap.ic_launcher)

        findViewById<ImageButton>(R.id.play_button).setOnClickListener {
            mClient.postCommand("play")
        }
        findViewById<ImageButton>(R.id.pause_button).setOnClickListener {
            mClient.postCommand("pause")
        }
        findViewById<ImageButton>(R.id.play_fast_button).setOnClickListener {
            mClient.postCommand("fast")
        }
        findViewById<ImageButton>(R.id.next_button).setOnClickListener {
            mClient.postCommand("next")
        }
        findViewById<ImageButton>(R.id.prev_button).setOnClickListener {
            mClient.postCommand("prev")
        }
        findViewById<ImageButton>(R.id.step_back_5).setOnClickListener {
            mClient.postCommand("back10");
        }
        findViewById<ImageButton>(R.id.step_back_10).setOnClickListener {
            mClient.postCommand("backL");
        }
        findViewById<ImageButton>(R.id.step_fwd_5).setOnClickListener {
            mClient.postCommand("fwd10");
        }
        findViewById<ImageButton>(R.id.step_fwd_10).setOnClickListener {
            mClient.postCommand("fwdL");
        }
        findViewById<ImageButton>(R.id.super_fast).setOnClickListener {
            mClient.postCommand("superFast");
        }
        findViewById<ImageButton>(R.id.good_button).setOnClickListener {
            mClient.postCommand("good")
        }
        findViewById<ImageButton>(R.id.normal_button).setOnClickListener {
            mClient.postCommand("normal")
        }
        findViewById<ImageButton>(R.id.bad_button).setOnClickListener {
            mClient.postCommand("bad")
        }
        findViewById<ImageButton>(R.id.dreadful_button).setOnClickListener {
            mClient.postCommand("dreadful")
        }
        findViewById<ImageButton>(R.id.aspect_button).setOnClickListener {
            mClient.postCommand("std")
        }
        findViewById<ImageButton>(R.id.aspect_next_button).setOnClickListener {
            mClient.postCommand("custNext")
        }
        findViewById<ImageButton>(R.id.aspect_prev_button).setOnClickListener {
            mClient.postCommand("custPrev")
        }
        findViewById<ImageButton>(R.id.ko_mouse_button).setOnClickListener {
            mClient.postCommand("kickOutMouse")
        }
        findViewById<ImageButton>(R.id.show_slider).setOnClickListener {
            mClient.postCommand("showSlider");
        }
        findViewById<ImageButton>(R.id.shutdown_button).setOnClickListener {
            mClient.postCommand("shutdown")
        }
        findViewById<ImageButton>(R.id.close_button).setOnClickListener {
            mClient.postCommand("close")
        }
        findViewById<ImageButton>(R.id.lock_button).setOnClickListener {
            mLockSystemCommands = !mLockSystemCommands
            updateSystemCommands()
        }

        findViewById<Button>(R.id.execute_camera).setOnClickListener {
            val packageName = "io.github.toyota32k.secureCamera"
            val className = "io.github.toyota32k.secureCamera.MainActivity"
            val intent = Intent().apply {
                setClassName(packageName, className)
            }
            startActivity(intent)
        }

        findViewById<Button>(R.id.execute_cast).setOnClickListener {
            mClient.postCommand("close")
//            val intent2 = Intent().apply {
//                setPackage("com.google.android.apps.chromecast.app")
//                addCategory(Intent.CATEGORY_DEFAULT)
//            }
//            val resolveInfos = packageManager.queryIntentActivities(intent2, PackageManager.GET_META_DATA)
//            resolveInfos.forEach {
//                Log.d("test", "$it")
//            }

//            val intent = packageManager.getLaunchIntentForPackage("com.google.android.apps.chromecast.app")
            val intent = Intent().apply {
//                setClassName("com.google.android.apps.chromecast.app", "com.google.android.apps.chromecast.app.main.MainActivity")
                setPackage("com.google.android.apps.chromecast.app")
                addCategory(Intent.CATEGORY_DEFAULT)
                setAction(Intent.ACTION_MAIN)
                addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
            }
            startActivity(intent)
        }
        updateSystemCommands()
    }

    private fun updateSystemCommands() {
        findViewById<ImageButton>(R.id.close_button)?.apply {
            isEnabled = !mLockSystemCommands
        }
        findViewById<ImageButton>(R.id.shutdown_button)?.apply {
            isEnabled = !mLockSystemCommands
        }
    }

    override fun onCreateOptionsMenu(menu: Menu): Boolean {
        // Inflate the menu; this adds items to the action bar if it is present.
        menuInflater.inflate(R.menu.menu_main, menu)
        return true
    }

    override fun onOptionsItemSelected(item: MenuItem): Boolean {
        // Handle action bar item clicks here. The action bar will
        // automatically handle clicks on the Home/Up button, so long
        // as you specify a parent activity in AndroidManifest.xml.
        return when (item.itemId) {
            R.id.action_settings -> showSettings()
            else -> super.onOptionsItemSelected(item)
        }
    }

    fun showSettings() : Boolean {
        startActivity(Intent(this, SettingsActivity::class.java))
        return true
    }
}

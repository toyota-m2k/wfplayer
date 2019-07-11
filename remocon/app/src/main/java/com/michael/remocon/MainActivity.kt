package com.michael.remocon

import android.os.Bundle
import androidx.appcompat.app.AppCompatActivity
import android.view.Menu
import android.view.MenuItem
import android.widget.ImageButton

import kotlinx.android.synthetic.main.activity_main.*

class MainActivity : AppCompatActivity() {

    private val mClient = WfClient()
    private var mLockSystemCommands = true

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_main)
        setSupportActionBar(toolbar)

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
            R.id.action_settings -> true
            else -> super.onOptionsItemSelected(item)
        }
    }
}

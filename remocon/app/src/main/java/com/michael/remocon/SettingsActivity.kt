package com.michael.remocon

import SettingsFragment
import android.os.Bundle
import android.view.MenuItem
import androidx.appcompat.app.AppCompatActivity

class SettingsActivity : AppCompatActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.settings_activity)
        supportFragmentManager
            .beginTransaction()
            .replace(R.id.settings_panel, SettingsFragment())
            .commit()

        setSupportActionBar(findViewById(R.id.settingsToolbar))
        supportActionBar?.setDisplayHomeAsUpEnabled(true)

//        actionBar.setDisplayHomeAsUpEnabled(true)
    }

    override fun onOptionsItemSelected(item: MenuItem): Boolean {
        return when(item.itemId) {
            android.R.id.home-> { finish(); true }
            else-> super.onOptionsItemSelected(item)
        }
    }
}
package com.michael.remocon

import android.os.Bundle
import android.preference.PreferenceManager
import androidx.preference.EditTextPreference
import androidx.preference.PreferenceFragmentCompat

class SettingsFragment: PreferenceFragmentCompat() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
    }
    override fun onCreatePreferences(savedInstanceState: Bundle?, rootKey: String?) {
        setPreferencesFromResource(R.xml.settings, rootKey)
        val key = getText(R.string.key_server_address)
        val pref = findPreference<EditTextPreference>(key) ?: return
        pref.setOnBindEditTextListener {
            it.setSingleLine()
        }
        val prefMan = PreferenceManager.getDefaultSharedPreferences(activity)
        val address = prefMan.getString(key.toString(), "192.168.0.5")
        pref.title = address
    }
}
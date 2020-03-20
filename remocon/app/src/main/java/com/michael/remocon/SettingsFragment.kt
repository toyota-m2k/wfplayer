import android.content.SharedPreferences
import android.os.Bundle
import android.preference.PreferenceManager
import androidx.preference.EditTextPreference
import androidx.preference.PreferenceFragmentCompat
import com.michael.remocon.R

class SettingsFragment: PreferenceFragmentCompat(),
    SharedPreferences.OnSharedPreferenceChangeListener {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
    }

    val KEY_SERVER_ADDR
        get() = getString(R.string.key_server_address)

    override fun onCreatePreferences(savedInstanceState: Bundle?, rootKey: String?) {
        setPreferencesFromResource(R.xml.settings, rootKey)
        val pref = findPreference<EditTextPreference>(KEY_SERVER_ADDR) ?: return
        pref.setOnBindEditTextListener {
            it.setSingleLine()
        }
        val prefs = PreferenceManager.getDefaultSharedPreferences(activity)
        val address = prefs.getString(KEY_SERVER_ADDR, "192.168.0.5")
        pref.title = address
    }

    override fun onResume() {
        super.onResume()
        PreferenceManager.getDefaultSharedPreferences(activity).registerOnSharedPreferenceChangeListener(this)
    }

    override fun onPause() {
        super.onPause()
        PreferenceManager.getDefaultSharedPreferences(activity).unregisterOnSharedPreferenceChangeListener(this)
    }

    override fun onSharedPreferenceChanged(sharedPreferences: SharedPreferences?, key: String?) {
        if(key == KEY_SERVER_ADDR) {
            val s = sharedPreferences?.getString(key, null) ?: return
            findPreference<EditTextPreference>(key)?.title = s
        }
    }
}
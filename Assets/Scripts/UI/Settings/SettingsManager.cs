using System;
using System.Collections.Generic;
using Platformer;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance;

    public SettingsCollection AudioCollection;
    public SettingsCollection VideoCollection;
    [NonSerialized] public SettingsMenu Menu;

    private Dictionary<string, Setting> settingsLookup;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        settingsLookup = new Dictionary<string, Setting>();
        AddSettingsFromCollection(AudioCollection);
        AddSettingsFromCollection(VideoCollection);

        LoadAllSettings();
    }

    private void Start()
    {
        SubscribeToSettings();
    }

    private void OnDestroy()
    {
        if (Instance != this) return;

        UnsubscribeFromSettings();
        Instance = null;
    }
    
    private void AddSettingsFromCollection(SettingsCollection collection)
    {
        if (collection == null || collection.Settings == null) return;

        foreach (var setting in collection.Settings)
        {
            if (!string.IsNullOrEmpty(setting.Key))
            {
                if (!settingsLookup.ContainsKey(setting.Key))
                {
                    settingsLookup.Add(setting.Key, setting);
                }
            }
        }
    }

    private void SubscribeToSettings()
    {
        foreach (Setting setting in settingsLookup.Values)
        {
            switch (setting)
            {
                case BoolSetting boolSetting:
                    boolSetting.OnStateChanged += UpdateSetting;
                    break;
                case FloatSetting floatSetting:
                    floatSetting.OnValueChanged += UpdateSetting;
                    break;
                case MultiOptionSetting multiOptionSetting:
                    multiOptionSetting.OnIndexChanged += UpdateSetting;
                    break;
            }
        }
    }

    private void UnsubscribeFromSettings()
    {
        if (settingsLookup == null) return;

        foreach (Setting setting in settingsLookup.Values)
        {
            switch (setting)
            {
                case BoolSetting boolSetting:
                    boolSetting.OnStateChanged -= UpdateSetting;
                    break;
                case FloatSetting floatSetting:
                    floatSetting.OnValueChanged -= UpdateSetting;
                    break;
                case MultiOptionSetting multiOptionSetting:
                    multiOptionSetting.OnIndexChanged -= UpdateSetting;
                    break;
            }
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    
    #region GetDataTypes

    private float GetFloat(string key, float defualtValue)
    {
        if (settingsLookup != null && settingsLookup.TryGetValue(key, out Setting setting))
        {
            if (setting is FloatSetting floatSetting)
                return floatSetting.Value;
        }
        return defualtValue;
    }

    private bool GetBool(string key, bool defualtValue)
    {
        if (settingsLookup != null && settingsLookup.TryGetValue(key, out Setting setting))
        {
            if (setting is BoolSetting boolSetting)
                return boolSetting.IsChecked;
        }
        return defualtValue;
    }

    private int GetInt(string key, int defualtValue)
    {
        if (settingsLookup != null && settingsLookup.TryGetValue(key, out Setting setting))
        {
            if (setting is MultiOptionSetting multiOptionSetting)
                return multiOptionSetting.SelectedIndex;
        }
        return defualtValue;
    }
    
    #endregion

    public bool ParticEnabled => GetBool("ParticlesEnabled", true);
    public int Difficulty => GetInt("Difficulty", 0);
    public float MasterVolume => GetFloat("MasterVolume", 1f);
    public float MusicVolume => GetFloat("MusicVolume", 1f);
    public float EffectsVolume => GetFloat("SoundEffectsVolume", 1f);
    public float MenuVolume => GetFloat("MenuVolume", 1f);

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
    }

    #region HandleSettings

    public void ResetAllSettings()
    {
        foreach (Setting setting in settingsLookup.Values)
        {
            setting.ResetToDefault();
        }

        PlayerPrefs.Save();
        Menu?.SettingsList?.Refresh();
    }

    public void LoadAllSettings()
    {
        foreach (Setting setting in settingsLookup.Values)
        {
            switch (setting)
            {
                case BoolSetting boolSetting: boolSetting.Load(); break;
                case FloatSetting floatSetting: floatSetting.Load(); break;
                case MultiOptionSetting multiOptionSetting: multiOptionSetting.Load(); break;
            }
        }
        Menu?.SettingsList?.Refresh();
    }

    public void SaveAllSettings()
    {
        foreach (Setting setting in settingsLookup.Values)
        {
            switch (setting)
            {
                case BoolSetting boolSetting: boolSetting.Save(); break;
                case FloatSetting floatSetting: floatSetting.Save(); break;
                case MultiOptionSetting multiOptionSetting: multiOptionSetting.Save(); break;
            }
        }
        PlayerPrefs.Save();
        Menu?.SettingsList?.Refresh();
    }

    public void UpdateSetting(Setting setting)
    {
        if (setting is FloatSetting floatSetting)
        {
            switch (floatSetting.category)
            {
                case Setting.SettingCategory.Audio:
                    AudioManager.Instance?.UpdateAllVolumes();
                    break;
            }
        }
        else if (setting is MultiOptionSetting multiOptionSetting)
        {
            
        }
        else if (setting is BoolSetting boolSetting)
        {
            
        }
    }

    #endregion
}

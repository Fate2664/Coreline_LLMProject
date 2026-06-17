using System;
using UnityEngine;
using UnityEngine.Audio;

namespace Platformer
{
    public enum SoundCategory
    {
        Master,
        Music,
        Effects,
        Menu
    }

    public class AudioManager : MonoBehaviour
    {
        [Serializable]
        private class Sound
        {
            public string key;
            public AudioClip clip;
            [Range(0f, 1f)] public float volume;
            [Range(0.1f, 3f)] public float pitch;
            public bool loop;

            public SoundCategory category = SoundCategory.Master;

            [HideInInspector] public AudioSource source;
        }

        public static AudioManager Instance { get; private set; }

        [SerializeField] private Sound[] sounds;

        private HashMapBase<string, Sound> soundMap;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            soundMap = new HashMapBase<string, Sound>();

            foreach (Sound sound in sounds)
            {
                sound.source = gameObject.AddComponent<AudioSource>();
                sound.source.clip = sound.clip;
                sound.source.volume = sound.volume;
                sound.source.pitch = sound.pitch;
                sound.source.loop = sound.loop;

                soundMap.Insert_Update(sound.key, sound);
            }
        }

        private void Start()
        {
            Play("MenuMusic"); // Play the menu music on start
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }


        public void Play(string soundName)
        {
            soundMap.TryGetValue(soundName, out Sound s);
            if (s == null) return;

            //Stop Dupicate
            if (s.source.isPlaying)
            {
                s.source.Stop();
            }

            if (s.source.loop)
            {
                s.source.loop = true;
            }

            float categoryVolume = GetCategoryVolume(s.category);
            //Final audio = audio source volume * category volume * master volume
            s.source.volume = (s.volume * (categoryVolume / 100) * (SettingsManager.Instance.MasterVolume / 100));
            s.source.Play();
        }


        private float GetCategoryVolume(SoundCategory category)
        {
            switch (category)
            {
                case SoundCategory.Music:
                    return SettingsManager.Instance.MusicVolume;
                case SoundCategory.Effects:
                    return SettingsManager.Instance.EffectsVolume;
                case SoundCategory.Menu:
                    return SettingsManager.Instance.MenuVolume;
                default:
                    return 1f;
            }
        }

        public void UpdateAllVolumes()
        {
            foreach (Sound s in sounds)
            {
                float categoryVolume = GetCategoryVolume(s.category);
                //Final audio = audio source volume * category volume * master volume
                s.source.volume = (s.volume * (categoryVolume / 100) * (SettingsManager.Instance.MasterVolume / 100));
            }
        }
    }
}
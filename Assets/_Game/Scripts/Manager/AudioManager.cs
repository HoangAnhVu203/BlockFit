using UnityEngine;
using Solo.MOST_IN_ONE;

public class AudioManager : MonoBehaviour
{
    private const string MusicPrefsKey = "BlockFit_MusicEnabled";
    private const string SoundPrefsKey = "BlockFit_SoundEnabled";
    private const string VibratePrefsKey = "BlockFit_VibrateEnabled";
    private const float HapticCooldownSeconds = 0.08f;

    private static AudioManager instance;

    [Header("Audio Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource soundSource;

    [Header("Clips")]
    [SerializeField] private AudioClip buttonClickClip;

    public bool IsMusicEnabled { get; private set; }
    public bool IsSoundEnabled { get; private set; }
    public bool IsVibrateEnabled { get; private set; }

    public static AudioManager Instance => GetOrCreate();

    public static AudioManager GetOrCreate()
    {
        if (instance != null)
        {
            return instance;
        }

        instance = FindFirstObjectByType<AudioManager>();
        if (instance != null)
        {
            return instance;
        }

        GameObject managerObject = new GameObject(nameof(AudioManager));
        instance = managerObject.AddComponent<AudioManager>();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        LoadSettings();
        ApplyAudioState();
        ApplyHapticState();
        MOST_HapticFeedback.Prewarm();
    }

    public void ToggleMusic()
    {
        SetMusicEnabled(!IsMusicEnabled);
    }

    public void ToggleSound()
    {
        SetSoundEnabled(!IsSoundEnabled);
    }

    public void ToggleVibrate()
    {
        SetVibrateEnabled(!IsVibrateEnabled);
    }

    public void SetMusicEnabled(bool enabled)
    {
        IsMusicEnabled = enabled;
        PlayerPrefs.SetInt(MusicPrefsKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
        ApplyMusicState();
    }

    public void SetSoundEnabled(bool enabled)
    {
        IsSoundEnabled = enabled;
        PlayerPrefs.SetInt(SoundPrefsKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
        ApplySoundState();
    }

    public void SetVibrateEnabled(bool enabled)
    {
        IsVibrateEnabled = enabled;
        PlayerPrefs.SetInt(VibratePrefsKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
        ApplyHapticState();
    }

    public void PlayButtonClick()
    {
        PlaySound(buttonClickClip);
        PlayLightHaptic();
    }

    public void PlayLightHaptic()
    {
        if (!IsVibrateEnabled)
        {
            return;
        }

        MOST_HapticFeedback.GenerateWithCooldown(MOST_HapticFeedback.HapticTypes.LightImpact, HapticCooldownSeconds);
    }

    private void PlaySound(AudioClip clip)
    {
        if (!IsSoundEnabled || clip == null)
        {
            return;
        }

        AudioSource source = GetSoundSource();
        if (source != null)
        {
            source.PlayOneShot(clip);
        }
    }

    private void LoadSettings()
    {
        IsMusicEnabled = PlayerPrefs.GetInt(MusicPrefsKey, 1) == 1;
        IsSoundEnabled = PlayerPrefs.GetInt(SoundPrefsKey, 1) == 1;
        IsVibrateEnabled = PlayerPrefs.GetInt(VibratePrefsKey, 1) == 1;
    }

    private void ApplyAudioState()
    {
        ApplyMusicState();
        ApplySoundState();
    }

    private void ApplyMusicState()
    {
        if (musicSource == null)
        {
            return;
        }

        musicSource.mute = !IsMusicEnabled;

        if (IsMusicEnabled && musicSource.clip != null && !musicSource.isPlaying)
        {
            musicSource.Play();
        }
        else if (!IsMusicEnabled && musicSource.isPlaying)
        {
            musicSource.Pause();
        }
    }

    private void ApplySoundState()
    {
        if (soundSource != null)
        {
            soundSource.mute = !IsSoundEnabled;
        }
    }

    private void ApplyHapticState()
    {
        MOST_HapticFeedback.HapticsEnabled = IsVibrateEnabled;
    }

    private AudioSource GetSoundSource()
    {
        if (soundSource != null)
        {
            return soundSource;
        }

        soundSource = GetComponent<AudioSource>();
        if (soundSource == null)
        {
            soundSource = gameObject.AddComponent<AudioSource>();
        }

        return soundSource;
    }
}

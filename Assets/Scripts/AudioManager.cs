using UnityEngine;

/// <summary>
/// Global audio manager. Access from anywhere with AudioManager.Instance.PlayXxx().
/// Uses AudioSource.PlayOneShot so any number of sounds can overlap.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Settings")]
    [Range(0f, 1f)] [SerializeField] private float sfxVolume = 1f;
    [SerializeField] private bool persistBetweenScenes = true;

    [Header("Audio Clips")]
    public AudioClip backgroundMusic;
    public AudioClip diamondPickup;
    public AudioClip levelComplete;
    public AudioClip sploderBoom;
    public AudioClip playerDeath;
    public AudioClip itemPop;
    public AudioClip blackhole;
    public AudioClip trackerKill;

    private AudioSource sfxSource;
    private AudioSource musicSource;

    private void Awake()
    {
        // Static instance / singleton setup
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (persistBetweenScenes)
        {
            // DontDestroyOnLoad only works on root objects
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.spatialBlend = 0f; // 2D sound
        
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.spatialBlend = 0f;
        musicSource.loop = true;
        musicSource.volume = 0.5f;
    }
    
    private void Start()
    {
        // Skip if this is a duplicate that's about to be destroyed
        if (Instance != this) return;

        if (backgroundMusic != null)
        {
            musicSource.clip = backgroundMusic;
            musicSource.Play();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// Plays a clip without cutting off other sounds. Safe to call with a null clip.
    /// </summary>
    public void Play(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null || sfxSource == null) return;
        sfxSource.PlayOneShot(clip, sfxVolume * volumeScale);
    }

    public void SetSfxVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
    }

    // ---- Convenience methods ----
    public void PlayDiamondPickup() => Play(diamondPickup, 0.3f);
    public void PlayLevelComplete() => Play(levelComplete);
    public void PlaySploderBoom()   => Play(sploderBoom);
    public void PlayPlayerDeath()   => Play(playerDeath);
    public void PlayPop()           => Play(itemPop);
    public void PlayBlackhole()     => Play(blackhole);
    public void PlayTrackerKill()    => Play(trackerKill);
}
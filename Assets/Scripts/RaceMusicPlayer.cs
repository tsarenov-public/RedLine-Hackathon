using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(AudioSource))]
public class RaceMusicPlayer : MonoBehaviour
{
    [Header("Плейлист")]
    [Tooltip("Оставь пустым, чтобы автоматически загрузить все треки из Assets/Resources/Music")]
    public List<AudioClip> tracks = new List<AudioClip>();
    public bool playOnStart = true;
    public bool shuffle = true;

    [Header("Настройки звука")]
    [Range(0f, 1f)] public float volume = 0.6f;

    private AudioSource _audioSource;
    private int _currentIndex = -1;
    private bool _isPlayingPlaylist = false;

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.loop = false;
        _audioSource.spatialBlend = 0f; // 2D-стерео (играет одинаково громко по всей трассе)
        _audioSource.volume = volume;

        // Автоматическая загрузка всех файлов из папки Assets/Resources/Music
        if (tracks == null || tracks.Count == 0)
        {
            AudioClip[] loaded = Resources.LoadAll<AudioClip>("Music");
            tracks = new List<AudioClip>(loaded);
        }
    }

    private void Start()
    {
        if (playOnStart && tracks.Count > 0)
        {
            _isPlayingPlaylist = true;
            PlayNextTrack();
        }
    }

    private void Update()
    {
        _audioSource.volume = volume;

        if (tracks.Count == 0) return;

        // Клавиша N — переключить на следующий трек прямо во время гонки
        if (WasNextKeyPressed())
        {
            _isPlayingPlaylist = true;
            PlayNextTrack();
            return;
        }

        // Автопереключение, когда текущий трек закончился
        if (_isPlayingPlaylist && Application.isFocused && !_audioSource.isPlaying)
        {
            PlayNextTrack();
        }
    }

    public void PlayNextTrack()
    {
        if (tracks == null || tracks.Count == 0) return;

        if (shuffle && tracks.Count > 1)
        {
            int nextIndex;
            do
            {
                nextIndex = Random.Range(0, tracks.Count);
            } while (nextIndex == _currentIndex);

            _currentIndex = nextIndex;
        }
        else
        {
            _currentIndex = (_currentIndex + 1) % tracks.Count;
        }

        AudioClip clip = tracks[_currentIndex];
        if (clip != null)
        {
            _audioSource.clip = clip;
            _audioSource.Play();
            Debug.Log($"[Music] Сейчас играет: {clip.name}");
        }
    }

    private bool WasNextKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.nKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.N);
#endif
    }
}
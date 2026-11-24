using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class SceneMusic : MonoBehaviour
{
    [Header("Музыка для этой карты")]
    public AudioClip track;
    [Range(0f, 1f)] public float volume = 0.5f;

    private AudioSource _audioSource;

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.loop = true;        // Повтор трека после окончания
        _audioSource.spatialBlend = 0f;  // 2D-звук (одинаковая громкость по всей карте)
        _audioSource.volume = volume;
    }

    private void Start()
    {
        if (track != null)
        {
            _audioSource.clip = track;
            _audioSource.Play();
        }
    }
}
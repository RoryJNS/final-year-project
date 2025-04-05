using UnityEngine;

[RequireComponent (typeof(AudioSource))]
public class SoundManager : MonoBehaviour
{
    private static SoundManager instance;
    private AudioSource audioSource;
    [SerializeField] private AudioClip[] soundList;

    public enum SoundType
    {
        FOOTSTEP
    }

    private void Awake()
    {
        instance = this;
        audioSource = GetComponent<AudioSource>();
    }

    public static void PlaySound(SoundType type, float volume = 1)
    {
        instance.audioSource.PlayOneShot(instance.soundList[(int)type], volume);
    }
}
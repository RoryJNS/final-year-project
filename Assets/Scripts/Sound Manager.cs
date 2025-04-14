using UnityEngine;
using System.Collections.Generic;
using System.Collections;

[RequireComponent (typeof(AudioSource))]
public class SoundManager : MonoBehaviour
{
    private static SoundManager Instance;

    [SerializeField] private List<SoundEntry> sounds;
    private Dictionary<SoundType, SoundEntry> soundMap;
    [SerializeField] private AudioClip[] musicTracks;
    [SerializeField] private AudioSource sfxSource, musicSource;
    private AudioClip currentMusic;

    [System.Serializable]
    public struct SoundEntry
    {
        public SoundType type;
        public AudioClip clip;
        [Range(0f, 2f)] public float defaultVolume;
    }

    public enum SoundType
    {
        UICONFIRM,
        UIBACK,
        FOOTSTEP,
        DEATH,
        ITEMPICKUP,
        MELEEATTACK,
        RIFLESHOT,
        RIFLERELOAD,
        SMGSHOT,
        SMGRELOAD,
        SHOTGUNSHOT,
        SHOTGUNPUMP,
        SHOTGUNRELOAD,
        NOAMMO,
        CHESTOPEN,
        SCOREPOPUP
    }

    private void Awake()
    {
        Instance = this;
        soundMap = new Dictionary<SoundType, SoundEntry>();

        foreach (var entry in sounds)
        {
            soundMap[entry.type] = entry;
        }

        currentMusic = musicSource.clip;
    }

    public static void PlaySound(SoundType type)
    {
        if (!Instance.soundMap.ContainsKey(type)) return;

        var sound = Instance.soundMap[type];

        Instance.sfxSource.ignoreListenerPause =
            type == SoundType.UICONFIRM || type == SoundType.UIBACK;

        float finalVolume = sound.defaultVolume;
        Instance.sfxSource.PlayOneShot(sound.clip, finalVolume);
    }

    public static void PlaySound(SoundType type, AudioSource source)
    {
        if (!Instance.soundMap.ContainsKey(type)) return;

        var sound = Instance.soundMap[type];
        float finalVolume = sound.defaultVolume;
        source.PlayOneShot(sound.clip, finalVolume);
    }

    public static void ChooseLevelMusic()
    {
        if (Instance.musicTracks.Length == 0 || Instance.musicSource == null)
            return;

        AudioClip chosenClip;

        // Keep picking a new random clip until it's different from the last
        do
        {
            chosenClip = Instance.musicTracks[Random.Range(0, Instance.musicTracks.Length)];
        } while (Instance.musicTracks.Length > 1 && chosenClip == Instance.currentMusic);

        Instance.musicSource.clip = chosenClip;
        Instance.musicSource.Play();
        Instance.currentMusic = chosenClip;
    }

    public static void FadeOutMusic()
    {
        if (Instance.musicSource == null) return;
        Instance.StartCoroutine(Instance.FadeOutMusicCoroutine());
    }

    private IEnumerator FadeOutMusicCoroutine()
    {
        float startVolume = musicSource.volume;
        float time = 0f;

        while (time < 4)
        {
            time += Time.deltaTime;
            musicSource.volume = Mathf.Lerp(startVolume, 0f, time / 4);
            yield return null;
        }

        musicSource.Stop();
        musicSource.volume = startVolume;
    }
}
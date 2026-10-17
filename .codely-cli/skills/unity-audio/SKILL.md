---
name: unity-audio
description: Unity Audio system expert — AudioSource, AudioClip, AudioMixer, 3D audio, reverb zones, audio filters, FMOD integration. Use when implementing game sound effects, BGM management, 3D spatial audio, dynamic mixing, or optimizing audio assets.
---

# Unity Audio System

## Basic Playback

```csharp
[RequireComponent(typeof(AudioSource))]
public class SoundPlayer : MonoBehaviour
{
    public AudioClip clip;
    private AudioSource _source;

    void Awake() {
        _source = GetComponent<AudioSource>();
        _source.clip = clip;
        _source.volume = 0.8f;
        _source.pitch = 1f;
        _source.loop = false;
        _source.playOnAwake = false;
        _source.spatialBlend = 0f; // 0=2D, 1=3D
    }

    public void Play() => _source.Play();
    public void Stop() => _source.Stop();
    public void Pause() => _source.Pause();

    // Play one-shot (overlapping, no channel conflict)
    public void PlayOneShot(AudioClip sfx, float volumeScale = 1f) {
        _source.PlayOneShot(sfx, volumeScale);
    }
}
```

## Audio Manager (Singleton)

```csharp
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [SerializeField] private AudioSource _bgmSource;
    [SerializeField] private AudioSource _sfxSource;
    [SerializeField] private AudioClip[] _bgmClips;
    [SerializeField] private AudioClip[] _sfxClips;

    private Dictionary<string, AudioClip> _sfxMap = new();

    void Awake() {
        Instance = this;
        foreach (var clip in _sfxClips)
            _sfxMap[clip.name] = clip;
    }

    public void PlayBGM(string name) {
        var clip = System.Array.Find(_bgmClips, c => c.name == name);
        if (clip) { _bgmSource.clip = clip; _bgmSource.Play(); }
    }

    public void PlaySFX(string name, float volume = 1f) {
        if (_sfxMap.TryGetValue(name, out var clip))
            _sfxSource.PlayOneShot(clip, volume);
    }

    public void FadeBGM(float targetVolume, float duration) {
        StartCoroutine(FadeCoroutine(_bgmSource, targetVolume, duration));
    }

    IEnumerator FadeCoroutine(AudioSource src, float target, float dur) {
        float start = src.volume;
        float t = 0;
        while (t < dur) {
            t += Time.deltaTime;
            src.volume = Mathf.Lerp(start, target, t / dur);
            yield return null;
        }
    }
}
```

## 3D Spatial Audio

```csharp
// On AudioSource component:
//   Spatial Blend: 1.0 (3D)
//   3D Sound Settings:
//     Doppler Level: 1.0
//     Spread: 60
//     Volume Rolloff: Logarithmic (natural)
//       Min Distance: 3
//       Max Distance: 30

// Reverb Zone — add to area for environmental reverb
// Audio Reverb Zone component: settings for room, cave, forest, etc.
```

## AudioMixer (Dynamic Mixing)

```csharp
// Create AudioMixer asset: Create > Audio > AudioMixer
// Expose parameters: right-click volume slider > "Expose to script"

public class AudioMixerController : MonoBehaviour
{
    [SerializeField] private AudioMixer _mixer;

    public void SetMasterVolume(float value) {
        // Convert 0-1 linear to -80~0 dB
        _mixer.SetFloat("MasterVolume", Mathf.Log10(Mathf.Max(value, 0.0001f)) * 20f);
    }

    public void SetBGMVolume(float value) {
        _mixer.SetFloat("BGMVolume", Mathf.Log10(Mathf.Max(value, 0.0001f)) * 20f);
    }

    public void SetSFXVolume(float value) {
        _mixer.SetFloat("SFXVolume", Mathf.Log10(Mathf.Max(value, 0.0001f)) * 20f);
    }

    // Snapshot transitions (e.g., combat → calm)
    public void TransitionToCombat() {
        _mixer.FindSnapshot("Combat").TransitionTo(1.5f); // 1.5s transition
    }
}
```

## Audio Filters

```csharp
// Add as components, or via script:
// AudioLowPassFilter — muffle sound behind walls
// AudioHighPassFilter — radio/phone effect
// AudioDistortionFilter — heavy metal / robotic
// AudioEchoFilter — canyon / large room
// AudioReverbFilter — reverb without Reverb Zone

// Example: muffle audio when underwater
var lpf = GetComponent<AudioLowPassFilter>();
lpf.cutoffFrequency = isUnderwater ? 800f : 22000f;
```

## Audio Compression & Loading

| Load Type | Use case | Memory |
|-----------|----------|--------|
| DecompressOnLoad | Short SFX (< 5s) | High (full decompressed in RAM) |
| Streaming | BGM, long audio | Low (reads from disk) |
| CompressedInMemory | Medium clips, frequent play | Medium |

**Mobile tips:**
- Use `.ogg` for BGM (better compression than .wav)
- Use `.wav` for short SFX (faster decode)
- Max simultaneous voices: 16-24 on mobile
- Sample rate: 22050 Hz is sufficient for most SFX

## Key Gotchas

1. `PlayOneShot` can't be stopped — it plays to completion. Use separate AudioSource for stoppable SFX.
2. `AudioSource.volume` is 0-1 linear, but AudioMixer uses dB (-80 to 0). Convert with `Mathf.Log10(v) * 20`.
3. `spatialBlend = 0` = pure 2D (same everywhere). `= 1` = pure 3D (position-based).
4. AudioMixer snapshots are powerful for adaptive music — create snapshots per game state.
5. `AudioListener` should be on the main camera (or player). Only ONE listener per scene.
6. Loading large audio with `DecompressOnLoad` causes frame hitches — use Streaming for BGM.
7. `Resources.Load<AudioClip>` loads synchronously (frame hitch). Use `Addressables.LoadAssetAsync` for large clips.

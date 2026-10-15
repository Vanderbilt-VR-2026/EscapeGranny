// MenuAudio.cs
//
// The main menu's sound: a looping ambience (low drone, wind, a faint heartbeat) that fades in, plus
// short UI sounds that menu buttons ask for (hover tick, click, and a deep "boom" for the big
// buttons like PLAY and START).
//
// Setup: one per menu scene, on its own GameObject. Drag in the clips. MainMenuSceneBuilder does
// this, using the sounds MenuSoundGenerator makes in Assets/_Game/Audio/. Buttons find it through
// MenuAudio.Instance, so nothing needs wiring per button. The sound stops when the scene unloads.

using UnityEngine;

public class MenuAudio : MonoBehaviour
{
    public static MenuAudio Instance { get; private set; }

    [SerializeField] private AudioClip ambience;
    [SerializeField] private AudioClip hover;
    [SerializeField] private AudioClip click;
    [SerializeField] private AudioClip confirm;

    [SerializeField, Range(0f, 1f)] private float ambienceVolume = 0.5f;
    [SerializeField, Range(0f, 1f)] private float uiVolume = 0.7f;
    [Tooltip("Seconds for the ambience to fade in, so the menu doesn't start with a jolt.")]
    [SerializeField, Min(0f)] private float fadeInSeconds = 3f;

    private AudioSource loop;
    private AudioSource oneShots;

    private void Awake()
    {
        Instance = this;

        // 2D sources (spatialBlend 0): menu sound plays the same in both ears wherever you look.
        loop = gameObject.AddComponent<AudioSource>();
        loop.clip = ambience;
        loop.loop = true;
        loop.playOnAwake = false;
        loop.spatialBlend = 0f;
        loop.volume = 0f;
        if (ambience != null) loop.Play();

        oneShots = gameObject.AddComponent<AudioSource>();
        oneShots.playOnAwake = false;
        oneShots.spatialBlend = 0f;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (loop.volume < ambienceVolume)
            loop.volume = Mathf.MoveTowards(loop.volume, ambienceVolume,
                ambienceVolume * Time.unscaledDeltaTime / Mathf.Max(fadeInSeconds, 0.01f));
    }

    public void PlayHover() => Play(hover, 0.5f); // quieter: it plays a lot
    public void PlayClick() => Play(click, 1f);
    public void PlayConfirm() => Play(confirm, 1f);

    private void Play(AudioClip clip, float volume)
    {
        if (clip != null) oneShots.PlayOneShot(clip, uiVolume * volume);
    }
}

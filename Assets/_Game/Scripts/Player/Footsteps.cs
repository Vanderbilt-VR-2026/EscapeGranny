// Footsteps.cs
//
// Plays a footstep sound every time the watched head moves one stride across the floor, whether the
// player walks with the thumbstick or walks for real in their play space. Faster movement is louder,
// so creeping slowly is quieter (useful for the Prisoner). Big jumps in one frame (spawning, map
// teleport) don't count as steps. Only sideways movement counts, so crouching or looking around is silent.
//
// The sound is 3D: it comes from the player's feet and gets quieter with distance, so the other
// player can hear roughly where you are.
//
// Floors: steps carry down but not up. The player below hears the player above, quieter and muffled
// as if through the floorboards. The player above can't hear the player below. Your own steps are
// always on your floor, so you always hear them. The rule and its numbers live in FloorAudio.cs.
//
// Setup (multiplayer): open the Network Player prefab, select its "Head" child, Add Component >
// Footsteps. Because Fusion already moves the Head on every client, both players hear each other's
// steps with no extra networking. The owner hears their own steps too.
// Setup (offline test from the main menu): nothing. OfflineGameLauncher adds one to the camera.
// Sounds: drag clips into Clips, or leave it empty to use every clip in a Resources/Footsteps folder
// (Tools > Escape Granny > Generate Footstep Sounds makes placeholder ones there).

using UnityEngine;

public class Footsteps : MonoBehaviour
{
    [Tooltip("The thing whose movement makes steps. Leave empty to use this object (put it on the head).")]
    [SerializeField] private Transform tracked;

    [Tooltip("Picked at random each step so it doesn't sound like a loop. Empty = load Resources/Footsteps.")]
    [SerializeField] private AudioClip[] clips;

    [Tooltip("Metres moved across the floor between two step sounds.")]
    [SerializeField, Min(0.1f)] private float strideLength = 0.6f;

    [Tooltip("Moving slower than this (m/s) makes no steps, so leaning and head wobble stay silent.")]
    [SerializeField, Min(0f)] private float minSpeed = 0.3f;

    [Tooltip("Speed (m/s) at which steps reach full volume. Slower steps are quieter.")]
    [SerializeField, Min(0.1f)] private float fullVolumeSpeed = 2f;

    [SerializeField, Range(0f, 1f)] private float maxVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] private float minVolume = 0.25f;

    [Tooltip("Random pitch change per step (0.1 = up to 10% higher or lower).")]
    [SerializeField, Range(0f, 0.5f)] private float pitchVariation = 0.1f;

    [Tooltip("Random change in step length (0.15 = each stride up to 15% shorter or longer), so the rhythm isn't machine-even.")]
    [SerializeField, Range(0f, 0.4f)] private float strideVariation = 0.15f;

    [Tooltip("Random change in loudness per step (0.15 = up to 15% louder or quieter).")]
    [SerializeField, Range(0f, 0.5f)] private float volumeVariation = 0.15f;

    [Tooltip("How far left/right of the walking line each foot lands (m). Steps alternate sides.")]
    [SerializeField, Range(0f, 0.3f)] private float footSpacing = 0.12f;

    [Tooltip("Moving more than this in one frame is a teleport, not walking, so it makes no sound.")]
    [SerializeField, Min(0.1f)] private float teleportDistance = 1f;

    [Tooltip("Where the sound plays from if no floor is found below the head (m below the head). Normally the real floor is used.")]
    [SerializeField, Min(0f)] private float feetBelowHead = 1.4f;

    [Tooltip("Within this distance the steps are at full volume.")]
    [SerializeField, Min(0.1f)] private float fullVolumeDistance = 1.5f;

    [Tooltip("Beyond this distance the steps can't be heard.")]
    [SerializeField, Min(1f)] private float hearingDistance = 12f;

    // Floor rules (quieter and muffled from above, silent from below) are shared: see FloorAudio.

    private AudioSource source;
    private AudioLowPassFilter muffle;
    private Vector3 lastPosition;
    private float distanceSinceStep;
    private float nextStride;
    private readonly System.Collections.Generic.List<int> recent = new System.Collections.Generic.List<int>(); // clips played lately, oldest first
    private bool leftFoot;
    private Vector3 lastDirection = Vector3.forward; // which way the player was last walking

    // Lets code (OfflineGameLauncher) add footsteps at runtime with the default settings.
    public static Footsteps AddTo(GameObject target)
    {
        // Not "??": Unity objects can look non-null to "??" even when missing.
        var existing = target.GetComponent<Footsteps>();
        return existing != null ? existing : target.AddComponent<Footsteps>();
    }

    private void Awake()
    {
        if (tracked == null) tracked = transform;
        if (clips == null || clips.Length == 0) clips = Resources.LoadAll<AudioClip>("Footsteps");
        if (clips.Length == 0)
            Debug.LogWarning("[Footsteps] No footstep clips. Run Tools > Escape Granny > Generate Footstep Sounds, or drag clips in.", this);

        // The source lives on its own child so it can sit at the feet while this object stays on the head.
        var feet = new GameObject("Footstep Audio");
        feet.transform.SetParent(transform, false);
        source = feet.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 1f; // fully 3D: direction and distance matter
        source.rolloffMode = AudioRolloffMode.Linear; // reaches silence at hearingDistance, unlike Logarithmic
        source.minDistance = fullVolumeDistance;
        source.maxDistance = hearingDistance;
        source.dopplerLevel = 0f; // no pitch warping when the other player moves fast

        // Muffles steps coming through the ceiling. Off (no effect) for steps on the same floor.
        muffle = feet.AddComponent<AudioLowPassFilter>();
        muffle.enabled = false;
    }

    private void OnEnable()
    {
        lastPosition = tracked.position;
        distanceSinceStep = 0f;
        nextStride = strideLength;
    }

    private void Update()
    {
        Vector3 position = tracked.position;
        Vector3 moved = position - lastPosition;
        lastPosition = position;
        moved.y = 0f; // only movement across the floor; stairs still count through their sideways part

        float distance = moved.magnitude;
        if (distance > teleportDistance || Time.deltaTime <= 0f) return;

        float speed = distance / Time.deltaTime;
        if (distance > 0.0001f) lastDirection = moved / distance;
        if (speed < minSpeed)
        {
            // Standing still: start the next stride fresh, so the first step comes after a full stride.
            distanceSinceStep = 0f;
            return;
        }

        distanceSinceStep += distance;
        if (distanceSinceStep >= nextStride)
        {
            distanceSinceStep -= nextStride;
            // Real steps are never exactly the same length, so the rhythm isn't perfectly even.
            nextStride = strideLength * (1f + Random.Range(-strideVariation, strideVariation));
            PlayStep(speed);
        }
    }

    private void PlayStep(float speed)
    {
        if (clips.Length == 0) return;

        // Footsteps travel down through floors, not up (the shared rule in FloorAudio). Each headset
        // decides for itself: the person downstairs hears the person upstairs (quieter and muffled),
        // but the person upstairs doesn't hear the one below.
        // Which floor the walker is on comes from their HEAD, which is always between a floor and its
        // ceiling. (Guessing the feet as "1.4 m below the head" put them under the floor for shorter,
        // seated or crouching players, so their steps counted as "from below" and went silent.)
        if (!FloorAudio.CanHear(tracked.position, out float floorVolume, out bool muffled)) return;
        muffle.enabled = muffled;
        muffle.cutoffFrequency = FloorAudio.FromAboveMuffleHz;

        Vector3 feetPosition = FeetPosition();

        int index = PickClip();

        // Left foot, right foot: each step comes from slightly to one side of the walking line.
        leftFoot = !leftFoot;
        Vector3 side = Vector3.Cross(Vector3.up, lastDirection).normalized * (leftFoot ? -footSpacing : footSpacing);
        source.transform.position = feetPosition + side;

        source.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        float volume = Mathf.Lerp(minVolume, maxVolume, Mathf.InverseLerp(minSpeed, fullVolumeSpeed, speed));
        volume *= 1f + Random.Range(-volumeVariation, volumeVariation); // some steps land harder than others
        source.PlayOneShot(clips[index], Mathf.Clamp01(volume * floorVolume));
    }

    // A random clip, chosen evenly from the clips that weren't played recently. "Recently" is half the
    // clips (8 clips: the last 4 steps), so a clip never comes back soon, and no order (1, 2, 3, 4...)
    // can be heard, while there are always a few clips left to pick from at random.
    private int PickClip()
    {
        int n = clips.Length;
        if (n == 1) return 0;
        int avoid = Mathf.Max(1, n / 2);
        while (recent.Count > avoid) recent.RemoveAt(0); // in case clips were removed

        int index;
        do index = Random.Range(0, n);
        while (recent.Contains(index));

        recent.Add(index);
        if (recent.Count > avoid) recent.RemoveAt(0); // forget the oldest
        return index;
    }

    // Where the sound plays from: the floor straight below the head (found with a short downward ray,
    // ignoring triggers). If nothing is found, fall back to Feet Below Head, but never lower than that.
    private Vector3 FeetPosition()
    {
        Vector3 head = tracked.position;
        if (Physics.Raycast(head, Vector3.down, out RaycastHit hit, feetBelowHead + 0.5f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point;
        return head + Vector3.down * feetBelowHead;
    }
}

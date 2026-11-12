// DropSound.cs
//
// Plays a thunk / metal clank when a grabbable object hits the floor (or a wall, table, etc.) hard
// enough, as long as nobody is holding it. Harder landings are louder, so bounces get quieter.
//
// Multiplayer: only the headset that last held the object simulates its fall, so only that headset
// detects the landing. It asks XRGrabNetworkInteractable to tell both players to play the sound.
//
// Setup: nothing for the axe, knife, gun, bomb and key. Their scripts (Scratcher, Gun, Bomb, Key)
// add this automatically. For any other object, Add Component > Drop Sound. Leave Clip empty to use
// Resources/WeaponSounds/<Fallback Clip> (made by Tools > Escape Granny > Generate Weapon Sounds).

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(Rigidbody))]
public class DropSound : MonoBehaviour
{
    [SerializeField] AudioClip clip;
    [Tooltip("Used when Clip is empty: the name of a clip in Resources/WeaponSounds (DropHeavy or DropKey).")]
    [SerializeField] string fallbackClip = "DropHeavy";

    [Tooltip("Landing slower than this (m/s) is silent, so settling and rolling don't make noise.")]
    [SerializeField] float minImpactSpeed = 0.6f;
    [Tooltip("Landing at this speed (m/s) or faster is full volume.")]
    [SerializeField] float fullVolumeSpeed = 4f;
    [SerializeField, Range(0f, 1f)] float volume = 0.9f;
    [Tooltip("Shortest time (s) between two sounds, so one landing touching several colliders plays once.")]
    [SerializeField] float cooldown = 0.15f;
    [SerializeField, Range(0f, 0.3f)] float pitchVariation = 0.1f;
    [SerializeField] float hearingDistance = 15f;
    [Tooltip("Silent for this many seconds after the scene starts, while objects settle into place.")]
    [SerializeField] float quietAtStartSeconds = 1f;

    XRGrabInteractable grab;
    XRGrabNetworkInteractable networkGrab;
    float lastSoundTime = -999f;
    float quietUntil;

    bool Networked => networkGrab != null && networkGrab.Object && networkGrab.Object.IsValid;

    /// <summary>Adds a DropSound (if there isn't one) that uses this placeholder clip. Called from weapon/key scripts.</summary>
    public static DropSound Ensure(GameObject target, string fallbackClipName)
    {
        if (target.TryGetComponent<DropSound>(out var existing)) return existing;
        var added = target.AddComponent<DropSound>();
        added.fallbackClip = fallbackClipName;
        return added;
    }

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        networkGrab = GetComponent<XRGrabNetworkInteractable>();
        quietUntil = Time.time + quietAtStartSeconds;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (Time.time < quietUntil) return;
        if (grab != null && grab.isSelected) return;         // knocking it while holding isn't a drop
        if (Networked && !networkGrab.HasStateAuthority) return; // the other headset reports this one

        float speed = collision.relativeVelocity.magnitude;
        if (speed < minImpactSpeed || Time.time - lastSoundTime < cooldown) return;
        lastSoundTime = Time.time;

        float loudness = volume * Mathf.Lerp(0.3f, 1f, Mathf.InverseLerp(minImpactSpeed, fullVolumeSpeed, speed));
        Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;

        if (Networked) networkGrab.RequestDropSound(point, loudness);
        else Play(point, loudness);
    }

    // Runs on every headset (online through XRGrabNetworkInteractable's RPC).
    public void Play(Vector3 point, float loudness)
    {
        if (clip == null && !string.IsNullOrEmpty(fallbackClip))
            clip = Resources.Load<AudioClip>("WeaponSounds/" + fallbackClip);
        OneShotSound.PlayAt(clip, point, loudness, hearingDistance, 1f, pitchVariation);
    }
}

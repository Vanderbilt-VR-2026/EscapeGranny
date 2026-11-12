// OneShotSound.cs
//
// Plays a 3D sound at a spot in the world on a temporary object that deletes itself when the sound
// ends. Every interactable sound (bomb, gun, knife/axe, drops) goes through here, so they all follow
// the same rules for both players:
//   - Distance: full volume up close, fading to silence at hearingDistance.
//   - Floors (FloorAudio): from the floor above it's quieter and muffled; from the floor below it's silent.
//
// Setup: nothing. Call OneShotSound.PlayAt(clip, position, volume, hearingDistance) from any script.
// Pass `follow` to make the sound move with an object (like a bomb in flight).

using UnityEngine;

public static class OneShotSound
{
    /// <summary>
    /// Plays clip at position. Full volume within fullVolumeDistance, silent beyond hearingDistance.
    /// Returns null if nothing plays (no clip, or the sound is on a floor below this player).
    /// </summary>
    public static AudioSource PlayAt(AudioClip clip, Vector3 position, float volume, float hearingDistance,
                                     float fullVolumeDistance = 1f, float pitchVariation = 0f, float pitch = 1f,
                                     Transform follow = null)
    {
        if (clip == null) return null;
        if (!FloorAudio.CanHear(position, out float floorVolume, out bool muffled)) return null;

        var go = new GameObject($"Sound ({clip.name})");
        go.transform.position = position;
        if (follow != null) go.transform.SetParent(follow, true);

        var source = go.AddComponent<AudioSource>();
        Configure3D(source, fullVolumeDistance, hearingDistance);
        source.clip = clip;
        source.volume = volume * floorVolume;
        source.pitch = pitch * (1f + Random.Range(-pitchVariation, pitchVariation));

        if (muffled)
        {
            var muffle = go.AddComponent<AudioLowPassFilter>();
            muffle.cutoffFrequency = FloorAudio.FromAboveMuffleHz;
        }

        source.Play();

        // Lower pitch plays slower, so wait a bit longer than the clip itself.
        Object.Destroy(go, clip.length / Mathf.Max(source.pitch, 0.1f) + 0.1f);
        return source;
    }

    /// <summary>Sets an AudioSource up as a 3D sound that fades out to silence at hearingDistance.</summary>
    public static void Configure3D(AudioSource source, float fullVolumeDistance, float hearingDistance)
    {
        source.playOnAwake = false;
        source.spatialBlend = 1f;                      // fully 3D: direction and distance matter
        source.rolloffMode = AudioRolloffMode.Linear;  // actually reaches silence at maxDistance
        source.minDistance = fullVolumeDistance;
        source.maxDistance = Mathf.Max(hearingDistance, fullVolumeDistance + 0.1f);
        source.dopplerLevel = 0f;                      // no pitch warping from fast-moving objects
    }
}

// WeaponSoundGenerator.cs
//
// Makes placeholder weapon sounds from math (no downloaded audio, so no licences to worry about) and
// saves them in Assets/_Game/Resources/WeaponSounds/. Weapon scripts load them from there by name
// when their sound fields are left empty:
//   BombPin.wav        metallic click and ping of the pin coming out
//   BombThrow.wav      whoosh of the bomb leaving the hand
//   BombExplosion.wav  crack, deep boom and a long rumble with falling debris
//   GunShot.wav        handgun: sharp crack, punchy thump, slide click and a short room echo
//   BladeHitSurface.wav  knife/axe hitting wood or a wall: swish, a dull chop and a metal ring
//   BladeHitPlayer.wav   knife/axe hitting a player: swish, a soft wet slice and a muffled thud
//   DropHeavy.wav      axe/knife/gun/bomb landing on the floor: wooden thunk, metal clank, small bounce
//   DropKey.wav        key landing on the floor: light metal clinks as it bounces
//
// How to use: Tools > Escape Granny > Generate Weapon Sounds. Existing files are kept, so you can
// replace any of them with a real recording (same file name). Delete a file to get the generated one back.

using System.IO;
using UnityEditor;
using UnityEngine;

public static class WeaponSoundGenerator
{
    const string Folder = "Assets/_Game/Resources/WeaponSounds";
    static int Rate => MenuSoundGenerator.SampleRate;

    [MenuItem("Tools/Escape Granny/Generate Weapon Sounds")]
    public static void Generate()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Game/Resources")) AssetDatabase.CreateFolder("Assets/_Game", "Resources");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/_Game/Resources", "WeaponSounds");

        int made = 0;
        made += Make("BombPin", MakePin);
        made += Make("BombThrow", MakeThrow);
        made += Make("BombExplosion", MakeExplosion);
        made += Make("GunShot", MakeGunShot);
        made += Make("BladeHitSurface", MakeBladeHitSurface);
        made += Make("BladeHitPlayer", MakeBladeHitPlayer);
        made += Make("DropHeavy", MakeDropHeavy);
        made += Make("DropKey", MakeDropKey);
        Debug.Log($"[WeaponSoundGenerator] Made {made} weapon sound(s) in {Folder}.");
    }

    static int Make(string name, System.Func<float[]> make)
    {
        string path = $"{Folder}/{name}.wav";
        if (File.Exists(path)) return 0;
        MenuSoundGenerator.WriteWav(path, MenuSoundGenerator.Normalize(make(), 0.95f));
        AssetDatabase.ImportAsset(path);
        return 1;
    }

    static float Sin(float hz, float t) => Mathf.Sin(2f * Mathf.PI * hz * t);

    // Two quick metal "tinks": the ring sliding off, then the pin clearing the bomb.
    static float[] MakePin()
    {
        var samples = new float[(int)(0.4f * Rate)];
        var random = new System.Random(11);
        foreach (var (start, pitch, volume) in new[] { (0f, 1f, 1f), (0.07f, 0.85f, 0.7f) })
        {
            for (int i = (int)(start * Rate); i < samples.Length; i++)
            {
                float t = (float)i / Rate - start;
                // Metal rings at unrelated (inharmonic) frequencies, which is what makes it sound metallic.
                float ring = Sin(2400f * pitch, t) + 0.6f * Sin(3730f * pitch, t) + 0.3f * Sin(5210f * pitch, t);
                float click = t < 0.004f ? (float)random.NextDouble() * 2f - 1f : 0f;
                samples[i] += volume * (ring * Mathf.Exp(-t * 28f) * 0.5f + click);
            }
        }
        return samples;
    }

    // Noise whose brightness rises and falls, like air rushing past.
    static float[] MakeThrow()
    {
        const float seconds = 0.5f;
        var samples = new float[(int)(seconds * Rate)];
        var random = new System.Random(23);
        float bright = 0f, dark = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            float k = (float)i / samples.Length;               // 0..1 through the sound
            float swell = Mathf.Sin(Mathf.PI * k);              // rises then falls
            float noise = (float)random.NextDouble() * 2f - 1f;
            // Two smoothers: subtracting the slow one from the fast one keeps a band of "air" frequencies.
            bright += (0.05f + 0.25f * swell) * (noise - bright);
            dark += 0.02f * (noise - dark);
            samples[i] = (bright - dark) * swell * swell;
        }
        return samples;
    }

    // Adds one landing at `start`: a wooden knock (floorboards) plus metal ringing at inharmonic pitches.
    static void AddImpact(float[] samples, float start, float volume, float knockHz, float knockAmount,
                          float[] ringHz, float ringDecay, float ringAmount)
    {
        for (int i = (int)(start * Rate); i < samples.Length; i++)
        {
            float t = (float)i / Rate - start;
            float knock = Sin(knockHz, t) * Mathf.Exp(-t * 28f) * knockAmount;
            float ring = 0f;
            for (int r = 0; r < ringHz.Length; r++)
                ring += Sin(ringHz[r], t) / (r + 1);
            ring *= Mathf.Exp(-t * ringDecay) * ringAmount;
            samples[i] += volume * (knock + ring);
        }
    }

    // Heavy metal object on wooden boards: a solid thunk and clank, then one smaller bounce.
    static float[] MakeDropHeavy()
    {
        var samples = new float[(int)(0.7f * Rate)];
        var ring = new[] { 640f, 1010f, 1570f };
        AddImpact(samples, 0f, 1f, 110f, 1.2f, ring, 11f, 0.45f);
        AddImpact(samples, 0.14f, 0.35f, 120f, 1f, ring, 14f, 0.45f);
        return samples;
    }

    // Small key on the floor: bright clinks bouncing a few times, each quieter and closer together.
    static float[] MakeDropKey()
    {
        var samples = new float[(int)(0.55f * Rate)];
        var ring = new[] { 3150f, 4620f, 6080f };
        AddImpact(samples, 0f, 1f, 260f, 0.3f, ring, 22f, 0.8f);
        AddImpact(samples, 0.11f, 0.5f, 260f, 0.3f, ring, 26f, 0.8f);
        AddImpact(samples, 0.18f, 0.25f, 260f, 0.3f, ring, 30f, 0.8f);
        return samples;
    }

    // A short "air" swish leading into the hit, shared by both blade sounds.
    static float Swish(float t, ref float bright, ref float dark, System.Random random)
    {
        const float length = 0.08f;
        float noise = (float)random.NextDouble() * 2f - 1f;
        bright += 0.3f * (noise - bright);
        dark += 0.03f * (noise - dark);
        float k = Mathf.Clamp01(t / length);
        return (bright - dark) * Mathf.Sin(Mathf.PI * k) * 0.8f;
    }

    // Blade on wood or a wall: swish, a dull chop into the surface, and the blade ringing a little.
    static float[] MakeBladeHitSurface()
    {
        var samples = new float[(int)(0.5f * Rate)];
        var random = new System.Random(61);
        float bright = 0f, dark = 0f, wood = 0f;
        const float hitAt = 0.06f;
        for (int i = 0; i < samples.Length; i++)
        {
            float t = (float)i / Rate;
            float swish = Swish(t, ref bright, ref dark, random);

            float h = t - hitAt;
            float chop = 0f, ring = 0f;
            if (h > 0f)
            {
                // Chop: a low knock (wood resonance) plus a burst of smoothed noise (splintering).
                wood += 0.15f * ((float)random.NextDouble() * 2f - 1f - wood);
                chop = (Sin(180f, h) * 0.9f + wood * 2.5f) * Mathf.Exp(-h * 30f);
                // Ring: the steel blade vibrating after the impact.
                ring = (Sin(1850f, h) + 0.5f * Sin(2770f, h)) * Mathf.Exp(-h * 14f) * 0.18f;
            }
            samples[i] = swish + chop + ring;
        }
        return samples;
    }

    // Blade on a player: swish, a soft wet slice (no metal ring), and a muffled body thud.
    static float[] MakeBladeHitPlayer()
    {
        var samples = new float[(int)(0.45f * Rate)];
        var random = new System.Random(67);
        float bright = 0f, dark = 0f, wet = 0f;
        const float hitAt = 0.06f;
        for (int i = 0; i < samples.Length; i++)
        {
            float t = (float)i / Rate;
            float swish = Swish(t, ref bright, ref dark, random);

            float h = t - hitAt;
            float slice = 0f, thud = 0f;
            if (h > 0f)
            {
                // Slice: mid-range noise that wobbles in loudness, for a squelchy texture.
                wet += 0.2f * ((float)random.NextDouble() * 2f - 1f - wet);
                float wobble = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 38f * h);
                slice = wet * wobble * Mathf.Exp(-h * 12f) * 2.2f;
                // Thud: low and soft, like hitting something heavy and padded.
                thud = Sin(85f, h) * Mathf.Exp(-h * 22f);
            }
            samples[i] = swish + slice + thud;
        }
        return samples;
    }

    // Handgun: a very sharp crack, a short punchy thump, the slide snapping back, then the room echoing.
    static float[] MakeGunShot()
    {
        const float seconds = 0.9f;
        var samples = new float[(int)(seconds * Rate)];
        var random = new System.Random(53);
        float Noise() => (float)random.NextDouble() * 2f - 1f;

        float tail = 0f, phase = 0f;
        const float slideAt = 0.035f;
        for (int i = 0; i < samples.Length; i++)
        {
            float t = (float)i / Rate;

            // Crack: bright noise that's gone in a few hundredths of a second.
            float crack = Noise() * Mathf.Exp(-t * 90f) * 1.5f;

            // Thump: a low tone that drops from 160 Hz to 70 Hz, giving the shot its punch.
            float hz = Mathf.Lerp(70f, 160f, Mathf.Exp(-t * 30f));
            phase += 2f * Mathf.PI * hz / Rate;
            float thump = Mathf.Sin(phase) * Mathf.Exp(-t * 20f) * 1.2f;

            // Slide: a small metallic click just after the bang.
            float s = t - slideAt;
            float slide = s > 0f ? (Sin(2900f, s) + 0.5f * Sin(4300f, s)) * Mathf.Exp(-s * 120f) * 0.25f : 0f;

            // Echo: darker noise fading over most of a second, like the sound bouncing around a room.
            tail += 0.08f * (Noise() - tail);
            float echo = tail * Mathf.Exp(-t * 6f) * 1.8f * Mathf.Clamp01(t / 0.01f);

            samples[i] = crack + thump + slide + echo;
        }
        return samples;
    }

    // A sharp crack, a deep falling boom, a long rumble, and a few bits of debris landing.
    static float[] MakeExplosion()
    {
        const float seconds = 3f;
        var samples = new float[(int)(seconds * Rate)];
        var random = new System.Random(37);
        float Noise() => (float)random.NextDouble() * 2f - 1f;

        float rumble = 0f, phase = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            float t = (float)i / Rate;

            float crack = Noise() * Mathf.Exp(-t * 40f);

            // Boom: a low tone that slides from 70 Hz down to 30 Hz.
            float hz = Mathf.Lerp(30f, 70f, Mathf.Exp(-t * 4f));
            phase += 2f * Mathf.PI * hz / Rate;
            float boom = Mathf.Sin(phase) * Mathf.Exp(-t * 2.2f) * 1.4f;

            rumble += 0.03f * (Noise() - rumble);
            float rumbleSound = rumble * Mathf.Exp(-t * 1.1f) * 4f;

            samples[i] = crack + boom + rumbleSound;
        }

        // Debris: short taps scattered over the first two seconds, getting quieter.
        for (int d = 0; d < 14; d++)
        {
            float start = 0.2f + (float)random.NextDouble() * 1.8f;
            float hz = 180f + (float)random.NextDouble() * 500f;
            float volume = 0.25f * (1f - start / 2.2f);
            for (int i = (int)(start * Rate); i < samples.Length; i++)
            {
                float t = (float)i / Rate - start;
                if (t > 0.08f) break;
                samples[i] += volume * Sin(hz, t) * Mathf.Exp(-t * 60f);
            }
        }
        return samples;
    }
}

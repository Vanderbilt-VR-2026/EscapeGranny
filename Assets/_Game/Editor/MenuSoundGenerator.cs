// MenuSoundGenerator.cs
//
// Makes the main menu's placeholder sounds from math (no downloaded audio, so no licences to worry
// about) and saves them as .wav files in Assets/_Game/Audio/:
//   MenuAmbience.wav  10 s seamless loop: low detuned drone, dark wind, a faint slow heartbeat
//   MenuHover.wav     soft high tick when the controller ray moves onto a button
//   MenuClick.wav     short low "thunk" for ordinary buttons
//   MenuConfirm.wav   deep boom with a dissonant ring for PLAY / START / CONFIRM / READY
//
// How to use: runs automatically from Tools > Escape Granny > Build Main Menu. Each file is only
// made if it doesn't exist yet, so you can replace any of them with a real sound (same file name)
// and rebuilds will keep yours. Delete a file to get the generated one back.

using System.IO;
using UnityEditor;
using UnityEngine;

public static class MenuSoundGenerator
{
    const string Folder = "Assets/_Game/Audio";
    const int SampleRate = 22050; // plenty for these sounds, and half the file size of 44.1 kHz

    public static AudioClip Ambience => LoadOrCreate("MenuAmbience", MakeAmbience);
    public static AudioClip Hover => LoadOrCreate("MenuHover", MakeHover);
    public static AudioClip Click => LoadOrCreate("MenuClick", MakeClick);
    public static AudioClip Confirm => LoadOrCreate("MenuConfirm", MakeConfirm);

    static AudioClip LoadOrCreate(string name, System.Func<float[]> make)
    {
        string path = $"{Folder}/{name}.wav";
        if (!File.Exists(path))
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/_Game", "Audio");
            WriteWav(path, Normalize(make(), 0.9f));
            AssetDatabase.ImportAsset(path);
        }
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }

    static float[] MakeAmbience()
    {
        const float seconds = 10f;
        int n = (int)(seconds * SampleRate);
        int fade = SampleRate / 2;
        var output = new float[n];

        // Wind: random noise smoothed into a low rumble. Made a little longer than the loop, and the
        // extra tail is blended into the start, so the loop point can't be heard.
        var wind = new float[n + fade];
        var random = new System.Random(4249);
        float smooth = 0f;
        for (int i = 0; i < wind.Length; i++)
        {
            smooth += 0.02f * ((float)random.NextDouble() * 2f - 1f - smooth);
            wind[i] = smooth;
        }
        for (int i = 0; i < fade; i++)
        {
            float k = (float)i / fade;
            wind[i] = wind[i] * k + wind[n + i] * (1f - k);
        }

        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SampleRate;
            // Every frequency fits a whole number of times into 10 s, so the loop joins up cleanly.
            float drone = 0.18f * Sin(55f, t) + 0.12f * Sin(55.5f, t) + 0.05f * Sin(110.2f, t);
            float gusts = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 0.1f * t);
            output[i] = drone + 1.6f * wind[i] * gusts;
        }

        // Heartbeat: "lub-dub" every 1.25 s (8 beats per loop), quiet, felt more than heard.
        for (int beat = 0; beat < 8; beat++)
        {
            AddThump(output, beat * 1.25f, 0.5f);
            AddThump(output, beat * 1.25f + 0.28f, 0.35f);
        }
        return output;
    }

    static void AddThump(float[] buffer, float start, float volume)
    {
        int from = (int)(start * SampleRate);
        int length = (int)(0.3f * SampleRate);
        float phase = 0f;
        for (int i = 0; i < length && from + i < buffer.Length; i++)
        {
            float t = (float)i / SampleRate;
            phase += 2f * Mathf.PI * Mathf.Lerp(55f, 38f, t / 0.3f) / SampleRate; // pitch drops: "thud"
            float envelope = t * 40f * Mathf.Exp(-t * 25f);                      // quick rise, quick fall
            buffer[from + i] += volume * envelope * Mathf.Sin(phase);
        }
    }

    static float[] MakeHover()
    {
        var output = new float[(int)(0.04f * SampleRate)];
        for (int i = 0; i < output.Length; i++)
        {
            float t = (float)i / SampleRate;
            output[i] = Mathf.Exp(-t * 160f) * (Sin(1800f, t) + 0.3f * Sin(3600f, t)) * 0.35f;
        }
        return output;
    }

    static float[] MakeClick()
    {
        var output = new float[(int)(0.2f * SampleRate)];
        var random = new System.Random(7);
        float phase = 0f;
        for (int i = 0; i < output.Length; i++)
        {
            float t = (float)i / SampleRate;
            phase += 2f * Mathf.PI * Mathf.Lerp(130f, 45f, t / 0.2f) / SampleRate;
            float body = Mathf.Exp(-t * 18f) * Mathf.Sin(phase);
            float snap = Mathf.Exp(-t * 300f) * ((float)random.NextDouble() * 2f - 1f) * 0.4f;
            output[i] = body + snap;
        }
        return output;
    }

    static float[] MakeConfirm()
    {
        const float seconds = 2.2f;
        var output = new float[(int)(seconds * SampleRate)];
        var random = new System.Random(13);
        float phase = 0f, rumble = 0f;
        for (int i = 0; i < output.Length; i++)
        {
            float t = (float)i / SampleRate;
            // Sub boom that sinks in pitch.
            phase += 2f * Mathf.PI * Mathf.Lerp(70f, 32f, t / seconds) / SampleRate;
            float boom = Mathf.Exp(-t * 2.2f) * Mathf.Sin(phase);
            // Rumbling noise burst under it.
            rumble += 0.05f * ((float)random.NextDouble() * 2f - 1f - rumble);
            float noise = rumble * 3f * Mathf.Exp(-t * 3f);
            // Two close high notes that clash: the "something is wrong" ring of horror stingers.
            float ring = (Sin(880f, t) + Sin(932f, t)) * 0.05f * Mathf.Sin(Mathf.PI * Mathf.Min(t / seconds, 1f));
            output[i] = boom + noise + ring;
        }
        return output;
    }

    static float Sin(float hz, float t) => Mathf.Sin(2f * Mathf.PI * hz * t);

    static float[] Normalize(float[] samples, float peak)
    {
        float max = 0.0001f;
        foreach (float s in samples) max = Mathf.Max(max, Mathf.Abs(s));
        for (int i = 0; i < samples.Length; i++) samples[i] *= peak / max;
        return samples;
    }

    // 16-bit mono PCM .wav file.
    static void WriteWav(string path, float[] samples)
    {
        using var writer = new BinaryWriter(File.Create(path));
        int dataBytes = samples.Length * 2;
        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataBytes);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
        writer.Write(16);             // format chunk size
        writer.Write((short)1);       // PCM
        writer.Write((short)1);       // mono
        writer.Write(SampleRate);
        writer.Write(SampleRate * 2); // bytes per second
        writer.Write((short)2);       // bytes per sample frame
        writer.Write((short)16);      // bits per sample
        writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        writer.Write(dataBytes);
        foreach (float s in samples)
            writer.Write((short)(Mathf.Clamp(s, -1f, 1f) * short.MaxValue));
    }
}

// DoorSoundGenerator.cs
//
// Makes placeholder door and cabinet sounds from math and saves them in Assets/_Game/Resources/DoorSounds/.
// OpenCloseSound loads them from there by name when its clip fields are left empty:
//   DoorOpen.wav      long, slow hinge creak of a heavy old door
//   DoorClose.wav     heavy wooden thud with the latch clicking shut
//   CabinetOpen.wav   short, higher creak of a small cabinet or wardrobe door
//   CabinetClose.wav  light wooden clack
//   DrawerOpen.wav    wooden drawer sliding out: a rough, rattly rumble
//   DrawerClose.wav   a short slide ending in a knock as the drawer hits the frame
//
// How to use: Tools > Escape Granny > Generate Door Sounds. Existing files are kept, so you can
// replace any of them with a real recording (same file name). Delete a file to get the generated one back.

using System.IO;
using UnityEditor;
using UnityEngine;

public static class DoorSoundGenerator
{
    const string Folder = "Assets/_Game/Resources/DoorSounds";
    static int Rate => MenuSoundGenerator.SampleRate;

    [MenuItem("Tools/Escape Granny/Generate Door Sounds")]
    public static void Generate()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Game/Resources")) AssetDatabase.CreateFolder("Assets/_Game", "Resources");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/_Game/Resources", "DoorSounds");

        int made = 0;
        made += Make("DoorOpen", () => MakeCreak(1.1f, 45f, 35f, 520f, 1150f, 71));
        made += Make("DoorClose", () => MakeShut(85f, 1f, 0.05f, 2900f, 73));
        made += Make("CabinetOpen", () => MakeCreak(0.45f, 90f, 50f, 900f, 1900f, 79));
        made += Make("CabinetClose", () => MakeShut(170f, 0.45f, 0f, 0f, 83));
        made += Make("DrawerOpen", () => MakeSlide(0.5f, 89));
        made += Make("DrawerClose", () => MakeSlideShut(97));
        Debug.Log($"[DoorSoundGenerator] Made {made} door sound(s) in {Folder}.");
    }

    static int Make(string name, System.Func<float[]> make)
    {
        string path = $"{Folder}/{name}.wav";
        if (File.Exists(path)) return 0;
        MenuSoundGenerator.WriteWav(path, MenuSoundGenerator.Normalize(make(), 0.9f));
        AssetDatabase.ImportAsset(path);
        return 1;
    }

    // A hinge creak is "stick-slip": the hinge catches and lets go many times a second. Each slip is a
    // tiny click, and the clicks ring the wood at its own pitches. The slip rate wanders, which makes
    // the creak bend up and down.
    static float[] MakeCreak(float seconds, float slipRate, float slipWander, float woodHz1, float woodHz2, int seed)
    {
        var samples = new float[(int)(seconds * Rate)];
        var random = new System.Random(seed);
        var ring1 = new Resonator(woodHz1, 0.9965f);
        var ring2 = new Resonator(woodHz2, 0.995f);
        float phase = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            float t = (float)i / Rate;
            float k = t / seconds;
            float rate = slipRate + slipWander * Mathf.Sin(2f * Mathf.PI * 1.3f * t) + 10f * (float)random.NextDouble();
            phase += rate / Rate;
            float click = 0f;
            if (phase >= 1f)
            {
                phase -= 1f;
                click = 0.6f + 0.4f * (float)random.NextDouble();
            }
            float swell = Mathf.Sin(Mathf.PI * Mathf.Pow(k, 0.7f)); // fades in fast, out slowly
            samples[i] = (ring1.Next(click) + 0.6f * ring2.Next(click)) * swell;
        }
        return samples;
    }

    // Door shutting: a low knock of the wood, a puff of air/noise, and (for doors) the latch clicking.
    static float[] MakeShut(float knockHz, float seconds, float latchAt, float latchHz, int seed)
    {
        var samples = new float[(int)(seconds * Rate)];
        var random = new System.Random(seed);
        float noise = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            float t = (float)i / Rate;
            float knock = Mathf.Sin(2f * Mathf.PI * knockHz * t) * Mathf.Exp(-t * 18f);
            noise += 0.12f * ((float)random.NextDouble() * 2f - 1f - noise);
            float slam = noise * Mathf.Exp(-t * 35f) * 3f;
            float latch = 0f;
            float l = t - latchAt;
            if (latchHz > 0f && l > 0f) latch = Mathf.Sin(2f * Mathf.PI * latchHz * l) * Mathf.Exp(-l * 90f) * 0.35f;
            samples[i] = knock + slam + latch;
        }
        return samples;
    }

    // Wood rubbing on wood: dark noise, rattling (wobbling in loudness) as the runners catch and slip.
    static float[] MakeSlide(float seconds, int seed)
    {
        var samples = new float[(int)(seconds * Rate)];
        var random = new System.Random(seed);
        var body = new Resonator(240f, 0.995f); // the drawer box humming a little
        float rumble = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            float t = (float)i / Rate;
            float k = t / seconds;
            rumble += 0.08f * ((float)random.NextDouble() * 2f - 1f - rumble);
            float rattle = 0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * (28f + 9f * Mathf.Sin(t * 7f)) * t);
            float swell = Mathf.Sin(Mathf.PI * Mathf.Pow(k, 0.6f));
            samples[i] = (rumble * 3f + body.Next(rumble) * 0.02f) * rattle * swell;
        }
        return samples;
    }

    // A quick slide that ends in a knock against the cabinet frame.
    static float[] MakeSlideShut(int seed)
    {
        var slide = MakeSlide(0.18f, seed);
        var samples = new float[(int)(0.5f * Rate)];
        for (int i = 0; i < slide.Length; i++) samples[i] = slide[i] * 0.6f;
        var knock = MakeShut(150f, 0.32f, 0f, 0f, seed + 1);
        int at = (int)(0.16f * Rate);
        for (int i = 0; i < knock.Length && at + i < samples.Length; i++) samples[at + i] += knock[i];
        return samples;
    }

    // Rings at one pitch when hit, fading at a rate set by `decay` (closer to 1 = rings longer).
    class Resonator
    {
        readonly float a1, a2;
        float y1, y2;

        public Resonator(float hz, float decay)
        {
            a1 = 2f * decay * Mathf.Cos(2f * Mathf.PI * hz / Rate);
            a2 = -decay * decay;
        }

        public float Next(float input)
        {
            float y = input + a1 * y1 + a2 * y2;
            y2 = y1;
            y1 = y;
            return y;
        }
    }
}

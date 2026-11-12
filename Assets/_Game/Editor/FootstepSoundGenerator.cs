// FootstepSoundGenerator.cs
//
// Makes four placeholder footstep sounds from math (a dull heel thump on old wooden boards, a scuff
// of noise, and a softer toe tap), each slightly different, and saves them as
// Assets/_Game/Resources/Footsteps/Footstep1..4.wav. Footsteps.cs loads that folder automatically.
//
// How to use: Tools > Escape Granny > Generate Footstep Sounds. Existing files are kept, so you can
// replace any of them with a real recording (same name) or add more clips to the folder.
// Delete a file to get the generated one back.

using System.IO;
using UnityEditor;
using UnityEngine;

public static class FootstepSoundGenerator
{
    const string Folder = "Assets/_Game/Resources/Footsteps";
    const int Count = 4;

    [MenuItem("Tools/Escape Granny/Generate Footstep Sounds")]
    public static void Generate()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Game/Resources")) AssetDatabase.CreateFolder("Assets/_Game", "Resources");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/_Game/Resources", "Footsteps");

        int made = 0;
        for (int i = 1; i <= Count; i++)
        {
            string path = $"{Folder}/Footstep{i}.wav";
            if (File.Exists(path)) continue;
            MenuSoundGenerator.WriteWav(path, MenuSoundGenerator.Normalize(MakeStep(i), 0.9f));
            AssetDatabase.ImportAsset(path);
            made++;
        }
        Debug.Log($"[FootstepSoundGenerator] Made {made} footstep sound(s) in {Folder}.");
    }

    static float[] MakeStep(int seed)
    {
        int rate = MenuSoundGenerator.SampleRate;
        var random = new System.Random(seed * 7919);
        float Rand() => (float)random.NextDouble();

        var samples = new float[(int)(0.3f * rate)];
        float thumpHz = 70f + 25f * Rand();          // pitch of the board under the heel
        float toeDelay = 0.06f + 0.03f * Rand();     // heel-to-toe gap in seconds
        float creak = Rand() < 0.5f ? 0f : 0.15f;     // half the steps get a faint wood creak

        float smooth = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            float t = (float)i / rate;

            // Heel: low sine that dies away fast, like a thud on a floorboard.
            float heel = Mathf.Sin(2f * Mathf.PI * thumpHz * t) * Mathf.Exp(-t * 35f);

            // Scuff: noise smoothed so it's a soft shoe-on-wood sound, not a harsh hiss.
            smooth += 0.25f * (Rand() * 2f - 1f - smooth);
            float scuff = smooth * Mathf.Exp(-t * 25f) * 0.6f;

            // Toe: a quieter second tap just after the heel.
            float toeT = t - toeDelay;
            float toe = toeT > 0f ? Mathf.Sin(2f * Mathf.PI * thumpHz * 1.3f * toeT) * Mathf.Exp(-toeT * 45f) * 0.4f : 0f;

            // Creak: a short wobbling tone, for some steps.
            float creakSound = creak * Mathf.Sin(2f * Mathf.PI * (320f + 40f * Mathf.Sin(t * 30f)) * t) * Mathf.Exp(-t * 12f);

            samples[i] = heel + scuff + toe + creakSound;
        }
        return samples;
    }
}

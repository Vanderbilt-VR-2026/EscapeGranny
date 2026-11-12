// WallGrimeTextures.cs
//
// Makes the placeholder decal art for WallGrimeDecorator from math (no downloaded art, so no licences
// to worry about): blood splatters with drips, blood smears, claw scratches, dirt blotches, water
// stains and grime along the bottom of walls. Each is saved as a see-through PNG in
// Assets/_Game/Textures/WallGrime/ with a matching material in Assets/_Game/Materials/WallGrime/.
//
// Files are only made if they don't exist yet, so you can paint over a PNG (or replace it with real
// art, same file name, with a transparent background) and regenerating keeps yours. Delete a file to
// get the generated one back.
//
// The materials use the Escape Granny/Wall Decal (Multiply) shader: a decal darkens and tints the wall behind it,
// so it always matches the wall's lighting (including the baked lightmaps) and never glows in the dark.
// Multiply can only darken, so pale parts of a picture (like the torn plaster at a scratch's edge)
// show up as a faint tint rather than lighter than the wall.

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class WallGrimeTextures
{
    const string TextureFolder = "Assets/_Game/Textures/WallGrime";
    const string MaterialFolder = "Assets/_Game/Materials/WallGrime";

    public enum Kind { BloodSplatter, BloodSmear, Scratches, Dirt, WaterStain, Grime }

    // How many different-looking versions of each kind to make.
    static readonly Dictionary<Kind, int> Variants = new Dictionary<Kind, int>
    {
        { Kind.BloodSplatter, 3 }, { Kind.BloodSmear, 2 }, { Kind.Scratches, 3 },
        { Kind.Dirt, 3 }, { Kind.WaterStain, 2 }, { Kind.Grime, 2 },
    };

    /// <summary>Width / height of each kind's texture, so the decal quad isn't stretched.</summary>
    public static float Aspect(Kind kind) => kind == Kind.BloodSmear ? 2f : kind == Kind.Grime ? 4f : 1f;

    /// <summary>All materials for one kind, making any missing textures and materials first.</summary>
    public static List<Material> Materials(Kind kind)
    {
        EnsureFolder(TextureFolder);
        EnsureFolder(MaterialFolder);

        var list = new List<Material>();
        for (int v = 1; v <= Variants[kind]; v++)
        {
            string name = $"{kind}_{v}";
            string texPath = $"{TextureFolder}/{name}.png";
            if (!File.Exists(texPath)) SavePng(texPath, Paint(kind, v * 7919 + (int)kind * 104729));

            string matPath = $"{MaterialFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = name };
                AssetDatabase.CreateAsset(mat, matPath);
            }
            // Always (re)applied, so materials made by an older version of this tool get updated too.
            SetUpMultiply(mat, AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            EditorUtility.SetDirty(mat);
            list.Add(mat);
        }
        return list;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        string parent = path.Substring(0, slash);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
    }

    // ---------- materials ----------

    // URP Unlit, "Surface Type: Transparent, Blending Mode: Multiply". Multiply darkens and tints the
    // wall underneath by the picture's colour (where the picture is see-through, the wall is unchanged).
    // So the decal always gets exactly the wall's lighting, baked lightmaps included, without being
    // lit itself. That's also cheaper than a lit material.
    // Uses our own Wall Decal (Multiply) shader (Assets/_Game/Shaders). URP Unlit's Multiply mode
    // fogged the see-through parts of each decal too, so at a distance every decal showed as a dark square.
    static void SetUpMultiply(Material mat, Texture2D texture)
    {
        mat.shader = Shader.Find("Escape Granny/Wall Decal (Multiply)");
        mat.shaderKeywords = new string[0]; // drop leftovers from the old URP Unlit setup
        mat.renderQueue = -1;               // use the shader's (Transparent)
        mat.enableInstancing = true;
        mat.SetTexture("_BaseMap", texture);
        mat.SetColor("_BaseColor", Color.white);
    }

    /// <summary>Updates the existing Wall Grime materials to the current setup, without moving any decals.</summary>
    [MenuItem("Tools/Escape Granny/Wall Grime: Update Materials")]
    public static void UpdateAllMaterials()
    {
        int count = 0;
        foreach (Kind kind in System.Enum.GetValues(typeof(Kind))) count += Materials(kind).Count;
        AssetDatabase.SaveAssets();
        Debug.Log($"[WallGrimeTextures] Updated {count} Wall Grime materials. No decals were moved.");
    }

    // ---------- saving ----------

    static void SavePng(string path, Texture2D tex)
    {
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.alphaIsTransparency = true; // no dark fringe around the see-through edges
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = true;
        importer.maxTextureSize = 512;       // small enough for Quest memory
        importer.SaveAndReimport();
    }

    // ---------- painting ----------

    static Texture2D Paint(Kind kind, int seed)
    {
        switch (kind)
        {
            case Kind.BloodSplatter: return BloodSplatter(seed);
            case Kind.BloodSmear: return BloodSmear(seed);
            case Kind.Scratches: return Scratches(seed);
            case Kind.Dirt: return Dirt(seed);
            case Kind.WaterStain: return WaterStain(seed);
            default: return Grime(seed);
        }
    }

    // Layered Perlin noise, roughly 0..1. More octaves = more fine detail.
    static float Fbm(float x, float y, int octaves = 4)
    {
        float sum = 0f, amp = 0.5f, freq = 1f, total = 0f;
        for (int i = 0; i < octaves; i++)
        {
            sum += amp * Mathf.PerlinNoise(x * freq + 17.3f * i, y * freq + 9.1f * i);
            total += amp;
            amp *= 0.5f;
            freq *= 2f;
        }
        return sum / total;
    }

    static Texture2D NewTexture(int w, int h) => new Texture2D(w, h, TextureFormat.RGBA32, false);

    // Coverage mask helper: stamps soft-edged discs (in pixels), keeping the strongest coverage.
    static void Disc(float[] mask, int w, int h, float cx, float cy, float r)
    {
        int x0 = Mathf.Max(0, (int)(cx - r - 2)), x1 = Mathf.Min(w - 1, (int)(cx + r + 2));
        int y0 = Mathf.Max(0, (int)(cy - r - 2)), y1 = Mathf.Min(h - 1, (int)(cy + r + 2));
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                float cover = Mathf.Clamp01(r - d + 0.5f); // 1 px soft edge
                int i = y * w + x;
                if (cover > mask[i]) mask[i] = cover;
            }
    }

    // Turns a blood coverage mask into colour: fresh red where thick, darker dried brown-red in patches.
    static Texture2D ColorBlood(float[] mask, int w, int h, System.Random random)
    {
        var tex = NewTexture(w, h);
        var pixels = new Color[w * h];
        float ox = (float)random.NextDouble() * 100f, oy = (float)random.NextDouble() * 100f;
        var fresh = new Color(0.40f, 0.02f, 0.02f);
        var dried = new Color(0.17f, 0.03f, 0.02f);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                float m = mask[i];
                if (m <= 0f) { pixels[i] = Color.clear; continue; }
                float n = Fbm(x / 80f + ox, y / 80f + oy, 3);
                Color c = Color.Lerp(fresh, dried, Mathf.SmoothStep(0.3f, 0.7f, n));
                c.a = m * Mathf.Lerp(0.75f, 1f, Fbm(x / 20f + oy, y / 20f + ox, 2));
                pixels[i] = c;
            }
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    // A splash: a ragged central blob, spikes flung outward, scattered droplets, and drips running down.
    static Texture2D BloodSplatter(int seed)
    {
        const int N = 512;
        var random = new System.Random(seed);
        float R() => (float)random.NextDouble();
        var mask = new float[N * N];

        float cx = N * 0.5f, cy = N * 0.6f, r0 = N * (0.11f + 0.05f * R());
        float ox = R() * 100f, oy = R() * 100f;

        // Main blob with a wobbly edge.
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = x - cx, dy = y - cy, d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > r0 * 1.5f) continue;
                float ang = Mathf.Atan2(dy, dx);
                float edge = r0 * (0.65f + 0.7f * Fbm(Mathf.Cos(ang) * 1.8f + ox, Mathf.Sin(ang) * 1.8f + oy, 3));
                mask[y * N + x] = Mathf.Max(mask[y * N + x], Mathf.Clamp01(edge - d + 0.5f));
            }

        // Spikes: tapering streaks flung out from the blob, sometimes ending in a separate drop.
        int spikes = 10 + random.Next(9);
        for (int k = 0; k < spikes; k++)
        {
            float ang = R() * Mathf.PI * 2f;
            var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            float len = r0 * (0.5f + 1.4f * R()), rad = N * (0.008f + 0.014f * R());
            for (float s = 0f; s <= 1f; s += 0.5f / Mathf.Max(len / rad, 1f))
            {
                Vector2 p = new Vector2(cx, cy) + dir * (r0 * 0.7f + s * len);
                Disc(mask, N, N, p.x, p.y, rad * (1f - 0.8f * s));
            }
            if (R() < 0.5f)
            {
                Vector2 p = new Vector2(cx, cy) + dir * (r0 * 0.7f + len + rad * 3f);
                Disc(mask, N, N, p.x, p.y, rad * 0.6f);
            }
        }

        // Fine droplets further out, smaller the further they flew.
        int drops = 40 + random.Next(40);
        for (int k = 0; k < drops; k++)
        {
            float ang = R() * Mathf.PI * 2f;
            float dist = Mathf.Min(r0 * (1.2f + 2.4f * Mathf.Pow(R(), 0.7f)), N * 0.46f);
            float rad = Mathf.Max(1.2f, N * (0.002f + 0.008f * R()) * (1.8f - dist / (r0 * 3f)));
            Disc(mask, N, N, cx + Mathf.Cos(ang) * dist, cy + Mathf.Sin(ang) * dist, rad);
        }

        // Drips: run straight down (texture row 0 is the bottom), thinning, with a bead at the end.
        int drips = 2 + random.Next(4);
        for (int k = 0; k < drips; k++)
        {
            float x = cx + (R() * 2f - 1f) * r0 * 0.7f;
            float yStart = cy - r0 * 0.4f * R();
            float yEnd = Mathf.Max(N * 0.03f, yStart - N * (0.12f + 0.3f * R()));
            float width = N * (0.005f + 0.007f * R());
            for (float y = yStart; y >= yEnd; y -= 1f)
            {
                float t = (yStart - y) / Mathf.Max(yStart - yEnd, 1f);
                float wobble = (Mathf.PerlinNoise(y * 0.03f, k * 3.1f + ox) - 0.5f) * 3f;
                Disc(mask, N, N, x + wobble, y, width * (1f - 0.4f * t));
            }
            Disc(mask, N, N, x, yEnd, width * 1.4f);
        }

        return ColorBlood(mask, N, N, random);
    }

    // A smear dragged sideways (like a bloody hand pulled along the wall), streaky and fading out.
    static Texture2D BloodSmear(int seed)
    {
        const int W = 512, H = 256;
        var random = new System.Random(seed);
        float R() => (float)random.NextDouble();
        var mask = new float[W * H];
        float ox = R() * 100f, oy = R() * 100f;
        float x0 = W * 0.1f, x1 = W * 0.92f, curve = H * (0.05f + 0.1f * R()) * (R() < 0.5f ? -1f : 1f);

        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float t = Mathf.Clamp01((x - x0) / (x1 - x0));
                if (x < x0 - H * 0.3f) continue;
                float center = H * 0.5f + curve * Mathf.Sin(t * Mathf.PI);
                float half = H * 0.28f * (1f - 0.55f * t) * (0.85f + 0.3f * Mathf.PerlinNoise(t * 4f + ox, oy));
                float d = Mathf.Abs(y - center);
                float cover = Mathf.Clamp01(half - d + 0.5f);
                if (x < x0) cover *= Mathf.Clamp01(1f - (x0 - x) / (H * 0.25f)); // rounded start
                if (cover <= 0f) continue;
                // Streaks along the drag direction, getting thinner and patchier towards the end.
                float streak = Mathf.PerlinNoise(y * 0.12f + ox, t * 1.5f + oy);
                cover *= Mathf.Lerp(1f, Mathf.SmoothStep(0.25f, 0.65f, streak), 0.4f + 0.6f * t);
                cover *= 1f - 0.7f * t * t;
                mask[y * W + x] = cover;
            }

        // A couple of drips from the heavy start of the smear.
        for (int k = 0; k < 1 + random.Next(2); k++)
        {
            float x = x0 + W * 0.15f * R(), yStart = H * 0.35f, yEnd = H * (0.03f + 0.1f * R());
            for (float y = yStart; y >= yEnd; y -= 1f)
                Disc(mask, W, H, x, y, H * 0.012f);
            Disc(mask, W, H, x, yEnd, H * 0.018f);
        }

        return ColorBlood(mask, W, H, random);
    }

    // Claw marks: three or four parallel gouges, dark in the middle with pale torn plaster at the edges.
    static Texture2D Scratches(int seed)
    {
        const int N = 512;
        var random = new System.Random(seed);
        float R() => (float)random.NextDouble();
        int marks = 3 + random.Next(2);
        float angle = Mathf.Deg2Rad * (-60f + 25f * (R() * 2f - 1f));
        var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        var across = new Vector2(-dir.y, dir.x);
        float spacing = N * (0.06f + 0.03f * R());

        var starts = new Vector2[marks];
        var ends = new Vector2[marks];
        var widths = new float[marks];
        for (int k = 0; k < marks; k++)
        {
            float len = N * (0.55f + 0.2f * R());
            Vector2 mid = new Vector2(N * 0.5f, N * 0.5f) + across * (k - (marks - 1) / 2f) * spacing + dir * (R() - 0.5f) * N * 0.1f;
            starts[k] = mid - dir * len * 0.5f;
            ends[k] = mid + dir * len * 0.5f;
            widths[k] = N * (0.012f + 0.01f * R());
        }

        var groove = new Color(0.07f, 0.05f, 0.04f);
        var plaster = new Color(0.60f, 0.55f, 0.46f);
        var pixels = new Color[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                Color best = Color.clear;
                var p = new Vector2(x, y);
                for (int k = 0; k < marks; k++)
                {
                    Vector2 seg = ends[k] - starts[k];
                    float t = Mathf.Clamp01(Vector2.Dot(p - starts[k], seg) / seg.sqrMagnitude);
                    float d = Vector2.Distance(p, starts[k] + seg * t);
                    // Thin at both ends (the claw going in and coming out), ragged along the way.
                    float w = widths[k] * Mathf.Pow(Mathf.Sin(Mathf.PI * t), 0.6f);
                    d += (Mathf.PerlinNoise(t * 25f, k * 7.7f) - 0.5f) * w * 0.7f;
                    if (w <= 0.5f) continue;
                    if (d < w * 0.45f) { best = new Color(groove.r, groove.g, groove.b, 0.95f); break; }
                    if (d < w)
                    {
                        float a = 0.75f * (1f - (d - w * 0.45f) / (w * 0.55f));
                        if (a > best.a) best = new Color(plaster.r, plaster.g, plaster.b, a);
                    }
                }
                pixels[y * N + x] = best;
            }

        var tex = NewTexture(N, N);
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    // A soft, blotchy patch of grime, fading out at the edges.
    static Texture2D Dirt(int seed)
    {
        const int N = 256;
        var random = new System.Random(seed);
        float ox = (float)random.NextDouble() * 100f, oy = (float)random.NextDouble() * 100f;
        var pixels = new Color[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = (float)x / N, v = (float)y / N;
                float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f));
                float falloff = 1f - Mathf.SmoothStep(0.2f, 0.5f, d);
                float blotch = Mathf.Clamp01((Fbm(u * 3f + ox, v * 3f + oy, 5) - 0.42f) * 3f);
                float speckle = Fbm(u * 40f + oy, v * 40f + ox, 2) > 0.68f ? 0.25f : 0f;
                float a = Mathf.Clamp01(blotch * 0.6f + speckle) * falloff;
                float shade = 0.07f + 0.05f * Fbm(u * 8f + ox, v * 8f, 2);
                pixels[y * N + x] = new Color(shade * 1.2f, shade, shade * 0.7f, a);
            }
        var tex = NewTexture(N, N);
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    // A leak stain: a pale brown patch with darker "tide line" rings and streaks running down.
    static Texture2D WaterStain(int seed)
    {
        const int N = 256;
        var random = new System.Random(seed);
        float R() => (float)random.NextDouble();
        float ox = R() * 100f, oy = R() * 100f;
        var streakX = new float[4 + random.Next(4)];
        for (int i = 0; i < streakX.Length; i++) streakX[i] = 0.3f + 0.4f * R();

        var pixels = new Color[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = (float)x / N, v = (float)y / N;
                float dx = u - 0.5f, dy = v - 0.62f;
                float ang = Mathf.Atan2(dy, dx);
                float radius = 0.26f + 0.1f * Fbm(Mathf.Cos(ang) * 1.5f + ox, Mathf.Sin(ang) * 1.5f + oy, 3);
                float d = Mathf.Sqrt(dx * dx + dy * dy) / radius; // 1 = the outer tide line

                float a = 0f;
                if (d < 1f) a = 0.16f + 0.1f * Fbm(u * 6f + ox, v * 6f + oy, 3);
                a += 0.45f * Mathf.Exp(-Mathf.Pow((d - 1f) / 0.05f, 2f));    // outer tide line
                a += 0.2f * Mathf.Exp(-Mathf.Pow((d - 0.7f) / 0.04f, 2f));   // older inner line

                // Streaks running down from the stain.
                foreach (float sx in streakX)
                {
                    float across = Mathf.Abs(u - sx);
                    if (v < 0.5f && across < 0.012f)
                        a = Mathf.Max(a, 0.22f * (1f - across / 0.012f) * (v / 0.5f));
                }
                pixels[y * N + x] = new Color(0.30f, 0.22f, 0.11f, Mathf.Clamp01(a));
            }
        var tex = NewTexture(N, N);
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    // Grime along the bottom of a wall: darkest at the floor, fading upwards, patchy along its length.
    static Texture2D Grime(int seed)
    {
        const int W = 512, H = 128;
        var random = new System.Random(seed);
        float ox = (float)random.NextDouble() * 100f, oy = (float)random.NextDouble() * 100f;
        var pixels = new Color[W * H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float u = (float)x / W, v = (float)y / H;
                float rise = Fbm(u * 6f + ox, oy, 3);                 // how high the grime reaches here
                float a = Mathf.Clamp01(1f - v / Mathf.Lerp(0.3f, 1f, rise));
                a = Mathf.Pow(a, 1.5f) * (0.5f + 0.5f * Fbm(u * 20f + oy, v * 5f + ox, 3)) * 0.75f;
                a *= Mathf.SmoothStep(0f, 0.08f, u) * Mathf.SmoothStep(0f, 0.08f, 1f - u); // soft ends
                pixels[y * W + x] = new Color(0.08f, 0.06f, 0.04f, a);
            }
        var tex = NewTexture(W, H);
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }
}

using UnityEngine;

/// <summary>
/// Big text floating in front of this headset's camera ("Waiting for the other player...",
/// "GRANNY WINS"). Built in code, so there's nothing to set up. One message at a time.
/// </summary>
public static class FloatingMessage
{
    private static GameObject current;

    public static void Show(string text, Color color)
    {
        Hide();
        var cam = Camera.main;
        if (cam == null) return;

        current = new GameObject("Floating Message");
        current.transform.SetParent(cam.transform, false);
        current.transform.localPosition = new Vector3(0f, 0f, 1.5f);
        var mesh = current.AddComponent<TextMesh>();
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        mesh.font = font;
        current.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        mesh.text = text;
        mesh.fontSize = 100;
        mesh.characterSize = 0.01f;
        mesh.anchor = TextAnchor.MiddleCenter;
        mesh.alignment = TextAlignment.Center;
        mesh.color = color;
    }

    public static void Hide()
    {
        if (current != null) Object.Destroy(current);
        current = null;
    }
}

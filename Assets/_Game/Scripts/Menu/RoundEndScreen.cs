// RoundEndScreen.cs
//
// The results panel shown in front of the player when the round ends on time:
//   Granny   -> "Lose"  "You ran out of time. You were unable to catch the prisoner in time."
//   Prisoner -> "Lose"  "You ran out of time. You are still the Granny's prisoner!"
// The role comes from GameSession (what the player picked in the main menu).
// The panel appears once at eye height, straight ahead, and stays put so they can read it.
// It's drawn on top of walls, so it can't end up hidden inside one.
//
// Setup: nothing to put in a scene. RoundTimer calls RoundEndScreen.ShowTimeUp() at 0:00.
// To change the wording or colours, edit the texts below.

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class RoundEndScreen : MonoBehaviour
{
    // ---- Wording ----
    private const string GrannyTitle = "Lose";
    private const string GrannyMessage = "You ran out of time. You were unable to catch the prisoner in time.";
    private const string PrisonerTitle = "Lose";
    private const string PrisonerMessage = "You ran out of time. You are still the Granny's prisoner!";

    // ---- Look (menu palette) ----
    private static readonly Color Bone = new Color32(0xE8, 0xE1, 0xD3, 0xFF);
    private static readonly Color Ash = new Color32(0xA5, 0x9E, 0x90, 0xFF);
    private static readonly Color BloodRed = new Color32(0xD9, 0x3A, 0x2B, 0xFF);

    // ---- Placement ----
    private const float Distance = 1.6f;   // metres in front of the player
    private const float HeightOffset = -0.05f;

    public static RoundEndScreen Current { get; private set; }

    /// <summary>Shows the right "out of time" result for the role this player picked.</summary>
    public static void ShowTimeUp()
    {
        var role = GameSession.Instance != null ? GameSession.Instance.SelectedRole : PlayerRole.None;
        switch (role)
        {
            case PlayerRole.Granny:
                Show(GrannyTitle, GrannyMessage, BloodRed);
                break;
            case PlayerRole.Prisoner:
                Show(PrisonerTitle, PrisonerMessage, BloodRed);
                break;
            default:
                Show("Time's up", "", Bone);
                break;
        }
    }

    /// <summary>Shows a results panel straight ahead of the player.</summary>
    public static void Show(string title, string message, Color titleColor)
    {
        if (Current != null) Destroy(Current.gameObject);

        var cam = Camera.main;
        var screen = new GameObject("Round End Screen").AddComponent<RoundEndScreen>();
        Current = screen;

        if (cam != null)
        {
            Vector3 flat = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
            if (flat.sqrMagnitude < 0.001f) flat = Vector3.ProjectOnPlane(cam.transform.up, Vector3.up);
            flat.Normalize();
            Vector3 pos = cam.transform.position + flat * Distance;
            pos.y = cam.transform.position.y + HeightOffset;
            screen.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(flat, Vector3.up));
        }

        screen.Build(title, message, titleColor);
        Debug.Log($"[RoundEndScreen] {title}: {message}");
    }

    private void OnDestroy()
    {
        if (Current == this) Current = null;
    }

    private void Build(string title, string message, Color titleColor)
    {
        transform.localScale = Vector3.one * 0.0015f;   // 900 x 420 units = 1.35 m x 0.63 m

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 1001;
        ((RectTransform)transform).sizeDelta = new Vector2(900f, 420f);

        // Draw on top of walls and furniture.
        var onTop = new Material(Shader.Find("UI/Default"));
        onTop.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var bg = MakeChild<Image>("Background", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        bg.color = new Color(0f, 0f, 0f, 0.85f);
        bg.material = onTop;

        // Thin accent line under the title, like the menu's dividers.
        var divider = MakeChild<Image>("Divider", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-70f, 38f), new Vector2(70f, 42f));
        divider.color = titleColor;
        divider.material = onTop;

        var titleText = MakeChild<Text>("Title", new Vector2(0f, 0.55f), new Vector2(1f, 1f), new Vector2(30f, 0f), new Vector2(-30f, -20f));
        Style(titleText, font, onTop, title, 120, titleColor, FontStyle.Bold);

        var messageText = MakeChild<Text>("Message", new Vector2(0f, 0f), new Vector2(1f, 0.45f), new Vector2(70f, 30f), new Vector2(-70f, 0f));
        Style(messageText, font, onTop, message, 42, Bone, FontStyle.Normal);
        messageText.horizontalOverflow = HorizontalWrapMode.Wrap;
    }

    private T MakeChild<T>(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax) where T : Graphic
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(T));
        var rect = (RectTransform)go.transform;
        rect.SetParent(transform, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        var graphic = go.GetComponent<T>();
        graphic.raycastTarget = false;
        return graphic;
    }

    private static void Style(Text text, Font font, Material material, string content, int size, Color color, FontStyle style)
    {
        text.font = font;
        text.material = material;
        text.text = content;
        text.fontSize = size;
        text.color = color;
        text.fontStyle = style;
        text.alignment = TextAnchor.MiddleCenter;
        text.verticalOverflow = VerticalWrapMode.Overflow;
    }
}

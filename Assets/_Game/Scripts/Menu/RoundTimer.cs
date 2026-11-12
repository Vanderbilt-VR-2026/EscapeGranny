// RoundTimer.cs
//
// The round countdown (5:00 by default) shown at the top centre of the player's view, for both
// Granny and the Prisoner. It's a small panel attached to the headset camera, drawn on top of
// everything so walls never hide it. With one minute left it turns red, and in the last 30 seconds it
// also pulses. At 0:00 it fires RoundTimer.TimeUp, disappears, and RoundEndScreen shows the
// player's result (Lose, for Granny and the Prisoner alike).
//
// Setup: nothing to put in a scene. OfflineGameLauncher calls RoundTimer.StartRound() when the house
// loads. EscapeExit calls RoundTimer.Instance.Hide() when the prisoner escapes. Other scripts can use
// RoundTimer.Instance.Stop() to freeze it, and
// subscribe to RoundTimer.TimeUp (e.g. Granny wins).
// Keep the round length in sync with PrisonerBriefing's Round Seconds (shown on the briefing screen).

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class RoundTimer : MonoBehaviour
{
    public const float DefaultRoundSeconds = 300f;   // 5:00

    public static RoundTimer Instance { get; private set; }

    /// <summary>Fired once when the countdown reaches 0:00.</summary>
    public static event System.Action TimeUp;

    public float Remaining => remaining;
    public bool Running => running;

    // Placement in front of the eyes (metres, in the camera's space): a little above centre,
    // about 17 degrees up, so it sits at the top of the view without blocking anything.
    private const float Distance = 0.8f;
    private const float Up = 0.24f;
    private const float RedSeconds = 60f;     // turns red with one minute left
    private const float PulseSeconds = 30f;   // and starts pulsing for the last 30 seconds

    private static readonly Color NormalColor = new Color32(0xE8, 0xE1, 0xD3, 0xFF);  // menu "bone"
    private static readonly Color WarningColor = new Color32(0xD9, 0x3A, 0x2B, 0xFF); // blood red

    private float remaining;
    private bool running;
    private int shownSeconds = -1;
    private Text label;

    /// <summary>Starts (or restarts) the countdown at the top of the player's view.</summary>
    public static RoundTimer StartRound(float seconds = DefaultRoundSeconds)
    {
        if (Instance != null) Destroy(Instance.gameObject);

        var cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[RoundTimer] No Main Camera found, so the timer can't be shown.");
            return null;
        }

        var timer = new GameObject("Round Timer HUD").AddComponent<RoundTimer>();
        timer.Build(cam.transform);
        timer.remaining = seconds;
        timer.running = true;
        timer.Refresh();
        Debug.Log($"[RoundTimer] Round started: {seconds / 60f:0.#} minutes.");
        return timer;
    }

    /// <summary>Freezes the timer where it is (e.g. the prisoner escaped).</summary>
    public void Stop() => running = false;

    /// <summary>Stops the timer and removes it from view (e.g. the prisoner escaped).</summary>
    public void Hide()
    {
        running = false;
        gameObject.SetActive(false);
        Debug.Log($"[RoundTimer] Stopped and hidden with {Mathf.CeilToInt(remaining)} s left.");
    }

    private void Awake() => Instance = this;

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (!running) return;

        remaining -= Time.deltaTime;
        if (remaining <= 0f)
        {
            remaining = 0f;
            running = false;
            Debug.Log("[RoundTimer] Time's up.");
            TimeUp?.Invoke();

            // Show this player's result (Lose for both roles) and take the timer away.
            RoundEndScreen.ShowTimeUp();
            gameObject.SetActive(false);
            return;
        }
        Refresh();
    }

    private void Refresh()
    {
        int seconds = Mathf.CeilToInt(remaining);
        if (seconds != shownSeconds)
        {
            shownSeconds = seconds;
            label.text = $"{seconds / 60}:{seconds % 60:00}";
        }

        if (remaining <= RedSeconds)
        {
            var c = WarningColor;
            // Gently pulsing in the last half minute, so it feels more urgent.
            if (remaining <= PulseSeconds)
                c.a = 0.65f + 0.35f * (Mathf.Cos(remaining * Mathf.PI * 2f) + 1f) / 2f;
            label.color = c;
        }
        else
        {
            label.color = NormalColor;
        }
    }

    // Builds a small world-space canvas fixed to the headset camera.
    private void Build(Transform head)
    {
        transform.SetParent(head, false);
        transform.localPosition = new Vector3(0f, Up, Distance);
        // Face the eye (it sits above centre, so tilt it down toward the player).
        transform.localRotation = Quaternion.LookRotation(transform.localPosition.normalized, Vector3.up);
        transform.localScale = Vector3.one * 0.001f; // 1 canvas unit = 1 mm

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 1000;
        ((RectTransform)transform).sizeDelta = new Vector2(220f, 80f);

        // Draw on top of walls and furniture, so it never disappears when you stand close to something.
        var onTop = new Material(Shader.Find("UI/Default"));
        onTop.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);

        var bg = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var bgRect = (RectTransform)bg.transform;
        bgRect.SetParent(transform, false);
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = bgRect.offsetMax = Vector2.zero;
        var bgImage = bg.GetComponent<Image>();
        bgImage.color = new Color(0f, 0f, 0f, 0.55f);
        bgImage.material = onTop;
        bgImage.raycastTarget = false;

        var text = new GameObject("Time", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        var textRect = (RectTransform)text.transform;
        textRect.SetParent(transform, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;
        label = text.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 56;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.material = onTop;
        label.raycastTarget = false;
    }
}
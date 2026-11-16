using UnityEngine;
using UnityEngine.UI;

// Dark-edged "flashlight" vignette on the local Prisoner's own view. Local only: it is parented to the
// rig camera, so other players never see it. The gradient texture is painted in code (no art needed).
//  - Networked game: the copy on the Network Player prefab shows it for the local Prisoner.
//  - Single-headset offline test (no Fusion): a copy that creates itself at startup shows it when the
//    menu's chosen role is Prisoner and the round timer is running.
public class PrisonerVignette : MonoBehaviour
{
    [SerializeField] private NetworkAvatarBody avatarBody;
    [Tooltip("How dark the edges get (0 = invisible, 1 = black).")]
    [SerializeField, Range(0f, 1f)] private float edgeDarkness = 0.95f;
    [Tooltip("Size of the fully clear centre (smaller = tighter beam).")]
    [SerializeField, Range(0f, 0.9f)] private float clearRadius = 0.2f;
    [Tooltip("Overlay width in metres. About 1.8 fills a Quest 3 view; raise it for a gentler vignette.")]
    [SerializeField, Min(0.5f)] private float size = 1.8f;
    [SerializeField, Min(0.2f)] private float distance = 0.5f;

    private const int TexSize = 128;
    private GameObject overlay;
    private Transform overlayCam;
    private Image image;
    private Texture2D tex;
    private float builtDarkness = -1f, builtClear = -1f;

    // Offline mode: only the self-created copy uses these.
    private bool offlineMode;
    private bool networkedGame;
    private float nextOfflineCheck;

    // Creates the offline copy once at startup. Harmless in the networked game: it stays hidden there.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateOfflineDriver()
    {
        var go = new GameObject("Prisoner Vignette (offline)");
        DontDestroyOnLoad(go);
        go.AddComponent<PrisonerVignette>().offlineMode = true;
    }

    private bool ShouldShow()
    {
        if (!offlineMode)
        {
            var net = avatarBody != null ? avatarBody.Object : null;
            return net != null && net.IsValid && net.HasStateAuthority
                   && avatarBody.Role == PlayerRole.Prisoner;
        }

        // Offline: if a networked avatar exists, the copy on that prefab handles it instead.
        if (Time.unscaledTime >= nextOfflineCheck)
        {
            nextOfflineCheck = Time.unscaledTime + 0.5f;
            networkedGame = FindFirstObjectByType<NetworkAvatarBody>() != null;
        }
        if (networkedGame) return false;

        var timer = RoundTimer.Instance;
        var session = GameSession.Instance;
        return timer != null && timer.Running
               && session != null && session.SelectedRole == PlayerRole.Prisoner;
    }

    private void Update()
    {
        if (!ShouldShow()) { if (overlay != null) overlay.SetActive(false); return; }

        var cam = Camera.main;
        if (cam == null) return;

        // The rig is replaced on every scene load, so rebuild when the camera changes.
        if (overlay == null || overlayCam != cam.transform) Build(cam.transform);
        if (builtDarkness != edgeDarkness || builtClear != clearRadius) Paint();

        overlay.SetActive(true);
        overlay.transform.localPosition = new Vector3(0f, 0f, distance);
        overlay.transform.localScale = Vector3.one * (size / 1000f);
    }

    private void Build(Transform cam)
    {
        if (overlay != null) Destroy(overlay);
        overlayCam = cam;

        overlay = new GameObject("Prisoner Vignette", typeof(RectTransform), typeof(Canvas), typeof(Image));
        overlay.transform.SetParent(cam, false);

        var canvas = overlay.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 1000;
        overlay.GetComponent<RectTransform>().sizeDelta = new Vector2(1000f, 1000f);

        image = overlay.GetComponent<Image>();
        image.raycastTarget = false;
        builtDarkness = -1f;
        Paint();
    }

    private void Paint()
    {
        builtDarkness = edgeDarkness;
        builtClear = clearRadius;

        if (tex == null)
            tex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false)
            { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };

        var pixels = new Color32[TexSize * TexSize];
        float half = (TexSize - 1) * 0.5f;
        for (int y = 0; y < TexSize; y++)
        {
            for (int x = 0; x < TexSize; x++)
            {
                float dx = (x - half) / half, dy = (y - half) / half;
                float d = Mathf.Sqrt(dx * dx + dy * dy);          // 0 = centre, 1 = edge midpoints
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(clearRadius, 0.7f, d)) * edgeDarkness;
                pixels[y * TexSize + x] = new Color32(0, 0, 0, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();

        if (image.sprite == null)
            image.sprite = Sprite.Create(tex, new Rect(0, 0, TexSize, TexSize), new Vector2(0.5f, 0.5f), 100f);
    }

    private void OnDestroy()
    {
        if (overlay != null) Destroy(overlay);
        if (tex != null) Destroy(tex);
    }
}

// using UnityEngine;
// using UnityEngine.UI;

// // Dark-edged "flashlight" vignette on the local Prisoner's own view. Local only: it is parented to the
// // rig camera, so other players never see it. The gradient texture is painted in code (no art needed).
// public class PrisonerVignette : MonoBehaviour
// {
//     [SerializeField] private NetworkAvatarBody avatarBody;
//     [Tooltip("How dark the edges get (0 = invisible, 1 = black).")]
//     [SerializeField, Range(0f, 1f)] private float edgeDarkness = 0.92f;
//     [Tooltip("Size of the fully clear centre (smaller = tighter beam).")]
//     [SerializeField, Range(0f, 0.9f)] private float clearRadius = 0.25f;
//     [Tooltip("Overlay width in metres. About 1.8 fills a Quest 3 view; raise it for a gentler vignette.")]
//     [SerializeField, Min(0.5f)] private float size = 1.8f;
//     [SerializeField, Min(0.2f)] private float distance = 0.5f;

//     private const int TexSize = 128;
//     private GameObject overlay;
//     private Transform overlayCam;
//     private Image image;
//     private Texture2D tex;
//     private float builtDarkness = -1f, builtClear = -1f;

//     private void Update()
//     {
//         var net = avatarBody != null ? avatarBody.Object : null;
//         bool want = net != null && net.IsValid && net.HasStateAuthority
//                     && avatarBody.Role == PlayerRole.Prisoner;
//         if (!want) { if (overlay != null) overlay.SetActive(false); return; }

//         var cam = Camera.main;
//         if (cam == null) return;

//         // The rig is replaced on every scene load, so rebuild when the camera changes.
//         if (overlay == null || overlayCam != cam.transform) Build(cam.transform);
//         if (builtDarkness != edgeDarkness || builtClear != clearRadius) Paint();

//         overlay.SetActive(true);
//         overlay.transform.localPosition = new Vector3(0f, 0f, distance);
//         overlay.transform.localScale = Vector3.one * (size / 1000f);
//     }

//     private void Build(Transform cam)
//     {
//         if (overlay != null) Destroy(overlay);
//         overlayCam = cam;

//         overlay = new GameObject("Prisoner Vignette", typeof(RectTransform), typeof(Canvas), typeof(Image));
//         overlay.transform.SetParent(cam, false);

//         var canvas = overlay.GetComponent<Canvas>();
//         canvas.renderMode = RenderMode.WorldSpace;
//         canvas.sortingOrder = 1000;
//         overlay.GetComponent<RectTransform>().sizeDelta = new Vector2(1000f, 1000f);

//         image = overlay.GetComponent<Image>();
//         image.raycastTarget = false;
//         builtDarkness = -1f;
//         Paint();
//     }

//     private void Paint()
//     {
//         builtDarkness = edgeDarkness;
//         builtClear = clearRadius;

//         if (tex == null)
//             tex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false)
//             { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };

//         var pixels = new Color32[TexSize * TexSize];
//         float half = (TexSize - 1) * 0.5f;
//         for (int y = 0; y < TexSize; y++)
//         {
//             for (int x = 0; x < TexSize; x++)
//             {
//                 float dx = (x - half) / half, dy = (y - half) / half;
//                 float d = Mathf.Sqrt(dx * dx + dy * dy);          // 0 = centre, 1 = edge midpoints
//                 float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(clearRadius, 1f, d)) * edgeDarkness;
//                 pixels[y * TexSize + x] = new Color32(0, 0, 0, (byte)(a * 255f));
//             }
//         }
//         tex.SetPixels32(pixels);
//         tex.Apply();

//         if (image.sprite == null)
//             image.sprite = Sprite.Create(tex, new Rect(0, 0, TexSize, TexSize), new Vector2(0.5f, 0.5f), 100f);
//     }

//     private void OnDestroy()
//     {
//         if (overlay != null) Destroy(overlay);
//         if (tex != null) Destroy(tex);
//     }
// }
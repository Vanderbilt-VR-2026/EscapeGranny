// GrannyWeaponTutorial.cs
//
// Granny's "how to use your weapon" screen, shown after she confirms a weapon on the loadout screen
// and before she's dropped into the house. Works like PrisonerBriefing: fills in the text for the
// weapon she picked, READY starts the game, BACK returns to the weapon picker.
// {fuse} in the bomb text is replaced with the fuse time, e.g. "2.5".
//
// Optional video: give a weapon a Video Clip and it loops on the left of the screen, with the text
// card moved to the right. A weapon without a clip shows the text on its own, as before.
//
// Setup: in MainMenu run Tools > Escape Granny > Build Granny Weapon Tutorial Panel, then
// Tools > Escape Granny > Add Videos To Granny Tutorial (which also finds the Tutorial_*.mp4 clips).
// (Or by hand: put this on GrannyTutorialPanel, drag in the Heading and body Text, and hook up
//   READY Button OnClick -> GrannyWeaponTutorial.OnReadyPressed
//   BACK  Button OnClick -> GrannyWeaponTutorial.OnBackPressed
// then drag the panel into MenuController's Granny Tutorial Panel field.)

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class GrannyWeaponTutorial : MonoBehaviour
{
    [SerializeField] private MenuController menuController;
    [SerializeField] private Text headingText;
    [SerializeField] private Text bodyText;

    [Tooltip("Font size of the instructions. Lower it if the text runs past the edge of its card.")]
    [SerializeField, Range(10, 40)] private int bodyFontSize = 18;
    [Tooltip("Font size used when a weapon has no video (the card is wider then).")]
    [SerializeField, Range(10, 40)] private int bodyFontSizeNoVideo = 24;

    [Tooltip("Keep in sync with Fuse Time on the Bomb script on Bomb_Root (2.5 s by default).")]
    [SerializeField] private float bombFuseSeconds = 2.5f;

    [Header("Text (shown for every weapon, after the weapon's own text)")]
    [TextArea(2, 6)]
    [SerializeField] private string gripRule =
        "Keep holding the side GRIP button the whole time. If you let go you'll drop your weapon, " +
        "and the noise will reveal your position.";

    [Header("Text for each weapon")]
    [TextArea(2, 6)]
    [SerializeField] private string axeText =
        "Swing the axe to strike. Anything it hits gets scratched.\n" +
        "Get within arm's reach of the prisoner and swing.";

    [TextArea(2, 6)]
    [SerializeField] private string knifeText =
        "Swing or thrust the knife to strike. Anything it hits gets scratched.\n" +
        "Get within arm's reach of the prisoner and thrust.";

    [TextArea(2, 6)]
    [SerializeField] private string gunText =
        "Aim, then press the front TRIGGER button to shoot.\n" +
        "Ammo is limited, so make every bullet count.";

    [TextArea(2, 6)]
    [SerializeField] private string bombText =
        "Pull the pin out with your other hand to arm the bomb.\n" +
        "Then throw it. The fuse starts when it leaves your hand, and it explodes {fuse} seconds later.";

    [Header("Video for each weapon (optional)")]
    [SerializeField] private VideoClip axeVideo;
    [SerializeField] private VideoClip knifeVideo;
    [SerializeField] private VideoClip gunVideo;
    [SerializeField] private VideoClip bombVideo;

    [Header("Video display (filled in by the Add Videos tool)")]
    [SerializeField] private GameObject videoFrame;
    [SerializeField] private RawImage videoImage;
    [SerializeField] private VideoPlayer videoPlayer;
    [Tooltip("The text card, moved aside to make room for the video.")]
    [SerializeField] private RectTransform textCard;
    [SerializeField] private Vector2 cardPositionWithVideo = new Vector2(232f, -45f);
    [SerializeField] private Vector2 cardSizeWithVideo = new Vector2(400f, 252f);

    private Vector2 cardPositionNoVideo, cardSizeNoVideo;
    private bool layoutSaved;
    private RenderTexture videoTexture;

    private void Awake()
    {
        if (textCard != null)
        {
            cardPositionNoVideo = textCard.anchoredPosition;
            cardSizeNoVideo = textCard.sizeDelta;
            layoutSaved = true;
        }
    }

    private void OnEnable()
    {
        var weapon = GameSession.Instance != null ? GameSession.Instance.SelectedWeapon : WeaponType.None;

        string name, howTo;
        VideoClip clip;
        switch (weapon)
        {
            case WeaponType.Axe:   name = "AXE";   howTo = axeText;   clip = axeVideo; break;
            case WeaponType.Knife: name = "KNIFE"; howTo = knifeText; clip = knifeVideo; break;
            case WeaponType.Gun:   name = "GUN";   howTo = gunText;   clip = gunVideo; break;
            case WeaponType.Bomb:  name = "BOMB";  howTo = bombText.Replace("{fuse}", bombFuseSeconds.ToString("0.#")); clip = bombVideo; break;
            default:               name = "WEAPON"; howTo = ""; clip = null; break;
        }

        if (headingText != null) headingText.text = $"HOW TO USE THE {name}";
        if (bodyText != null) bodyText.text = string.IsNullOrEmpty(howTo) ? gripRule : howTo + "\n\n" + gripRule;

        ShowVideo(clip);
    }

    private void OnDisable()
    {
        if (videoPlayer != null) videoPlayer.Stop();
    }

    private void OnDestroy()
    {
        if (videoTexture != null) videoTexture.Release();
    }

    private void ShowVideo(VideoClip clip)
    {
        bool show = clip != null && videoPlayer != null && videoImage != null;

        if (videoFrame != null) videoFrame.SetActive(show);
        if (textCard != null && layoutSaved)
        {
            textCard.anchoredPosition = show ? cardPositionWithVideo : cardPositionNoVideo;
            textCard.sizeDelta = show ? cardSizeWithVideo : cardSizeNoVideo;
        }
        if (bodyText != null)
        {
            // A fixed size (not Best Fit), so every weapon's text looks the same and stays inside the card.
            bodyText.resizeTextForBestFit = false;
            bodyText.fontSize = show ? bodyFontSize : bodyFontSizeNoVideo;
            bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        }
        if (!show) return;

        if (videoTexture == null)
        {
            videoTexture = new RenderTexture(1280, 720, 0);
            videoTexture.Create();
        }
        videoImage.texture = videoTexture;

        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = true;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = videoTexture;
        videoPlayer.clip = clip;
        videoPlayer.Play();
    }

    public void OnReadyPressed()
    {
        menuController.StartGame();
    }

    public void OnBackPressed()
    {
        menuController.ShowGrannyLoadout();
    }
}
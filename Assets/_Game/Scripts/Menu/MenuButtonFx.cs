// MenuButtonFx.cs
//
// Makes a menu button feel alive: it grows a little and ticks while the controller ray hovers it,
// and "punches" with a sound when clicked, like the menus in most console/PC games. Greyed-out
// (non-interactable) buttons don't react. Sounds come from the scene's MenuAudio, if there is one.
//
// Setup: put this on a GameObject with a Button. MainMenuSceneBuilder adds it to every menu button.
// Works with XR rays because the XR UI Input Module sends normal pointer events.

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class MenuButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private float hoverScale = 1.05f;
    [SerializeField] private float clickScale = 0.95f;
    [Tooltip("How fast the button eases to its target size. Higher = snappier.")]
    [SerializeField] private float speed = 14f;
    [Tooltip("Big buttons (PLAY, START, CONFIRM, READY) play the deep confirm sound instead of the click.")]
    [SerializeField] private bool mainAction;

    private Button button;
    private Vector3 baseScale;
    private bool hovered;

    private void Awake()
    {
        button = GetComponent<Button>();
        baseScale = transform.localScale;
    }

    // Panels get hidden while a button is hovered; start un-hovered next time they're shown.
    private void OnDisable()
    {
        hovered = false;
        transform.localScale = baseScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        if (button.interactable && MenuAudio.Instance != null) MenuAudio.Instance.PlayHover();
    }

    public void OnPointerExit(PointerEventData eventData) => hovered = false;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!button.interactable) return;
        transform.localScale = baseScale * clickScale; // eases back in Update

        var audio = MenuAudio.Instance;
        if (audio == null) return;
        if (mainAction) audio.PlayConfirm();
        else audio.PlayClick();
    }

    private void Update()
    {
        float target = hovered && button.interactable ? hoverScale : 1f;
        // Unscaled time so the menu still animates if the game is paused.
        float t = 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime);
        transform.localScale = Vector3.Lerp(transform.localScale, baseScale * target, t);
    }
}

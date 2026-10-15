// MenuPulse.cs
//
// Makes a UI element softly pulse (brightness goes up and down), e.g. the gold frame around the
// role or weapon you picked, so the selection reads as "active" like in a game lobby.
//
// Setup: put this on a UI object with a Graphic (Image or Text). MainMenuSceneBuilder adds it to
// every selection frame.

using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Graphic))]
public class MenuPulse : MonoBehaviour
{
    [Tooltip("Lowest brightness, as a fraction of the normal colour.")]
    [SerializeField, Range(0f, 1f)] private float minAlpha = 0.45f;
    [Tooltip("Seconds for one full pulse.")]
    [SerializeField, Min(0.1f)] private float seconds = 1.4f;

    private Graphic graphic;
    private float baseAlpha;
    private float shownAt;

    private void Awake()
    {
        graphic = GetComponent<Graphic>();
        baseAlpha = graphic.color.a;
    }

    private void OnEnable() => shownAt = Time.unscaledTime;

    private void OnDisable()
    {
        if (graphic == null) return;
        var color = graphic.color;
        color.a = baseAlpha;
        graphic.color = color;
    }

    private void Update()
    {
        // Cosine starts at full brightness, so a freshly picked frame appears solid, then breathes.
        float wave = (Mathf.Cos((Time.unscaledTime - shownAt) * 2f * Mathf.PI / seconds) + 1f) / 2f;
        var color = graphic.color;
        color.a = baseAlpha * Mathf.Lerp(minAlpha, 1f, wave);
        graphic.color = color;
    }
}

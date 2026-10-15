// MenuFlicker.cs
//
// Makes a UI element flicker now and then like a failing light bulb (the horror-menu look from
// Granny and Five Nights at Freddy's). Most of the time it's steady; every few seconds it dips a
// few times quickly. The dips are short and only partly dim, so it stays comfortable in a headset.
//
// Setup: put this on any UI object with a Graphic (Text or Image). MainMenuSceneBuilder adds it to
// the title and the red glow behind it.

using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Graphic))]
public class MenuFlicker : MonoBehaviour
{
    [Tooltip("Seconds of steady light between flicker bursts (random in this range).")]
    [SerializeField] private Vector2 steadySeconds = new Vector2(3f, 8f);
    [Tooltip("How many quick dips each burst has (random in this range).")]
    [SerializeField] private Vector2Int dipsPerBurst = new Vector2Int(1, 4);
    [Tooltip("Brightness during a dip, as a fraction of normal (0 = off).")]
    [SerializeField, Range(0f, 1f)] private float dimTo = 0.35f;
    [SerializeField] private float dipSeconds = 0.06f;

    private Graphic graphic;
    private float baseAlpha;
    private float nextChange;
    private int dipsLeft;
    private bool dimmed;

    private void Awake()
    {
        graphic = GetComponent<Graphic>();
        baseAlpha = graphic.color.a;
    }

    private void OnEnable() => ScheduleSteady();

    private void OnDisable() => SetAlpha(baseAlpha);

    private void Update()
    {
        if (Time.unscaledTime < nextChange) return;

        if (dimmed)
        {
            // Back on. More dips left in this burst? Then wait only a moment before the next one.
            SetAlpha(baseAlpha);
            dimmed = false;
            if (--dipsLeft > 0) nextChange = Time.unscaledTime + Random.Range(dipSeconds, dipSeconds * 3f);
            else ScheduleSteady();
        }
        else
        {
            if (dipsLeft <= 0) dipsLeft = Random.Range(dipsPerBurst.x, dipsPerBurst.y + 1);
            SetAlpha(baseAlpha * dimTo);
            dimmed = true;
            nextChange = Time.unscaledTime + dipSeconds;
        }
    }

    private void ScheduleSteady()
    {
        dimmed = false;
        dipsLeft = 0;
        nextChange = Time.unscaledTime + Random.Range(steadySeconds.x, steadySeconds.y);
    }

    private void SetAlpha(float alpha)
    {
        if (graphic == null) return;
        var color = graphic.color;
        color.a = alpha;
        graphic.color = color;
    }
}

using UnityEngine;

/// <summary>
/// Quick flash: a sphere that pops up then shrinks to nothing, plus an optional fading light.
/// Reuse it for the muzzle flash, bullet impact and explosion by changing the numbers.
/// </summary>
public class FlashEffect : MonoBehaviour
{
    [SerializeField] Transform visual;
    [SerializeField] Light flashLight;
    [SerializeField] float duration = 0.12f;
    [SerializeField] float startScale = 0.02f;
    [SerializeField] float peakScale = 0.12f;
    [SerializeField] float lightIntensity = 3f;

    float t;

    void Start()
    {
        Destroy(gameObject, duration + 0.05f);
        Apply(0f);
    }

    void Update()
    {
        t += Time.deltaTime;
        Apply(Mathf.Clamp01(t / duration));
    }

    void Apply(float k)
    {
        // Grow quickly for the first 30%, then shrink to zero.
        float s = k < 0.3f
            ? Mathf.Lerp(startScale, peakScale, k / 0.3f)
            : Mathf.Lerp(peakScale, 0f, (k - 0.3f) / 0.7f);

        if (visual != null) visual.localScale = Vector3.one * s;
        if (flashLight != null) flashLight.intensity = lightIntensity * (1f - k);
    }
}

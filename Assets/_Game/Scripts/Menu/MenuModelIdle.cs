// MenuModelIdle.cs
//
// Idle animation for a 3D model shown in the menu: it slowly turns a little left and right (the
// weapons on the loadout cards), or spins all the way round like a turntable (the character preview
// on role select, like Fortnite's locker), and can gently "breathe", so it looks alive.
//
// Setup: put this on the model's root. It turns around the vertical axis of its parent (the card),
// through the middle of the model, so it doesn't drift sideways. MainMenuSceneBuilder and
// WeaponModelBuilder add it for you.

using UnityEngine;

public class MenuModelIdle : MonoBehaviour
{
    [Tooltip("How far it turns each way (degrees).")]
    [SerializeField] private float swayDegrees = 20f;
    [Tooltip("Seconds for one full left-right-left turn.")]
    [SerializeField, Min(0.1f)] private float swaySeconds = 7f;
    [Tooltip("Turntable speed (degrees per second). Not 0 = spin all the way round instead of swaying.")]
    [SerializeField] private float spinDegreesPerSecond;
    [Tooltip("How much it grows and shrinks vertically, like breathing (0.01 = 1%). 0 = off.")]
    [SerializeField] private float breathe;
    [SerializeField, Min(0.1f)] private float breatheSeconds = 3.5f;

    private Vector3 basePosition;
    private Quaternion baseRotation;
    private Vector3 baseScale;
    private Vector3 pivot; // middle of the model, in the parent's space
    private float phase;
    private float shownAt;

    private void Awake()
    {
        basePosition = transform.localPosition;
        baseRotation = transform.localRotation;
        baseScale = transform.localScale;
        phase = Random.Range(0f, 100f); // so several models don't move in lockstep

        pivot = basePosition;
        var renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0 && transform.parent != null)
        {
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            pivot = transform.parent.InverseTransformPoint(bounds.center);
        }
    }

    // A turntable starts facing the player each time it's shown.
    private void OnEnable() => shownAt = Time.unscaledTime;

    private void Update()
    {
        float t = Time.unscaledTime + phase;
        float angle = spinDegreesPerSecond != 0f
            ? (Time.unscaledTime - shownAt) * spinDegreesPerSecond
            : Mathf.Sin(t * 2f * Mathf.PI / swaySeconds) * swayDegrees;
        var turn = Quaternion.AngleAxis(angle, Vector3.up);
        transform.localRotation = turn * baseRotation;
        transform.localPosition = pivot + turn * (basePosition - pivot);

        if (breathe != 0f)
        {
            float s = 1f + Mathf.Sin(t * 2f * Mathf.PI / breatheSeconds) * breathe;
            transform.localScale = new Vector3(baseScale.x, baseScale.y * s, baseScale.z);
        }
    }
}

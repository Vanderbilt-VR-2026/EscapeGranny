using UnityEngine;

// Put this on the DRAWER object. One select slides it out, the next slides it back in.
// Uses the IInteractable interface defined in Door.cs, so keep Door.cs in the project.
public class DrawerSlide : MonoBehaviour, IInteractable
{
    [SerializeField] private Vector3 openOffset = new Vector3(0f, 0f, 0.4f); // local-space pull direction and distance
    [SerializeField] private float duration = 0.6f;                          // seconds for a full open or close
    [SerializeField] private AnimationCurve easing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private bool isOpen = false;

    private Vector3 closedPosition;
    private Vector3 openPosition;
    private float progress; // 0 = closed, 1 = open

    private void Awake()
    {
        closedPosition = transform.localPosition;
        openPosition = closedPosition + openOffset;
        progress = isOpen ? 1f : 0f;
        transform.localPosition = Vector3.LerpUnclamped(closedPosition, openPosition, easing.Evaluate(progress));
    }

    public void Interact()
    {
        isOpen = !isOpen;
    }

    private void Update()
    {
        float target = isOpen ? 1f : 0f;
        progress = Mathf.MoveTowards(progress, target, Time.deltaTime / Mathf.Max(duration, 0.01f));

        transform.localPosition = Vector3.LerpUnclamped(
            closedPosition, openPosition, easing.Evaluate(progress));
    }
}

using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Put on the Door_Hinge object (the pivot the door swings around).
/// When the Padlock unlocks: the chain drops, the padlock falls, then the door swings open.
/// </summary>
public class EscapeDoor : MonoBehaviour
{
    [SerializeField] Padlock padlock;
    [SerializeField] ChainBuilder chain;

    [Header("Swing")]
    [Tooltip("Degrees the door turns around the hinge's Y axis. Use a negative number to swing the other way.")]
    [SerializeField] float openAngle = 100f;
    [SerializeField] float openTime = 1.8f;
    [Tooltip("Seconds between the chain falling and the door starting to move.")]
    [SerializeField] float delayAfterUnlock = 0.8f;

    [Header("Padlock mounting")]
    [Tooltip("Freeze the padlock on the chain until it is unlocked, and stop it being picked up.")]
    [SerializeField] bool mountPadlock = true;

    public bool IsOpen { get; private set; }

    Quaternion closedRot;
    Rigidbody padlockRb;
    XRGrabInteractable padlockGrab;
    bool opening;

    void Awake()
    {
        closedRot = transform.localRotation;

        // If the padlock has a HangingPadlock, it hangs and stays grabbable by itself.
        if (padlock != null && mountPadlock && padlock.GetComponent<HangingPadlock>() == null)
        {
            padlockRb = padlock.GetComponent<Rigidbody>();
            padlockGrab = padlock.GetComponent<XRGrabInteractable>();
            if (padlockRb != null) padlockRb.isKinematic = true;
            if (padlockGrab != null) padlockGrab.enabled = false;
        }
    }

    void OnEnable()
    {
        if (padlock != null) padlock.onUnlocked.AddListener(Open);
    }

    void OnDisable()
    {
        if (padlock != null) padlock.onUnlocked.RemoveListener(Open);
    }

    // Right-click the component header in Play mode and choose "Open" to test without the key.
    [ContextMenu("Open")]
    public void Open()
    {
        if (opening || IsOpen) return;
        opening = true;
        StartCoroutine(OpenRoutine());
    }

    IEnumerator OpenRoutine()
    {
        if (chain != null) chain.Release();

        if (padlockRb != null)
        {
            padlockRb.isKinematic = false;
            padlockRb.useGravity = true;
        }

        yield return new WaitForSeconds(delayAfterUnlock);

        Quaternion openRot = closedRot * Quaternion.Euler(0f, openAngle, 0f);
        for (float t = 0f; t < openTime; t += Time.deltaTime)
        {
            transform.localRotation = Quaternion.Slerp(closedRot, openRot, Mathf.SmoothStep(0f, 1f, t / openTime));
            yield return null;
        }
        transform.localRotation = openRot;

        IsOpen = true;
        opening = false;
    }
}
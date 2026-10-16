using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Put on Key_Root. Marks this object as a key that a Padlock can detect.
/// The insert direction is from the key's root toward its Tip.
/// While inserted, the Padlock borrows the key's Visual (the model) and seats it in the keyhole;
/// when the key is pulled out, the visual eases back into the hand.
/// </summary>
public class Key : MonoBehaviour
{
    public static readonly List<Key> All = new List<Key>();

    [Tooltip("The point of the key that goes into the keyhole (KeyTip). Found automatically if left empty.")]
    [SerializeField] Transform tip;

    [Tooltip("The key model (rust_key). Found automatically if left empty.")]
    [SerializeField] Transform visual;

    [Tooltip("Optional. A padlock with an Accepted Key Id only opens for a key with the same id. Leave empty for a key that fits any lock.")]
    [SerializeField] string keyId = "";

    XRGrabInteractable grab;
    Collider[] colliders;
    Vector3 visualLocalPos;
    Quaternion visualLocalRot;
    bool returning;

    public bool IsHeld => grab != null && grab.isSelected;
    public bool IsInserted { get; set; }
    public Transform Visual => visual;
    public Collider[] Colliders => colliders;
    public Vector3 TipPosition => tip != null ? tip.position : transform.position;

    public Vector3 InsertDirection
    {
        get
        {
            if (tip == null) return transform.forward;
            Vector3 d = tip.position - transform.position;
            return d.sqrMagnitude > 1e-8f ? d.normalized : transform.forward;
        }
    }

    public bool Matches(string acceptedId) =>
        string.IsNullOrEmpty(acceptedId) || acceptedId == keyId;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        colliders = GetComponentsInChildren<Collider>(true);

        if (tip == null) tip = transform.Find("KeyTip");

        if (visual == null)
        {
            visual = transform.Find("rust_key");
            if (visual == null)
            {
                foreach (Transform child in transform)
                {
                    if (child == tip) continue;
                    if (child.GetComponentInChildren<Renderer>() != null) { visual = child; break; }
                }
            }
        }

        if (visual != null)
        {
            visualLocalPos = visual.localPosition;
            visualLocalRot = visual.localRotation;
        }
    }

    void OnEnable() => All.Add(this);

    void OnDisable()
    {
        All.Remove(this);
        if (visual != null && visual.parent != transform)
        {
            visual.SetParent(transform, false);
            visual.localPosition = visualLocalPos;
            visual.localRotation = visualLocalRot;
        }
        returning = false;
        IsInserted = false;
    }

    /// <summary>Hands the model to the padlock (keeps its current world pose).</summary>
    public void DetachVisual(Transform newParent)
    {
        if (visual == null) return;
        returning = false;
        visual.SetParent(newParent, true);
    }

    /// <summary>Takes the model back and eases it into its normal spot in the hand.</summary>
    public void ReturnVisual()
    {
        if (visual == null) return;
        visual.SetParent(transform, true);
        returning = true;
    }

    void Update()
    {
        if (!returning || visual == null) return;

        float k = 1f - Mathf.Exp(-25f * Time.deltaTime);
        visual.localPosition = Vector3.Lerp(visual.localPosition, visualLocalPos, k);
        visual.localRotation = Quaternion.Slerp(visual.localRotation, visualLocalRot, k);

        if ((visual.localPosition - visualLocalPos).sqrMagnitude < 1e-8f &&
            Quaternion.Angle(visual.localRotation, visualLocalRot) < 0.1f)
        {
            visual.localPosition = visualLocalPos;
            visual.localRotation = visualLocalRot;
            returning = false;
        }
    }

    void OnDrawGizmosSelected()
    {
        if (tip == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, tip.position);
        Gizmos.DrawWireSphere(tip.position, 0.004f);
    }
}
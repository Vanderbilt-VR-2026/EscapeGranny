using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Put on Padlock_Root. Bring a Key's tip to the keyhole: the key model slides into the lock,
/// turns, and then the padlock toggles locked / unlocked. Pull your hand away to take the key out.
///
/// Unlocking lifts the shackle straight up out of the body, then swings it around one leg,
/// like a real padlock. Locking does the reverse.
/// </summary>
public class Padlock : MonoBehaviour
{
    [Header("Keyhole")]
    [Tooltip("Transform at the keyhole. Its forward (Z) axis is the direction the key slides in.")]
    [SerializeField] Transform keyhole;
    [Tooltip("How close (meters) the key tip must get to the keyhole to start inserting.")]
    [SerializeField] float insertRadius = 0.035f;
    [Tooltip("1 = key must point exactly along the keyhole axis, 0 = any angle within 90 degrees.")]
    [SerializeField, Range(-1f, 1f)] float alignmentTolerance = 0.3f;
    [Tooltip("Optional. Only keys with this id work. Leave empty to accept any key.")]
    [SerializeField] string acceptedKeyId = "";
    [SerializeField] bool requireKeyHeld = true;
    [Tooltip("If on, the padlock must also be held in the other hand. Off lets you unlock it on the table.")]
    [SerializeField] bool requireLockHeld = false;
    [SerializeField] float cooldown = 1f;

    [Header("Key insertion")]
    [Tooltip("How far (meters) the key tip goes in past the keyhole point.")]
    [SerializeField] float insertDepth = 0.02f;
    [Tooltip("How far (degrees) the key turns. Negative turns the other way.")]
    [SerializeField] float turnAngle = 90f;
    [SerializeField] float slideTime = 0.15f;
    [SerializeField] float turnTime = 0.25f;
    [Tooltip("Move your hand this far (meters) from the keyhole to pull the key back out.")]
    [SerializeField] float pullOutDistance = 0.06f;
    [Tooltip("Within this distance (meters) the key passes through the padlock's colliders so it can reach the hole.")]
    [SerializeField] float approachDistance = 0.1f;

    [Header("State")]
    [SerializeField] bool startLocked = true;

    [Header("Shackle")]
    [Tooltip("The U-shaped bar from the padlock model.")]
    [SerializeField] Transform shackle;
    [Tooltip("Optional. An empty object placed on the shackle leg it should swing around. " +
             "Leave empty to use one of the legs automatically.")]
    [SerializeField] Transform shackleHinge;
    [Tooltip("Which leg the shackle swings around when no hinge is assigned. Flip this if it swings the wrong way.")]
    [SerializeField] bool hingeOnLeft = false;
    [Tooltip("How far (meters) the shackle lifts out of the body before swinging.")]
    [SerializeField] float openLift = 0.012f;
    [Tooltip("How far (degrees) the shackle swings open. Negative swings the other direction.")]
    [SerializeField] float openAngle = 90f;
    [Tooltip("Seconds for the full open or close motion.")]
    [SerializeField] float animTime = 0.4f;

    [Header("Feedback (optional)")]
    [Tooltip("A renderer that turns Locked/Unlocked color.")]
    [SerializeField] Renderer statusLight;
    [SerializeField] Color lockedColor = Color.red;
    [SerializeField] Color unlockedColor = Color.green;
    [Tooltip("Logs to the Console when the lock changes state or a key is blocked.")]
    [SerializeField] bool debugLog = true;

    public UnityEvent onUnlocked;
    public UnityEvent onLocked;

    public bool IsLocked { get; private set; }

    enum InsertState { None, Sliding, Turning, Inserted }

    XRGrabInteractable grab;
    Collider[] lockColliders;
    readonly HashSet<Key> ignoring = new HashSet<Key>();
    float lastToggleTime = -999f;
    string lastBlock;
    MaterialPropertyBlock mpb;

    // Insertion
    InsertState state;
    Key activeKey;
    Key needsExit;        // a key that was just pulled out must leave the hole before it can go in again
    float phaseTime;
    Vector3 startPos, seatedPos, turnedPos;           // keyhole-local
    Quaternion startRot, seatedRot, turnedRot;        // keyhole-local

    // Shackle pose, stored relative to Padlock_Root so it works however the model is nested or scaled.
    Vector3 closedPos;
    Quaternion closedRot;
    Vector3 hingePoint;
    float openAmount;     // 0 = closed, 1 = fully open
    float openTarget;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        lockColliders = GetComponentsInChildren<Collider>(true);

        if (keyhole == null) keyhole = transform.Find("Keyhole");

        if (shackle != null)
        {
            closedPos = transform.InverseTransformPoint(shackle.position);
            closedRot = Quaternion.Inverse(transform.rotation) * shackle.rotation;
            hingePoint = ComputeHinge();
        }

        SetLocked(startLocked, instant: true, notify: false);
    }

    void OnDisable()
    {
        if (activeKey != null) EndInsertion();
        foreach (Key k in ignoring) SetIgnore(k, false);
        ignoring.Clear();
    }

    void Update()
    {
        AnimateShackle();
        if (keyhole == null) return;

        UpdateCollisionIgnores();

        if (activeKey != null)
        {
            UpdateInsertion();
            return;
        }

        Key key = FindKeyInHole();
        if (key == null) { lastBlock = null; return; }

        if (Time.time - lastToggleTime < cooldown) { Block("cooling down"); return; }
        if (requireKeyHeld && !key.IsHeld) { Block("key is not being held"); return; }
        if (requireLockHeld && (grab == null || !grab.isSelected)) { Block("padlock is not being held (turn off Require Lock Held to allow this)"); return; }

        lastBlock = null;
        BeginInsertion(key);
    }

    // ---------- key detection ----------

    Key FindKeyInHole()
    {
        Key best = null;
        float bestDist = float.MaxValue;
        Vector3 hole = keyhole.position;
        Vector3 axis = keyhole.forward;

        foreach (Key k in Key.All)
        {
            if (k == null || k.IsInserted || !k.Matches(acceptedKeyId)) continue;

            float dist = Vector3.Distance(k.TipPosition, hole);

            if (k == needsExit)
            {
                if (dist > insertRadius) needsExit = null;
                continue;
            }

            if (dist > insertRadius || dist >= bestDist) continue;

            if (Vector3.Dot(k.InsertDirection, axis) < alignmentTolerance)
            {
                Block("key is in the keyhole but pointing the wrong way");
                continue;
            }

            best = k;
            bestDist = dist;
        }
        return best;
    }

    void Block(string reason)
    {
        if (!debugLog || reason == lastBlock) return;
        lastBlock = reason;
        Debug.Log($"{name}: key not turning, {reason}.", this);
    }

    // ---------- insertion ----------

    void BeginInsertion(Key key)
    {
        Transform v = key.Visual;
        if (v == null)
        {
            // No model to animate: just toggle.
            needsExit = key;
            lastToggleTime = Time.time;
            SetLocked(!IsLocked, instant: false, notify: true);
            return;
        }

        Vector3 axis = keyhole.forward;
        Vector3 seatPoint = keyhole.position + axis * insertDepth;

        // Rotate the key so it points straight into the hole (keeping its roll),
        // then place it so its tip sits at the seat point.
        Quaternion align = Quaternion.FromToRotation(key.InsertDirection, axis);
        Vector3 tipFromVisual = key.TipPosition - v.position;
        Quaternion seatedWorldRot = align * v.rotation;
        Vector3 seatedWorldPos = seatPoint - align * tipFromVisual;

        // Turn around the keyhole axis. Unlocking and locking turn opposite ways.
        Quaternion turn = Quaternion.AngleAxis(IsLocked ? turnAngle : -turnAngle, axis);
        Vector3 turnedWorldPos = seatPoint + turn * (seatedWorldPos - seatPoint);
        Quaternion turnedWorldRot = turn * seatedWorldRot;

        key.DetachVisual(keyhole);
        key.IsInserted = true;
        activeKey = key;

        startPos = v.localPosition;
        startRot = v.localRotation;
        seatedPos = keyhole.InverseTransformPoint(seatedWorldPos);
        seatedRot = Quaternion.Inverse(keyhole.rotation) * seatedWorldRot;
        turnedPos = keyhole.InverseTransformPoint(turnedWorldPos);
        turnedRot = Quaternion.Inverse(keyhole.rotation) * turnedWorldRot;

        state = InsertState.Sliding;
        phaseTime = 0f;
    }

    void UpdateInsertion()
    {
        Key k = activeKey;
        bool pulledOut = k == null || !k.isActiveAndEnabled ||
                         (requireKeyHeld && !k.IsHeld) ||
                         Vector3.Distance(k.TipPosition, keyhole.position) > pullOutDistance;
        if (pulledOut)
        {
            EndInsertion();
            return;
        }

        Transform v = k.Visual;
        phaseTime += Time.deltaTime;

        switch (state)
        {
            case InsertState.Sliding:
            {
                float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(phaseTime / Mathf.Max(slideTime, 0.01f)));
                v.localPosition = Vector3.Lerp(startPos, seatedPos, e);
                v.localRotation = Quaternion.Slerp(startRot, seatedRot, e);
                if (phaseTime >= slideTime) { state = InsertState.Turning; phaseTime = 0f; }
                break;
            }
            case InsertState.Turning:
            {
                float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(phaseTime / Mathf.Max(turnTime, 0.01f)));
                v.localPosition = Vector3.Lerp(seatedPos, turnedPos, e);
                v.localRotation = Quaternion.Slerp(seatedRot, turnedRot, e);
                if (phaseTime >= turnTime)
                {
                    state = InsertState.Inserted;
                    lastToggleTime = Time.time;
                    SetLocked(!IsLocked, instant: false, notify: true);
                }
                break;
            }
        }
    }

    void EndInsertion()
    {
        Key k = activeKey;
        activeKey = null;
        state = InsertState.None;
        if (k == null) return;

        k.IsInserted = false;
        k.ReturnVisual();
        needsExit = k;
    }

    // Lets the key pass through the padlock's colliders near the keyhole so it can reach it.
    void UpdateCollisionIgnores()
    {
        foreach (Key k in Key.All)
        {
            if (k == null) continue;
            bool near = k == activeKey ||
                        Vector3.Distance(k.TipPosition, keyhole.position) < approachDistance;
            bool isIgnoring = ignoring.Contains(k);
            if (near == isIgnoring) continue;

            SetIgnore(k, near);
            if (near) ignoring.Add(k);
            else ignoring.Remove(k);
        }
    }

    void SetIgnore(Key k, bool ignore)
    {
        if (k == null || k.Colliders == null) return;
        foreach (Collider a in k.Colliders)
            foreach (Collider b in lockColliders)
                if (a != null && b != null) Physics.IgnoreCollision(a, b, ignore);
    }

    // ---------- state ----------

    public void SetLocked(bool locked) => SetLocked(locked, instant: false, notify: true);

    void SetLocked(bool locked, bool instant, bool notify)
    {
        IsLocked = locked;
        openTarget = locked ? 0f : 1f;

        if (instant || animTime <= 0f)
        {
            openAmount = openTarget;
            ApplyShackle(openAmount);
        }

        if (statusLight != null)
        {
            if (mpb == null) mpb = new MaterialPropertyBlock();
            Color c = locked ? lockedColor : unlockedColor;
            statusLight.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", c);   // URP
            mpb.SetColor("_Color", c);       // Built-in
            statusLight.SetPropertyBlock(mpb);
        }

        if (!notify) return;

        if (debugLog) Debug.Log($"{name}: {(locked ? "LOCKED" : "UNLOCKED")}", this);
        if (locked) onLocked?.Invoke();
        else onUnlocked?.Invoke();
    }

    // ---------- shackle motion ----------

    void AnimateShackle()
    {
        if (shackle == null || Mathf.Approximately(openAmount, openTarget)) return;
        openAmount = Mathf.MoveTowards(openAmount, openTarget, Time.deltaTime / Mathf.Max(animTime, 0.01f));
        ApplyShackle(openAmount);
    }

    // First half: lift straight up. Second half: swing around the hinge leg.
    void ApplyShackle(float p)
    {
        if (shackle == null) return;

        float lift = openLift * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(p * 2f));
        float angle = openAngle * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(p * 2f - 1f));

        Quaternion swing = Quaternion.AngleAxis(angle, Vector3.up);   // Padlock_Root's up = along the body
        Vector3 pos = hingePoint + swing * (closedPos - hingePoint) + Vector3.up * lift;
        Quaternion rot = swing * closedRot;

        shackle.SetPositionAndRotation(transform.TransformPoint(pos), transform.rotation * rot);
    }

    // Hinge point in Padlock_Root space: the assigned hinge, or the center of one shackle leg.
    Vector3 ComputeHinge()
    {
        if (shackleHinge != null)
            return transform.InverseTransformPoint(shackleHinge.position);

        bool any = false;
        Vector3 min = Vector3.one * float.MaxValue;
        Vector3 max = Vector3.one * float.MinValue;

        foreach (MeshFilter mf in shackle.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            Bounds b = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3(
                    (i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 r = transform.InverseTransformPoint(mf.transform.TransformPoint(corner));
                min = Vector3.Min(min, r);
                max = Vector3.Max(max, r);
                any = true;
            }
        }

        if (!any) return closedPos;

        // Legs sit at the left/right edges of the shackle (Padlock_Root X axis).
        float inset = (max.x - min.x) * 0.12f;
        float x = hingeOnLeft ? min.x + inset : max.x - inset;
        return new Vector3(x, closedPos.y, (min.z + max.z) * 0.5f);
    }

    // Yellow: keyhole reach and key direction. Magenta: where the key tip ends up.
    // Green: the leg the shackle swings around.
    void OnDrawGizmosSelected()
    {
        if (keyhole != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(keyhole.position, insertRadius);
            Gizmos.DrawLine(keyhole.position - keyhole.forward * 0.06f, keyhole.position);

            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(keyhole.position + keyhole.forward * insertDepth, 0.003f);
        }

        if (shackle != null)
        {
            Vector3 h = Application.isPlaying ? hingePoint : ComputeHingeForGizmo();
            Vector3 w = transform.TransformPoint(h);
            Gizmos.color = Color.green;
            Gizmos.DrawLine(w - transform.up * 0.05f, w + transform.up * 0.08f);
        }
    }

    Vector3 ComputeHingeForGizmo()
    {
        Vector3 saved = closedPos;
        closedPos = transform.InverseTransformPoint(shackle.position);
        Vector3 h = ComputeHinge();
        closedPos = saved;
        return h;
    }
}
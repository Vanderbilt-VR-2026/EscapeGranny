
using UnityEngine;

// Attach this to the BED.
// This is a regular Unity component, NOT a NetworkBehaviour.
public class HidingSpot : MonoBehaviour
{
    [Header("Hiding Positions")]
    [SerializeField] private Transform hidePoint;
    [SerializeField] private Transform exitPoint;

    // Local occupancy only. The bed is not networked.
    public bool Occupied { get; private set; }

    // Reference to the local player's VR rig.
    public static LocalPlayerRig LocalRig;

    public Transform HidePoint => hidePoint;
    public Transform ExitPoint => exitPoint;

    public bool TryOccupy()
    {
        if (Occupied)
            return false;

        Occupied = true;
        return true;
    }

    public void Release()
    {
        Occupied = false;
    }
}

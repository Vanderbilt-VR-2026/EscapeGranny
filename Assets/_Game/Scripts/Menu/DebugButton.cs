// DebugButton.cs
//
// Temporary test script for menu Step 1: proves that pointing a controller ray at a
// world-space UI button and pulling the trigger reaches our code.
// Hook LogClick() up to a Button's OnClick in the Inspector. Delete once real menu buttons work.

using UnityEngine;

public class DebugButton : MonoBehaviour
{
    [SerializeField] private string message = "clicked";

    public void LogClick()
    {
        Debug.Log($"[DebugButton] {message}", this);
    }
}

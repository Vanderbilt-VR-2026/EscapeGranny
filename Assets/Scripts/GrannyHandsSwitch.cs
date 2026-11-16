using UnityEngine;

// Shows the Granny hand models instead of the controller models when the local player is Granny.
// Put this on the XR Origin and fill in the four slots.
public class GrannyHandsSwitch : MonoBehaviour
{
    [SerializeField] private GameObject leftControllerVisual;
    [SerializeField] private GameObject rightControllerVisual;
    [SerializeField] private GameObject leftHand;
    [SerializeField] private GameObject rightHand;

    private float nextCheck;

    private void Update()
    {
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + 0.25f;
        Apply(CurrentRole() == PlayerRole.Granny);
    }

    private static PlayerRole CurrentRole()
    {
        // Networked game: the local player's avatar knows the role.
        foreach (var body in FindObjectsByType<NetworkAvatarBody>(FindObjectsSortMode.None))
        {
            var net = body.Object;
            if (net != null && net.IsValid && net.HasStateAuthority) return body.Role;
        }
        // Offline / menu: the role chosen on the menu screen.
        var session = GameSession.Instance;
        return session != null ? session.SelectedRole : PlayerRole.None;
    }

    private void Apply(bool granny)
    {
        if (leftHand != null) leftHand.SetActive(granny);
        if (rightHand != null) rightHand.SetActive(granny);
        if (leftControllerVisual != null) leftControllerVisual.SetActive(!granny);
        if (rightControllerVisual != null) rightControllerVisual.SetActive(!granny);
    }
}
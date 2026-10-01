// GameSession.cs
//
// The one place that remembers this player's choices from the menu (role and weapon) and
// carries them into the house scene. It survives scene loads (DontDestroyOnLoad).
//
// Use from any script: GameSession.Instance.SelectedRole
// Put exactly one GameSession on a root GameObject in the menu scene. If a second one shows up
// (e.g. coming back to the menu), the extra copy destroys itself.

using UnityEngine;

public class GameSession : MonoBehaviour
{
    public static GameSession Instance { get; private set; }

    // Serialized so you can watch the values in the Inspector during Play mode.
    [SerializeField] private PlayerRole selectedRole = PlayerRole.None;
    [SerializeField] private WeaponType selectedWeapon = WeaponType.None;

    public PlayerRole SelectedRole
    {
        get => selectedRole;
        set
        {
            selectedRole = value;
            Debug.Log($"[GameSession] Role = {selectedRole}");
        }
    }

    public WeaponType SelectedWeapon
    {
        get => selectedWeapon;
        set
        {
            selectedWeapon = value;
            Debug.Log($"[GameSession] Weapon = {selectedWeapon}");
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        transform.SetParent(null); // DontDestroyOnLoad only works on root objects
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // Clear choices, e.g. when returning to the home screen after a round.
    public void ClearSelection()
    {
        SelectedRole = PlayerRole.None;
        SelectedWeapon = WeaponType.None;
    }
}

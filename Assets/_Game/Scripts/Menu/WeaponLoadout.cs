// WeaponLoadout.cs
//
// Granny's weapon picker. Tap a weapon to highlight it and read its description, then CONFIRM.
// CONFIRM stays greyed out until a weapon is picked. After CONFIRM the choice is locked
// (Granny can't change weapon), saved in GameSession, and the game starts.
//
// Setup: put this on GrannyLoadoutPanel. Fill the Options list (one entry per weapon) and hook up:
//   each weapon Button OnClick -> WeaponLoadout.SelectWeapon, with the number of the weapon:
//                                 1 = Axe, 2 = Knife, 3 = Gun, 4 = Bomb (the WeaponType values)
//   CONFIRM Button OnClick     -> WeaponLoadout.OnConfirmPressed

using System;
using UnityEngine;
using UnityEngine.UI;

public class WeaponLoadout : MonoBehaviour
{
    [Serializable]
    private class WeaponOption
    {
        public WeaponType weapon;
        public Button button;
        public GameObject highlight;
        [TextArea] public string description;
    }

    [SerializeField] private MenuController menuController;
    [SerializeField] private WeaponOption[] options;
    [SerializeField] private Text descriptionText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private string promptText = "Pick a weapon. You can't change it later.";

    private WeaponType pendingWeapon = WeaponType.None;
    private bool locked;

    // Runs every time the panel is shown, so coming back here starts fresh.
    private void OnEnable()
    {
        pendingWeapon = WeaponType.None;
        locked = false;
        Refresh();
    }

    // Takes an int (not WeaponType) so it can be picked in a Button's OnClick list.
    public void SelectWeapon(int weaponType)
    {
        if (locked) return;
        pendingWeapon = (WeaponType)weaponType;
        Refresh();
    }

    public void OnConfirmPressed()
    {
        if (locked || pendingWeapon == WeaponType.None) return;

        if (GameSession.Instance == null)
        {
            Debug.LogError("[WeaponLoadout] No GameSession in the scene.", this);
            return;
        }
        GameSession.Instance.SelectedWeapon = pendingWeapon;
        locked = true;
        Refresh();

        menuController.StartGame();
    }

    private void Refresh()
    {
        WeaponOption picked = null;
        foreach (var option in options)
        {
            bool isPicked = option.weapon == pendingWeapon;
            if (isPicked) picked = option;
            if (option.highlight != null) option.highlight.SetActive(isPicked);
            if (option.button != null) option.button.interactable = !locked;
        }

        if (descriptionText != null) descriptionText.text = picked != null ? picked.description : promptText;
        if (confirmButton != null) confirmButton.interactable = !locked && pendingWeapon != WeaponType.None;
    }
}

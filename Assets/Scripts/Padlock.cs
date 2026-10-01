using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit;

public class Padlock : MonoBehaviour
{
    [SerializeField] XRSocketInteractor socket;
    [SerializeField] Animator animator; // optional

    void OnEnable()  => socket.selectEntered.AddListener(OnKeyInserted);
    void OnDisable() => socket.selectEntered.RemoveListener(OnKeyInserted);

    void OnKeyInserted(SelectEnterEventArgs args)
    {
        Debug.Log("Padlock unlocked!");
        if (animator) animator.SetTrigger("Open");
    }
}
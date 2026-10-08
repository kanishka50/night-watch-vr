using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// A button in the world. Point at it and grab (G) to start the night again.
[RequireComponent(typeof(XRSimpleInteractable))]
public class RestartButton : MonoBehaviour
{
    XRSimpleInteractable interactable;

    void Awake() => interactable = GetComponent<XRSimpleInteractable>();
    void OnEnable() => interactable.selectEntered.AddListener(OnSelected);
    void OnDisable() => interactable.selectEntered.RemoveListener(OnSelected);

    void OnSelected(SelectEnterEventArgs args)
    {
        if (GameManager.Instance != null) GameManager.Instance.Restart();
    }
}

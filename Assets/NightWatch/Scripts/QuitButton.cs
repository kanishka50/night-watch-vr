using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// A button in the world beside RESTART. Point at it and grab to close the game.
// Esc also closes it, through an input action (not a key read in code), at any time.
[RequireComponent(typeof(XRSimpleInteractable))]
public class QuitButton : MonoBehaviour
{
    XRSimpleInteractable interactable;
    // Lives for the whole session, so Esc works before the results board shows too.
    static InputAction quitAction;

    void Awake() => interactable = GetComponent<XRSimpleInteractable>();
    void OnEnable() => interactable.selectEntered.AddListener(OnSelected);
    void OnDisable() => interactable.selectEntered.RemoveListener(OnSelected);

    void OnSelected(SelectEnterEventArgs args) => Quit();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void CreateQuitAction()
    {
        if (quitAction != null) return;
        quitAction = new InputAction("Quit", InputActionType.Button, "<Keyboard>/escape");
        // In the editor Esc is often pressed to free the mouse, so there it only says what it would do.
        quitAction.performed += _ =>
        {
            if (Application.isEditor) Debug.Log("Night Watch: Esc would close the Windows build here.");
            else Quit();
        };
        quitAction.Enable();
    }

    public static void Quit()
    {
        Debug.Log("Night Watch: quit.");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}

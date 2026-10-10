using UnityEngine;

// The start screen: the title, one story line, the controls in a few short lines, and a START
// button. It stands in front of the player at the start spot. The clock does not run until
// START is pressed, so the beginning is clear.
public class StartScreen : MonoBehaviour
{
    public static StartScreen Instance { get; private set; }

    [Tooltip("The panel (board, text and button). Hidden when the game starts.")]
    public GameObject panel;

    public bool IsShowing => panel != null && panel.activeSelf;

    void Awake() => Instance = this;

    // Called by the START button.
    public void Begin()
    {
        if (panel != null) panel.SetActive(false);
        ScreenFeedback.BlinkNow();
        GameManager.Instance?.BeginGame();
    }

    [ContextMenu("Test: press START")]
    void TestBegin() => Begin();
}

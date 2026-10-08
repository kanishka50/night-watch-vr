using TMPro;
using UnityEngine;

// The notice board in the hut. Kept short, because nobody reads a wall of text while elephants
// are coming: one goal line for the part of the night, plus the alert for this moment
// ("The fence is broken!") that other scripts send with NoticeBoard.Message(...).
// The controls are on the start screen, and each tool explains itself the first time it is grabbed.
public class NoticeBoard : MonoBehaviour
{
    public static NoticeBoard Instance { get; private set; }

    public TextMeshPro body;
    public TextMeshPro alert;
    public float alertSeconds = 7f;

    float alertTimer;

    // The alert showing right now, or empty. The task board on the ground shows it too.
    public static string CurrentAlert =>
        Instance != null && Instance.alert != null && Instance.alertTimer > 0f ? Instance.alert.text : "";

    void Awake()
    {
        Instance = this;
        if (alert != null) alert.text = "";
    }

    void Start()
    {
        var game = GameManager.Instance;
        if (game != null)
        {
            game.onPhaseChanged.AddListener(ShowPhase);
            ShowPhase(game.CurrentPhase);
        }
    }

    void Update()
    {
        if (alertTimer <= 0f) return;
        alertTimer -= Time.deltaTime;
        if (alertTimer <= 0f && alert != null) alert.text = "";
    }

    public static void Message(string text)
    {
        if (Instance == null || Instance.alert == null) return;
        Instance.alert.text = text;
        Instance.alertTimer = Instance.alertSeconds;
    }

    void ShowPhase(GameManager.Phase phase)
    {
        if (body == null) return;
        switch (phase)
        {
            case GameManager.Phase.Afternoon:
                body.text = "<b>AFTERNOON</b>\nBefore dark, do the 3 jobs on the board by the ladder.";
                break;
            case GameManager.Phase.Dusk:
                body.text = "<b>DUSK</b>\nNight is coming. Warn your neighbour with the phone.";
                break;
            case GameManager.Phase.Night:
                body.text = "<b>NIGHT</b>\nStay in the hut. Find elephants with the flashlight. Scare them away.";
                break;
            case GameManager.Phase.Dawn:
                body.text = "<b>DAWN</b>\nThe elephants are leaving.";
                break;
            case GameManager.Phase.Debrief:
                body.text = "<b>THE NIGHT IS OVER</b>\nLook north for your results.";
                break;
        }
    }
}

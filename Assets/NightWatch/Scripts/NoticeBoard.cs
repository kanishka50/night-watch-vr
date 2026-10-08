using TMPro;
using UnityEngine;

// The notice board on the hut wall. Shows the goal and a tip for each phase, plus short alerts
// ("The fence is broken!") that other scripts send with NoticeBoard.Message(...).
public class NoticeBoard : MonoBehaviour
{
    public static NoticeBoard Instance { get; private set; }

    public TextMeshPro body;
    public TextMeshPro alert;
    public float alertSeconds = 7f;

    float alertTimer;

    const string Controls =
        "<b>Controls</b>  Look: hold right mouse.  Grab/drop: cursor on it (turns yellow) + G.\n" +
        "Use: T.  Teleport: I (not while holding something).";

    const string Practise =
        "<b>Practise now</b>\n" +
        "1. Grab a fire torch, hold it near the fire pot and press T to light it.\n" +
        "2. A cracker: light it the same way, then press T again to throw it.\n" +
        "3. The fence only covers the NORTH. Elephants can come from east, west and south.\n\n";

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
                body.text =
                    "<b>NIGHT WATCH</b>\n" +
                    "Protect your crop, yourself and the elephants until dawn. Use only safe, legal methods.\n\n" +
                    "<b>Before dark</b> (the list is on the board by the ladder)\n" +
                    "Light the 3 bonfires at the open sides, switch on the fence energiser (east of the hut),\n" +
                    "then come up here. When all three are done, the sun sets.\n\n" +
                    Practise + Controls;
                break;
            case GameManager.Phase.Dusk:
                body.text =
                    "<b>DUSK</b>\n" +
                    "Elephants usually come after dark. Listen for branches cracking and low rumbles.\n" +
                    "Elephants are out tonight: use the <b>phone</b> on the rail to warn your neighbour.\n\n" +
                    Practise + Controls;
                break;
            case GameManager.Phase.Night:
                body.text =
                    "<b>NIGHT</b>  Stay in the hut. Find elephants by sound, then with the flashlight.\n" +
                    "Firecracker: strongest, makes them run. Throw it NEAR them, never at them.\n" +
                    "Fire torch: hold it toward them. They stop and slowly give up.\n" +
                    "Tin cans: best before they reach the field.\n" +
                    "Mix your tools: elephants get used to the same one.\n" +
                    "An elephant left alone in the field will charge the hut. The hut takes 3 hits.\n\n" + Controls;
                break;
            case GameManager.Phase.Dawn:
                body.text = "<b>DAWN</b>\nThe elephants are going back to the forest.";
                break;
            case GameManager.Phase.Debrief:
                body.text = "<b>THE NIGHT IS OVER</b>\nLook north (forward) for your results.";
                break;
        }
    }
}

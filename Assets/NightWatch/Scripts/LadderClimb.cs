using TMPro;
using UnityEngine;

// The hut ladder. Point at it and press G to climb down, or at the bottom to climb back up.
// Tools stay in the hut: while the player holds one, the "Climb down" label asks them to put it
// down first (a hand holding a tool cannot point at the ladder). Any tool left lying around goes
// back on the table when the player climbs down.
// In the afternoon the player works on the ground and climbs up before dark.
// Climbing down at night is a wrong choice: on the ground the player cannot escape, and elephants
// near a person on the ground get agitated. A charge that reaches the player there ends the night.
public class LadderClimb : PressTarget
{
    [Tooltip("True on the top of the ladder (climb down), false at the bottom (climb up).")]
    public bool goesDown = true;
    [Tooltip("Where the player's feet end up.")]
    public Transform destination;
    [Tooltip("The label over this handle. Top only: it changes while a tool is held.")]
    public TextMeshPro label;
    public string labelText = "Climb down";
    public string holdingText = "Tools stay in the hut\nG: put it down first";

    static bool loggedClimb;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay() => loggedClimb = false;

    bool showingHint;

    protected override void Awake()
    {
        base.Awake();
        // Older scenes: find the label by name.
        if (label == null && goesDown)
        {
            var found = GameObject.Find("Label_" + labelText);
            if (found != null) label = found.GetComponent<TextMeshPro>();
        }
        // Room for the two-line hint.
        if (label != null) label.rectTransform.sizeDelta = new Vector2(1f, 0.25f);
    }

    protected override void Update()
    {
        base.Update();
        if (!goesDown || label == null) return;
        bool hint = !PlayerState.OnGround && PlayerAction.AnyHeld;
        if (hint == showingHint) return;
        showingHint = hint;
        label.text = hint ? holdingText : labelText;
        label.color = hint ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.96f, 0.88f);
    }

    protected override void OnPressed()
    {
        if (destination == null) return;
        if (goesDown == PlayerState.OnGround) return;   // already there

        var limit = PlayAreaLimit.Instance;
        if (limit == null) return;
        limit.MoveHeadTo(destination.position);
        limit.SetOnGround(goesDown);

        if (!goesDown) return;
        int putBack = 0;
        foreach (var tool in PlayerAction.All.ToArray())
            if (tool.PutBack()) putBack++;
        string tools = putBack > 0 ? " Tools stay in the hut, so they are back on the table." : "";

        var game = GameManager.Instance;
        bool night = game != null && game.CurrentPhase == GameManager.Phase.Night;
        if (!night)
        {
            NoticeBoard.Message("You are on the ground. Climb back up before dark." + tools);
            return;
        }
        NoticeBoard.Message("You are on the ground! Elephants near you will become agitated. Climb back up." + tools);
        if (!loggedClimb)
        {
            loggedClimb = true;
            ActionLog.Record("Climbed down from the hut", false, "On the ground you cannot escape a charge, and your presence agitates elephants. Stay in the hut.");
        }
    }
}

using UnityEngine;

// The hut ladder. Point at it and press G to climb down, or at the bottom to climb back up.
// In the afternoon the player works on the ground and climbs up before dark.
// Climbing down at night is a wrong choice: on the ground the player cannot escape, and elephants
// near a person on the ground get agitated. A charge that reaches the player there ends the night.
public class LadderClimb : PressTarget
{
    [Tooltip("True on the top of the ladder (climb down), false at the bottom (climb up).")]
    public bool goesDown = true;
    [Tooltip("Where the player's feet end up.")]
    public Transform destination;

    static bool loggedClimb;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay() => loggedClimb = false;

    protected override void OnPressed()
    {
        if (destination == null) return;
        if (goesDown == PlayerState.OnGround) return;   // already there

        var limit = PlayAreaLimit.Instance;
        if (limit == null) return;
        limit.MoveHeadTo(destination.position);
        limit.SetOnGround(goesDown);

        if (!goesDown) return;
        var game = GameManager.Instance;
        bool night = game != null && game.CurrentPhase == GameManager.Phase.Night;
        if (!night)
        {
            NoticeBoard.Message("You are on the ground. Climb back up before dark.");
            return;
        }
        NoticeBoard.Message("You are on the ground! Elephants near you will become agitated. Climb back up.");
        if (!loggedClimb)
        {
            loggedClimb = true;
            ActionLog.Record("Climbed down from the hut", false, "On the ground you cannot escape a charge, and your presence agitates elephants. Stay in the hut.");
        }
    }
}

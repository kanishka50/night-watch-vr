using TMPro;
using UnityEngine;

// The afternoon jobs, done on the ground before dark, as a watcher really does:
//   1. light the bonfires at the open sides (east and south)
//   2. switch on the fence energiser
//   3. climb into the hut
// The player starts on the ground. The task board by the ladder ticks each job and counts down
// to dark. When all three are done, the sun sets a few seconds later. When night starts, each
// job goes into the debrief as a right or wrong choice.
public class GroundPrep : MonoBehaviour
{
    [Tooltip("Where the player starts, on the ground in the afternoon.")]
    public Transform startSpot;
    public bool startOnGround = true;
    [Tooltip("Seconds after every job is done before dusk comes.")]
    public float readyDelay = 10f;

    [Header("Task board by the ladder")]
    public TextMeshPro tasks;
    public TextMeshPro alert;

    float readyTimer = -1f;
    float boardTimer;
    bool judged;

    bool InHut => !PlayerState.OnGround;
    bool FenceOn => FenceEnergiser.Instance != null && FenceEnergiser.Instance.on;
    bool AllFiresLit => Bonfire.All.Count > 0 && Bonfire.LitCount >= Bonfire.All.Count;
    bool AllDone => AllFiresLit && FenceOn && InHut;

    void Start()
    {
        if (startOnGround && startSpot != null && PlayAreaLimit.Instance != null)
        {
            // The first placement: no fade-out first (nothing to hide yet).
            PlayAreaLimit.Instance.MoveHeadTo(startSpot.position, startSpot.rotation, onGround: true, fadeFirst: false);
        }
        var game = GameManager.Instance;
        if (game != null) game.onPhaseChanged.AddListener(OnPhase);
    }

    void Update()
    {
        var game = GameManager.Instance;
        if (game == null) return;

        // Ready early: no need to wait for the clock.
        if (game.Started && game.CurrentPhase == GameManager.Phase.Afternoon)
        {
            if (AllDone)
            {
                if (readyTimer < 0f)
                {
                    readyTimer = readyDelay;
                    NoticeBoard.Message("Everything is ready. The sun is setting.");
                }
                readyTimer -= Time.deltaTime;
                if (readyTimer <= 0f) game.SkipTo(GameManager.Phase.Dusk);
            }
            else readyTimer = -1f;
        }

        boardTimer -= Time.deltaTime;
        if (boardTimer > 0f) return;
        boardTimer = 0.25f;
        ShowBoard(game);
    }

    void ShowBoard(GameManager game)
    {
        if (tasks != null)
        {
            string header;
            if (game.CurrentPhase == GameManager.Phase.Afternoon || game.CurrentPhase == GameManager.Phase.Dusk)
            {
                float toDark = game.CurrentPhase == GameManager.Phase.Afternoon
                    ? game.afternoonLength - game.PhaseTime + game.duskLength
                    : game.duskLength - game.PhaseTime;
                int s = Mathf.CeilToInt(Mathf.Max(0f, toDark));
                header = $"<b>BEFORE DARK</b>     Dark in {s / 60}:{s % 60:00}";
            }
            else if (game.CurrentPhase == GameManager.Phase.Night)
                header = "<b>IT IS DARK</b>     Stay in the hut until dawn.";
            else header = "<b>DAWN</b>     The night is over.";

            int fires = Bonfire.LitCount;
            tasks.text =
                header + "\n\n" +
                Line(1, AllFiresLit, $"Light the {Bonfire.All.Count} bonfires: east and south ({fires} of {Bonfire.All.Count})") + "\n" +
                Line(2, FenceOn, "Switch on the fence energiser (box east of the hut)") + "\n" +
                Line(3, InHut, "Climb into the hut (the ladder)") + "\n\n" +
                "<size=80%>Teleport: thumbstick forward / I.   Light or switch: point at it + Grab (grip / G).</size>";
        }
        if (alert != null) alert.text = NoticeBoard.CurrentAlert;
    }

    // A numbered job. When it is done it turns green and says DONE.
    static string Line(int number, bool done, string text)
    {
        return done
            ? $"<color=#2F6B1E>{number}.  {text}   <b>DONE</b></color>"
            : $"{number}.  {text}";
    }

    void OnPhase(GameManager.Phase phase)
    {
        if (phase == GameManager.Phase.Night) Judge();
    }

    // Night has come: each afternoon job is a right or wrong choice in the debrief.
    void Judge()
    {
        if (judged) return;
        judged = true;

        if (AllFiresLit)
            ActionLog.Record("Lit a bonfire at every open side", true,
                "Fires make elephants stop before the field.");
        else
        {
            var unlit = new System.Collections.Generic.List<string>();
            foreach (var b in Bonfire.All)
                if (!b.IsLit && !b.BurntOut) unlit.Add(b.side);
            string sides = unlit.Count >= 3 ? "every open side" : $"the {string.Join(" and ", unlit)} side";
            ActionLog.Record($"Left {sides} without a bonfire", false,
                "Light a bonfire at every open side before dark.");
        }

        if (FenceOn)
            ActionLog.Record("Switched on the fence before dark", true,
                "A live fence turns elephants back safely.");
        else
            ActionLog.Record("Forgot to switch on the fence", false,
                "Switch the fence on before dark.");

        if (InHut)
            ActionLog.Record("Got into the hut before dark", true,
                "The hut keeps you safe and you can see the field.");
        else
        {
            ActionLog.Record("Still on the ground after dark", false,
                "Climb into the hut before dark.");
            NoticeBoard.Message("It is dark and you are still on the ground! Climb into the hut now.");
        }
    }

    [ContextMenu("Test: finish all ground jobs")]
    void TestFinishAll()
    {
        foreach (var b in Bonfire.All) b.LightFire();
        if (FenceEnergiser.Instance != null && !FenceEnergiser.Instance.on) FenceEnergiser.Instance.Switch(true);
        var hutSpot = GameObject.Find("HutSpot");
        if (InHut || hutSpot == null || PlayAreaLimit.Instance == null) return;
        PlayAreaLimit.Instance.MoveHeadTo(hutSpot.transform.position, onGround: false);
    }
}

using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

// Runs the story from afternoon to dawn, keeps the three scores, and decides the ending.
//
//   Afternoon (tutorial) -> Dusk -> Night (main raids) -> Dawn -> Debrief
//
// Scores (0 to 100): people safe, elephants safe, crop saved (from the crop plots).
// Some events end the night early: the player killed, or an elephant killed by hakka patas.
public class GameManager : MonoBehaviour
{
    public enum Phase { Afternoon, Dusk, Night, Dawn, Debrief }
    public enum Ending { None, EveryoneSafe, ElephantHurt, CropLost, FarmerHurt, ElephantKilled, PlayerDied, NeighbourKilled }

    public static GameManager Instance { get; private set; }

    [Header("Phase lengths in seconds (about 9 minutes in total)")]
    [Tooltip("Preparing the ground: bonfires, fence energiser, climbing into the hut.")]
    public float afternoonLength = 120f;
    public float duskLength = 50f;
    public float nightLength = 360f;
    public float dawnLength = 20f;

    [Header("Scores (0 to 100)")]
    public float peopleSafe = 100f;
    public float elephantsSafe = 100f;
    [Tooltip("Below this crop percentage the night counts as crop lost.")]
    public float cropGoal = 70f;
    [Tooltip("Charges the hut can take. The last one collapses it and ends the night.")]
    public int hutStrength = 3;
    [Tooltip("Health lost when an elephant hits the hut.")]
    public float hutHitDamage = 20f;
    [Tooltip("Health lost when an elephant reaches the player on the ground.")]
    public float groundHitDamage = 60f;
    [Tooltip("Seconds after the last wave has left before dawn comes.")]
    public float dawnDelayAfterLastWave = 20f;
    [Tooltip("Seconds the ending card (title and one line) stays before the results board.")]
    public float endingCardSeconds = 4.5f;

    [Header("Scene")]
    [Tooltip("The watch hut. Charging elephants run at it.")]
    public Transform hut;
    [Tooltip("Where the player stands to see the results if the hut is gone.")]
    public Transform debriefSpot;

    [Header("Events")]
    public UnityEvent<Phase> onPhaseChanged;
    public UnityEvent<Ending> onEnded;

    public Phase CurrentPhase { get; private set; } = Phase.Afternoon;
    // False while the start screen is showing: the clock does not run yet.
    public bool Started { get; private set; }
    public float PhaseTime { get; private set; }
    public Ending Result { get; private set; } = Ending.None;
    public string ResultTitle { get; private set; }
    public string ResultReason { get; private set; }
    // The one short line under the title (the same as on the ending card).
    public string ResultLine { get; private set; }
    public float CropSaved => CropPlot.AverageHealth;
    public int HutHits { get; private set; }
    public int GroupsArrived { get; private set; }
    public int GroupsTurnedBack { get; private set; }

    float TotalLength => afternoonLength + duskLength + nightLength + dawnLength;
    float elapsed;
    float endTimer = -1f;
    Ending pendingEnding;
    string pendingReason;
    string lastHurtReason;
    bool elephantKilled;
    bool showDebriefFromGround;
    float dawnTimer = -1f;

    // 0 at the start of the afternoon, 1 at the end of dawn. Drives the sky and light.
    public float DayProgress => Mathf.Clamp01(elapsed / TotalLength);

    // Seconds left until dawn starts.
    public float TimeToDawn => Mathf.Max(0f, afternoonLength + duskLength + nightLength - elapsed);

    public float CurrentPhaseLength
    {
        get
        {
            switch (CurrentPhase)
            {
                case Phase.Afternoon: return afternoonLength;
                case Phase.Dusk: return duskLength;
                case Phase.Night: return nightLength;
                case Phase.Dawn: return dawnLength;
                default: return float.MaxValue;
            }
        }
    }

    void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;   // in case a slow-motion moment was cut short by a restart
        ActionLog.Clear();
        PlayerState.OnGround = false;
        // Restart reloads the scene, but these "said it once" flags live for the whole launch.
        // Clear them so the next player gets the tool hints and messages too.
        PlayerAction.ResetHints();
        LadderClimb.ResetOnPlay();
        Bonfire.ResetOnPlay();
        ElephantAI.ResetOnPlay();
        ElephantAI.ResetToldAngry();
        if (hut != null) ElephantAI.HutCenter = hut.position;
    }

    void Start()
    {
        SetPhase(Phase.Afternoon);
        // With no start screen in the scene, start at once.
        Started = StartScreen.Instance == null || !StartScreen.Instance.IsShowing;
    }

    // Called by the START button.
    public void BeginGame() => Started = true;

    void Update()
    {
        if (CurrentPhase == Phase.Debrief || !Started) return;

        if (endTimer >= 0f)
        {
            // A short pause after an early ending, so the player sees what happened.
            endTimer -= Time.deltaTime;
            if (endTimer < 0f) Finish(pendingEnding, pendingReason);
            return;
        }

        elapsed += Time.deltaTime;
        PhaseTime += Time.deltaTime;

        // Every wave has been dealt with: dawn comes early.
        if (dawnTimer >= 0f && CurrentPhase == Phase.Night)
        {
            dawnTimer -= Time.deltaTime;
            if (dawnTimer < 0f)
            {
                elapsed = afternoonLength + duskLength + nightLength;
                SetPhase(Phase.Dawn);
                return;
            }
        }

        if (PhaseTime >= CurrentPhaseLength) NextPhase();
    }

    void NextPhase()
    {
        switch (CurrentPhase)
        {
            case Phase.Afternoon: SetPhase(Phase.Dusk); break;
            case Phase.Dusk: SetPhase(Phase.Night); break;
            case Phase.Night: SetPhase(Phase.Dawn); break;
            case Phase.Dawn: FinishAtDawn(); break;
        }
    }

    void SetPhase(Phase next)
    {
        CurrentPhase = next;
        PhaseTime = 0f;
        if (next == Phase.Dawn) ElephantAI.SendAllHome();
        onPhaseChanged?.Invoke(next);
    }

    // ---- Things that change the scores ----

    // peopleSafe is the player's health (0 to 100).
    public void HurtPlayer(float amount, string reason)
    {
        if (CurrentPhase == Phase.Debrief || endTimer >= 0f) return;
        peopleSafe = Mathf.Max(0f, peopleSafe - amount);
        lastHurtReason = reason;
        NoticeBoard.Message(reason);
        ScreenFeedback.Flash(Color.red, 0.5f);
        if (peopleSafe <= 0f) PlayerKilled(reason);
    }

    // The player falls and the screen fades out; the night ends.
    public void PlayerKilled(string reason)
    {
        if (CurrentPhase == Phase.Debrief || endTimer >= 0f) return;
        peopleSafe = 0f;
        lastHurtReason = reason;
        NoticeBoard.Message(reason);
        ScreenFeedback.KnockDown(true);
        showDebriefFromGround = true;
        EndEarly(Ending.PlayerDied, reason, 3.5f);
    }

    // An elephant reached the player on the ground: knocked down, badly hurt. A second time kills.
    public void PlayerTrampled()
    {
        if (peopleSafe - groundHitDamage <= 0f)
        {
            PlayerKilled("An elephant trampled you on the ground. Never leave the hut while elephants are near.");
            return;
        }
        ScreenFeedback.KnockDown(false);
        HurtPlayer(groundHitDamage, "An elephant knocked you down! Get back up the ladder.");
    }

    // An elephant crashed into the hut. The hut shakes; on the last hit it collapses.
    public void HutCharged(Vector3 from)
    {
        HutHits++;
        SoundBank.PlayAt(SoundBank.Bang, ElephantAI.HutCenter, 0.8f);
        SoundBank.PlayAt(SoundBank.BranchCrack, ElephantAI.HutCenter, 1f);
        if (HutHits >= hutStrength)
        {
            if (HutDamage.Instance != null) HutDamage.Instance.Collapse(from);
            PlayerKilled("The hut collapsed after " + hutStrength + " charges. Deter elephants early, before they get bold or angry.");
            return;
        }
        if (HutDamage.Instance != null) HutDamage.Instance.TakeHit(HutHits, from);
        HurtPlayer(hutHitDamage, $"An elephant hit the hut ({HutHits} of {hutStrength})! Drive elephants back before they reach it.");
    }

    // Called by the arrival manager for each group that comes in at night.
    public void GroupArrived() => GroupsArrived++;

    // Called by an elephant (a lone one or a herd leader) that the player turned back.
    public void ElephantTurnedBack(string who)
    {
        GroupsTurnedBack++;
        NoticeBoard.Message(who + " turned back to the forest!");
    }

    // Called by the arrival manager when the last wave has left.
    public void AllWavesDone()
    {
        if (CurrentPhase == Phase.Night && dawnTimer < 0f)
        {
            dawnTimer = dawnDelayAfterLastWave;
            NoticeBoard.Message("The last elephants have gone. Dawn is coming.");
        }
    }

    public void HurtElephant(float amount)
    {
        elephantsSafe = Mathf.Max(0f, elephantsSafe - amount);
    }

    // The bull reached the neighbour outside their house and killed them. The night ends, as when
    // an elephant is killed: a slow fade, then the ending.
    public void NeighbourKilled()
    {
        NoticeBoard.Message("The bull has killed your neighbour.");
        ScreenFeedback.FadeOut(6f);
        EndEarly(Ending.NeighbourKilled, "Your neighbour went outside not knowing an elephant was there, and the bull killed them. " +
            "Warn the people around you early: one phone call would have kept them inside.", 6.5f);
    }

    public void ElephantKilled(string reason)
    {
        elephantKilled = true;
        elephantsSafe = 0f;
        NoticeBoard.Message("An elephant has died.");
        ScreenFeedback.FadeOut(5f);
        EndEarly(Ending.ElephantKilled, reason, 5.5f);
    }

    // ---- Endings ----

    void EndEarly(Ending ending, string reason, float delay)
    {
        if (CurrentPhase == Phase.Debrief || endTimer >= 0f) return;
        pendingEnding = ending;
        pendingReason = reason;
        endTimer = delay;
    }

    void FinishAtDawn()
    {
        if (elephantKilled)
            Finish(Ending.ElephantKilled, "An elephant died tonight. Hakka patas is illegal and cruel, and it does not solve the conflict.");
        else if (peopleSafe < 100f)
            Finish(Ending.FarmerHurt, lastHurtReason);
        else if (elephantsSafe < 100f)
            Finish(Ending.ElephantHurt, "An elephant was hit and hurt. Scare elephants from a distance, never injure them: a hurt elephant becomes more dangerous.");
        else if (CropSaved < cropGoal)
            Finish(Ending.CropLost, "No one was hurt, but elephants ate too much of the crop. Act early and watch every side, not only the fence.");
        else
            Finish(Ending.EveryoneSafe, "Every elephant went back to the forest. No one was hurt and most of the crop is saved.");
    }

    void Finish(Ending ending, string reason)
    {
        Result = ending;
        ResultReason = reason;
        ResultTitle = TitleFor(ending);
        ResultLine = CardLineFor(ending);
        CurrentPhase = Phase.Debrief;
        PhaseTime = 0f;
        ElephantAI.SendAllHome();
        onPhaseChanged?.Invoke(Phase.Debrief);
        StartCoroutine(EndingSequence(ending));
    }

    // Every ending gets a clear moment before the results board:
    //   fade to black > the title and one line in front of the eyes, with a sound > fade in on the board.
    IEnumerator EndingSequence(Ending ending)
    {
        // Real seconds: a slow-motion moment may still be running.
        ScreenFeedback.ClearFlash();
        ScreenFeedback.FadeOut(1.5f);
        yield return new WaitForSecondsRealtime(1.6f);

        // After a fall the hut may be gone: stand the player on the ground facing the results.
        if (showDebriefFromGround && debriefSpot != null) MovePlayerTo(debriefSpot);

        // Morning, whatever time the night ended: the sky, sun and fog follow DayProgress.
        elapsed = TotalLength;

        EndingCard.Show(ResultTitle, CardLineFor(ending), IsGood(ending));
        yield return new WaitForSecondsRealtime(endingCardSeconds);
        EndingCard.Hide();

        onEnded?.Invoke(ending);
        ScreenFeedback.FadeIn(1.5f);
    }

    static bool IsGood(Ending ending) => ending == Ending.EveryoneSafe || ending == Ending.CropLost;

    static void MovePlayerTo(Transform spot)
    {
        var limit = PlayAreaLimit.Instance;
        if (limit == null) return;
        limit.MoveHeadTo(spot.position, spot.rotation, onGround: true);
    }

    // Jumps forward to the start of dusk or night (the sky follows). Used when the player is
    // ready early, and by the test buttons.
    public void SkipTo(Phase phase)
    {
        if (CurrentPhase == Phase.Debrief || endTimer >= 0f || phase <= CurrentPhase) return;
        // A skip (test button) also gets past the start screen.
        if (!Started && StartScreen.Instance != null) StartScreen.Instance.Begin();
        Started = true;
        if (phase == Phase.Dusk) elapsed = afternoonLength;
        else if (phase == Phase.Night) elapsed = afternoonLength + duskLength;
        else return;
        SetPhase(phase);
    }

    static string TitleFor(Ending ending)
    {
        switch (ending)
        {
            case Ending.EveryoneSafe: return "Everyone is safe";
            case Ending.ElephantHurt: return "An elephant was hurt";
            case Ending.CropLost: return "Safe, but the crop is lost";
            case Ending.FarmerHurt: return "You survived, but you were hurt";
            case Ending.PlayerDied: return "You did not survive the night";
            case Ending.NeighbourKilled: return "Your neighbour was killed";
            case Ending.ElephantKilled: return "An elephant was killed";
            default: return "";
        }
    }

    // The one short line on the ending card (the full reason is on the results board).
    static string CardLineFor(Ending ending)
    {
        switch (ending)
        {
            case Ending.EveryoneSafe: return "The herd went back to the forest. You, your neighbour and the elephants are all alive.";
            case Ending.ElephantHurt: return "Everyone lived, but an elephant was injured tonight.";
            case Ending.CropLost: return "No one was hurt, but most of the paddy is gone.";
            case Ending.FarmerHurt: return "You made it to dawn, but you were injured.";
            case Ending.PlayerDied: return "An elephant reached you. No one can outrun an elephant in the dark.";
            case Ending.NeighbourKilled: return "Your neighbour went outside in the dark, and the bull was there.";
            case Ending.ElephantKilled: return "The others stood by its body, calling, until they had to leave.";
            default: return "";
        }
    }

    public void Restart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // Test buttons: in Play mode, right-click the Game Manager header in the Inspector.
    [ContextMenu("Test: an elephant hits the hut")]
    void TestHutHit() => HutCharged(ElephantAI.HutCenter + new Vector3(0f, 0f, 3f));

    [ContextMenu("Test: an elephant knocks the player down")]
    void TestTrample() => PlayerTrampled();

    [ContextMenu("Test: skip to dusk")]
    void TestSkipToDusk() => SkipTo(Phase.Dusk);

    [ContextMenu("Test: skip to night")]
    void TestSkipToNight() => SkipTo(Phase.Night);

    [ContextMenu("Test: end the night now (the dawn ending)")]
    void TestEndNow()
    {
        if (CurrentPhase != Phase.Debrief && endTimer < 0f) FinishAtDawn();
    }

    [ContextMenu("Test: bring the herd now")]
    void TestHerd()
    {
        var arrivals = FindFirstObjectByType<ArrivalManager>();
        if (arrivals != null) arrivals.SpawnNow(1);
    }

    [ContextMenu("Test: bring the fence breaker now")]
    void TestFenceBreaker()
    {
        var arrivals = FindFirstObjectByType<ArrivalManager>();
        if (arrivals != null) arrivals.SpawnFenceBreaker();
    }

    [ContextMenu("Test: the nearest elephant eats hakka patas")]
    void TestBait()
    {
        var elephant = ElephantAI.NearestActive(ElephantAI.HutCenter);
        if (elephant != null) elephant.Kill();
        else Debug.Log("Night Watch: no elephant in the scene yet. Use 'Test: bring the herd now' first.");
    }
}

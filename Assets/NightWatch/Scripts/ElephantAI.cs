using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// The elephant's brain: a state machine driven by two values.
//
//   resolve   : how determined it is to reach the crop. Safe deterrents lower it.
//               At 0 it gives up and goes back to the forest (the player turned it back).
//               It only grows back after a while with no deterrent.
//   agitation : how angry or stressed it is (0 to 100). Harm raises it (hit by a cracker,
//               a cracker bursting very close, a person on the ground nearby, its calf scared).
//               At 60 it is Agitated, at 100 it Charges.
//
//   Approach --north side--> AtFence --(shock)--> Retreat
//                              |  fence breaker pushes until the section breaks
//                              v
//   Approach --open side--> Raiding (eats a crop plot)
//   Approach/AtFence/Raiding --resolve 0--> Retreat           (turned back: good)
//   Approach/AtFence/Raiding --agitation 60--> Agitated --100--> Charge   (bad)
//   Raiding, left alone too long --> Charge at the hut (it smells the stored rice)
//   Agitated --agitation below 40--> back to what it was doing
//   any --eats hakka patas--> Killed                           (worst)
//   The rest of its herd then gathers round the body for a while before leaving.
//
// Each deterrent works differently (see Scare): a firecracker makes it run, a fire torch holds
// it in place, tin cans work best before it reaches the field. Each elephant gets used to each
// kind of deterrent (habituation), so mixing them works better than repeating one.
//
// Herds: one leader holds the brain. Followers copy the leader's state and pass any scare or
// harm on to it. Scaring or hurting a calf also angers the leader (the mother protects it).
[RequireComponent(typeof(NavMeshAgent))]
public class ElephantAI : MonoBehaviour
{
    public enum State { Approach, AtFence, Raiding, Agitated, Charge, Retreat, Killed }

    // How a deterrent makes the elephant react.
    public enum Reaction { Pause, Flee }

    public static readonly List<ElephantAI> All = new List<ElephantAI>();
    public static Vector3 HutCenter = Vector3.zero;

    [Header("The two values that drive the state")]
    [Tooltip("How determined it is to reach the crop. At 0 it goes home.")]
    public float resolve = 100f;
    [Tooltip("How angry or stressed it is.")]
    [Range(0f, 100f)] public float agitation = 10f;
    public float agitatedLevel = 60f;
    public float calmDownLevel = 40f;
    public float chargeLevel = 100f;
    [Tooltip("Anger lost per second. 2.5 = an angry elephant calms down in about 8 s.")]
    public float agitationDecayPerSecond = 2.5f;
    public float resolveRecoveryPerSecond = 0.5f;
    [Tooltip("Resolve only grows back after this many seconds with no deterrent.")]
    public float recoveryDelay = 10f;
    [Tooltip("Agitation added per second while the player stands on the ground nearby.")]
    public float groundedPlayerAgitationPerSecond = 7f;
    public float groundedPlayerRange = 20f;

    [Header("Left alone in the field")]
    [Tooltip("Seconds of raiding with no deterrent before it gets bold and charges the hut.")]
    public float boldAfterSeconds = 45f;

    [Header("Role")]
    public bool isFenceBreaker;
    public bool isCalf;
    [Tooltip("Set for herd followers. Empty for a leader or a lone elephant.")]
    public ElephantAI leader;
    public float fencePushDamagePerSecond = 8f;
    [Tooltip("Push damage per second on a fence with no power (energiser off). 100 health = about 4 s.")]
    public float deadFencePushDamagePerSecond = 25f;
    [Tooltip("Seconds a normal elephant stands at the fence before the shock sends it away.")]
    public float fenceTestTime = 6f;
    public float eatRateMultiplier = 1f;

    [Header("Speeds")]
    public float walkSpeed = 1.3f;
    public float fleeSpeed = 4.5f;
    public float chargeSpeed = 6f;
    public float retreatSpeed = 2.2f;

    [Header("Habituation")]
    [Tooltip("How much a deterrent's effect drops each time, per point of strength.")]
    public float habituationPerStrength = 0.005f;
    public float habituationRecoveryPerSecond = 0.01f;
    [Tooltip("A deterrent never drops below this share of its full effect.")]
    public float minimumEffect = 0.25f;

    [Header("Optional")]
    public ElephantBody body;
    public AudioSource voice;
    [Tooltip("Loudness of every trumpet call (0 to 1).")]
    public float trumpetVolume = 0.75f;
    public Animator animator;

    public State CurrentState { get; private set; } = State.Approach;

    [Header("Now (read only): click a live elephant in Play mode to see what it is doing")]
    [SerializeField] string stateNow;
    [SerializeField] string whyStandingStill;
    static bool toldAngry;
    public Vector3 HomePoint { get; set; }
    public bool FromNorth { get; set; }
    public bool IsActive => CurrentState != State.Retreat && CurrentState != State.Killed;
    public bool IsLeader => leader == null || leader == this;
    // When it last ran away from a scare (a firecracker).
    public float LastFleeTime => lastFleeTime;

    NavMeshAgent agent;
    float stateTimer, flinchTimer, soundTimer, wanderTimer, trumpetTimer, followTimer;
    float startResolve;
    float lastDeterredTime;
    float lastFleeTime = float.NegativeInfinity;
    bool needsResume;
    State stateBeforeAgitated = State.Approach;
    FenceSection fence;
    CropPlot plot;
    Transform bait;
    bool dying;
    Collider hitbox;
    // A place it goes to instead of the field (the neighbour's rice store, or the neighbour).
    Vector3? detour;
    float detourSpeed;
    // One knock-down at a time, shared by all elephants (the player needs time to get up).
    static float nextContactTime;
    // Radius of the hut's NavMesh obstacle. The walk map adds the elephant's own radius (1.2 m),
    // so its centre stays 5 m from the hut centre and the head (2.4 m in front) about 2.6 m: the
    // trunk reaches the stilts and the ladder but never goes in. Was 3 (the trunk went inside).
    public const float HutKeepOut = 3.8f;

    // Where the trunk reaches the crop: the head is about 2.4 m in front of the centre (less for a calf).
    Vector3 Mouth => transform.position + transform.forward * (2.4f * transform.localScale.z);
    ElephantAI mournTarget;
    float mournUntil;
    bool mournArrived;
    bool leaving;
    // Where a herd member walks, in the leader's space (x right, z forward). Set by ArrivalManager
    // (HerdSlot, CalfSlot) so the long bodies never overlap.
    [HideInInspector] public Vector3 followOffset;
    readonly Dictionary<string, float> habituation = new Dictionary<string, float>();
    readonly List<string> habituationKeys = new List<string>();

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (body == null) body = GetComponent<ElephantBody>();
        if (voice == null) voice = GetComponent<AudioSource>();
        hitbox = GetComponent<Collider>();
    }

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    void Start()
    {
        startResolve = resolve;
        lastDeterredTime = Time.time;
        if (followOffset == Vector3.zero) followOffset = HerdSlot(1);
        soundTimer = Random.Range(1f, 3f);
        if (IsLeader) EnterState(State.Approach);
    }

    // An adult body is 4.4 m long and 2 m wide. Members walk in pairs behind the leader, one each
    // side: 3 m out and 6 m back per pair (the leader's rear is 1.9 m behind its centre, a
    // member's head 2.5 m in front of its own).
    public static Vector3 HerdSlot(int member)
    {
        float side = member % 2 == 1 ? 1f : -1f;
        int row = (member + 1) / 2;
        return new Vector3(side * Random.Range(2.8f, 3.4f), 0f, -6f * row + Random.Range(-0.5f, 0.5f));
    }

    // A calf walks close beside its mother, on the side the first member leaves free.
    public static Vector3 CalfSlot() => new Vector3(-2.6f, 0f, Random.Range(-1.5f, -0.5f));

    void Update()
    {
        float dt = Time.deltaTime;
        stateTimer += dt;
        flinchTimer -= dt;
        soundTimer -= dt;
        trumpetTimer -= dt;
        RecoverHabituation(dt);

        if (CurrentState == State.Killed) return;
        CheckContactWithPlayer();
        if (mournTarget != null)
        {
            UpdateMourn();
            return;
        }
        if (bait != null && IsActive)
        {
            GoForBait();
            return;
        }
        if (!IsLeader)
        {
            FollowLeader(dt);
            return;
        }

        if (CurrentState != State.Charge && CurrentState != State.Retreat)
        {
            agitation = Mathf.Max(0f, agitation - agitationDecayPerSecond * dt);
            if (Time.time - lastDeterredTime > recoveryDelay)
                resolve = Mathf.Min(startResolve, resolve + resolveRecoveryPerSecond * dt);
            if (PlayerState.OnGround && PlayerState.FlatDistanceTo(transform.position) < groundedPlayerRange)
                agitation = Mathf.Min(100f, agitation + groundedPlayerAgitationPerSecond * dt);
        }

        if (needsResume && flinchTimer <= 0f)
        {
            needsResume = false;
            SetStateDestination();
        }

        // In any state, the feet flatten the rice under the body.
        var underfoot = CropPlot.At(transform.position);
        if (underfoot != null) underfoot.TrampleUnder(transform);

        switch (CurrentState)
        {
            case State.Approach: UpdateApproach(); break;
            case State.AtFence: UpdateAtFence(dt); break;
            case State.Raiding: UpdateRaiding(dt); break;
            case State.Agitated: UpdateAgitated(); break;
            case State.Charge: UpdateCharge(); break;
            case State.Retreat: UpdateRetreat(); break;
        }

        // Shared rules for the three "working" states.
        if (CurrentState == State.Approach || CurrentState == State.AtFence || CurrentState == State.Raiding)
        {
            if (agitation >= agitatedLevel)
            {
                EnterState(State.Agitated);
            }
            else if (resolve <= 0f)
            {
                // Turned back by the player (the fence shock reports itself separately).
                if (Time.time - lastDeterredTime < 15f) GameManager.Instance?.ElephantTurnedBack(name);
                EnterState(State.Retreat);
            }
        }

        MakeSounds();
        ShowStatus();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void ResetOnPlay() => nextContactTime = 0f;

    // A person on the ground who touches an elephant's body is knocked down, whatever the
    // elephant is doing. Elephants do not walk through people.
    void CheckContactWithPlayer()
    {
        if (!PlayerState.OnGround || dying || hitbox == null || Time.time < nextContactTime) return;
        var game = GameManager.Instance;
        if (game == null || game.CurrentPhase == GameManager.Phase.Debrief) return;
        var head = PlayerState.Head;
        if (head == null) return;

        var probe = head.position;
        probe.y = hitbox.bounds.center.y;
        if (FlatDistance(hitbox.ClosestPoint(probe), probe) > 0.5f) return;

        nextContactTime = Time.time + 6f;
        game.PlayerTrampled();
        // It pushes past and moves away from the person.
        if (IsLeader && IsActive && CurrentState != State.Charge) Flee(head.position);
    }

    // For testing: what it is doing now, and why it is standing still (shown in the Inspector).
    void ShowStatus()
    {
        stateNow = CurrentState.ToString();
        bool still = agent.isOnNavMesh && agent.velocity.sqrMagnitude < 0.04f;
        if (!still) whyStandingStill = "";
        else if (CurrentState == State.Agitated)
            whyStandingStill = $"Angry: stands until anger drops below {calmDownLevel:0} (now {agitation:0})";
        else if (flinchTimer > 0f) whyStandingStill = $"Stopped by a deterrent ({flinchTimer:0.0} s left)";
        else if (CurrentState == State.Raiding) whyStandingStill = "Eating the crop";
        else if (CurrentState == State.AtFence) whyStandingStill = "At the fence";
        else if (detour.HasValue) whyStandingStill = "At the house";
        else if (agent.pathPending) whyStandingStill = "Finding a path";
        else if (!agent.hasPath) whyStandingStill = "No path (stuck?)";
        else whyStandingStill = "Has a path but is not moving (stuck?)";
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void ResetToldAngry() => toldAngry = false;

    // ---------------- States ----------------

    void UpdateApproach()
    {
        if (flinchTimer > 0f) return;
        if (FromNorth)
        {
            if (fence == null) fence = FenceSection.Nearest(transform.position);
            if (fence == null || fence.IsBroken)
            {
                FromNorth = false;
                SetStateDestination();
                return;
            }
            if (stateTimer > 1f && Arrived(1.5f)) EnterState(State.AtFence);
        }
        else if (detour.HasValue)
        {
            // At the house it stands and searches for the stored rice.
            if (stateTimer > 1f && Arrived(1.5f))
            {
                if (agent.isOnNavMesh) agent.isStopped = true;
                FaceTowards(detour.Value);
            }
        }
        else
        {
            if (plot == null || plot.health <= 0f)
            {
                plot = CropPlot.NearestWithCrop(transform.position);
                SetStateDestination();
            }
            if (plot != null && plot.Contains(transform.position)) EnterState(State.Raiding);
        }
    }

    // Sends it to a place instead of the field (used by the neighbour's house). A speed above
    // walking (a run at a person) also shows the angry look: ears wide, trunk up.
    public void GoTo(Vector3 point, float speed = -1f)
    {
        detour = point;
        detourSpeed = speed > 0f ? speed : walkSpeed;
        if (body != null && detourSpeed > walkSpeed * 1.5f) body.SetAgitated(true);
        // While a deterrent holds it (a pause), wait; it resumes on its own afterwards.
        if (CurrentState == State.Approach && flinchTimer <= 0f) SetStateDestination();
    }

    // Back to the normal plan: the field.
    public void ClearDetour()
    {
        if (!detour.HasValue) return;
        detour = null;
        if (body != null && CurrentState != State.Agitated && CurrentState != State.Charge) body.SetAgitated(false);
        if (CurrentState == State.Approach && flinchTimer <= 0f) SetStateDestination();
    }

    // A warning display: it rears with its trunk up, flaps its ears and trumpets, standing still.
    public void Alarm(float seconds)
    {
        if (body != null)
        {
            body.Rear(seconds);
            body.FlapEars(seconds);
        }
        Play(SoundBank.Trumpet, 1f);
        Pause(seconds);
    }

    void UpdateAtFence(float dt)
    {
        if (fence == null || fence.IsBroken)
        {
            FromNorth = false;
            EnterState(State.Approach);
            return;
        }
        FaceTowards(fence.transform.position);

        bool live = FenceEnergiser.IsOn;
        if (isFenceBreaker || !live)
        {
            // A dead fence is only wire: any elephant pushes it down, and fast.
            bool pushing = flinchTimer <= 0f;
            if (body != null) body.SetPushing(pushing);
            float damage = live ? fencePushDamagePerSecond : deadFencePushDamagePerSecond;
            if (pushing) fence.Push(damage * dt, transform.position);
        }
        else if (stateTimer >= fenceTestTime)
        {
            // The electric fence does its job: one shock and the elephant gives up.
            fence.Zap(fence.transform.position + Vector3.up * 0.5f);
            NoticeBoard.Message("The electric fence turned an elephant back.");
            EnterState(State.Retreat);
        }
    }

    void UpdateRaiding(float dt)
    {
        if (plot == null || plot.health <= 0f)
        {
            plot = CropPlot.NearestWithCrop(transform.position);
            if (plot == null)
            {
                EnterState(State.Retreat);   // nothing left to eat
                return;
            }
            wanderTimer = 0f;
        }

        // A deterred elephant stops eating.
        if (flinchTimer <= 0f && plot.Contains(transform.position))
        {
            plot.Eat(dt * eatRateMultiplier, Mouth);
            if (body != null) body.Eat(0.3f);   // the Eat clip while it stands and eats
        }

        // Left alone for too long: it gets bold and goes for the stored rice in the hut.
        if (stateTimer > boldAfterSeconds && Time.time - lastDeterredTime > boldAfterSeconds)
        {
            NoticeBoard.Message("An elephant smelled the stored rice and is charging the hut!");
            EnterState(State.Charge);
            return;
        }

        wanderTimer -= dt;
        if (wanderTimer <= 0f && flinchTimer <= 0f)
        {
            wanderTimer = Random.Range(5f, 9f);
            MoveTo(plot.RandomPoint(), walkSpeed * 0.6f);
        }
    }

    void UpdateAgitated()
    {
        if (agent.isOnNavMesh) agent.isStopped = true;
        FaceTowards(ThreatPosition());
        if (trumpetTimer <= 0f)
        {
            Play(SoundBank.Trumpet, 1f);
            trumpetTimer = Random.Range(10f, 14f);   // once when it gets angry, then now and then
        }
        if (agitation >= chargeLevel) EnterState(State.Charge);
        else if (agitation < calmDownLevel) EnterState(stateBeforeAgitated);
    }

    void UpdateCharge()
    {
        followTimer -= Time.deltaTime;
        var target = ChargeTarget();
        if (followTimer <= 0f)
        {
            followTimer = 0.3f;
            MoveTo(target, chargeSpeed);
        }
        // The hut's obstacle keeps the body out, so "at the hut" means as close as it can get:
        // the trunk then touches the stilts.
        bool reachedPlayer = PlayerState.OnGround && PlayerState.FlatDistanceTo(transform.position) < 4f;
        bool reachedHut = !PlayerState.OnGround && FlatDistance(transform.position, HutCenter) < HutKeepOut + agent.radius + 0.5f;
        if (reachedPlayer || reachedHut)
        {
            Play(SoundBank.Trumpet, 1f);   // it trumpets as it hits
            if (reachedPlayer)
                GameManager.Instance?.PlayerTrampled();
            else
                GameManager.Instance?.HutCharged(transform.position);
            agitation = 20f;
            resolve = 0f;
            EnterState(State.Retreat);
        }
    }

    void UpdateRetreat()
    {
        if (stateTimer > 1f && Arrived(2f)) gameObject.SetActive(false);
    }

    void EnterState(State next)
    {
        if (next == State.Agitated && CurrentState != State.Agitated)
            stateBeforeAgitated = (CurrentState == State.AtFence || CurrentState == State.Raiding) ? CurrentState : State.Approach;

        // For testing: each change of state shows in the Console.
        if (next != CurrentState) Debug.Log($"Night Watch: {name}: {CurrentState} > {next}");
        CurrentState = next;
        stateTimer = 0f;
        needsResume = false;
        if (agent.isOnNavMesh) agent.isStopped = false;
        if (body != null)
        {
            body.SetAgitated(next == State.Agitated || next == State.Charge);
            body.SetPushing(false);
        }

        switch (next)
        {
            case State.Approach: SetStateDestination(); break;
            case State.AtFence: if (agent.isOnNavMesh) agent.isStopped = true; break;
            case State.Raiding: wanderTimer = 0f; break;
            case State.Agitated:
                trumpetTimer = 0f;
                if (!toldAngry)
                {
                    toldAngry = true;
                    NoticeBoard.Message("An elephant is angry! It stands and trumpets. Give it space until it calms down.");
                }
                break;
            case State.Charge:
                Play(SoundBank.Trumpet, 1f);
                followTimer = 0f;
                break;
            case State.Retreat:
                // Runs if it was just scared by a bang, walks otherwise.
                MoveTo(HomePoint, Time.time - lastFleeTime < 6f ? fleeSpeed : retreatSpeed);
                break;
        }

        if (animator != null) animator.SetTrigger(next.ToString());
    }

    void SetStateDestination()
    {
        switch (CurrentState)
        {
            case State.Approach:
                if (FromNorth)
                {
                    if (fence == null) fence = FenceSection.Nearest(transform.position);
                    if (fence != null) MoveTo(fence.ApproachPoint, walkSpeed);
                }
                else if (detour.HasValue)
                {
                    MoveTo(detour.Value, detourSpeed > 0f ? detourSpeed : walkSpeed);
                }
                else
                {
                    if (plot == null || plot.health <= 0f) plot = CropPlot.NearestWithCrop(transform.position);
                    if (plot != null) MoveTo(plot.RandomPoint(), walkSpeed);
                }
                break;
            case State.Raiding:
                if (plot != null) MoveTo(plot.RandomPoint(), walkSpeed * 0.6f);
                break;
            case State.Retreat:
                MoveTo(HomePoint, retreatSpeed);
                break;
            case State.AtFence:
            case State.Agitated:
                if (agent.isOnNavMesh) agent.isStopped = true;
                break;
        }
    }

    // ---------------- Herd followers ----------------

    void FollowLeader(float dt)
    {
        bool leaderGone = leader == null || !leader.gameObject.activeInHierarchy;
        var target = leaderGone ? State.Retreat : leader.CurrentState;
        if (target == State.Charge) target = State.Agitated;   // only the leader charges
        if (target == State.Killed) target = State.Agitated;   // stands by, alarmed, until it mourns
        if (target == State.AtFence) target = State.Approach;
        if (leaving) target = State.Retreat;

        if (target != CurrentState)
        {
            CurrentState = target;
            stateTimer = 0f;
            if (body != null) body.SetAgitated(target == State.Agitated);
            if (target == State.Retreat)
            {
                float speed = leaderGone ? retreatSpeed : leader.agent.speed;
                MoveTo(HomePoint + followOffset, Mathf.Max(speed, retreatSpeed));
            }
        }

        switch (CurrentState)
        {
            case State.Retreat:
                if (stateTimer > 1f && Arrived(2f)) gameObject.SetActive(false);
                break;
            case State.Agitated:
                if (agent.isOnNavMesh) agent.isStopped = true;
                FaceTowards(ThreatPosition());
                break;
            default:
                followTimer -= dt;
                if (followTimer <= 0f)
                {
                    followTimer = 0.4f;
                    MoveTo(leader.transform.TransformPoint(followOffset), leader.agent.speed);
                }
                if (CurrentState == State.Raiding && leader.flinchTimer <= 0f)
                {
                    var here = CropPlot.At(transform.position);
                    if (here != null)
                    {
                        here.Eat(dt * eatRateMultiplier, Mouth);
                        if (body != null) body.Eat(0.3f);
                    }
                }
                break;
        }
    }

    // ---------------- What the player can do to it ----------------

    // A safe deterrent. Returns the effect it had on resolve.
    //   reaction        : Pause (stop where it is) or Flee (run away from the source)
    //   angerIfClose    : share of the strength added to agitation when the source is under 4 m away
    public float Scare(string type, float strength, Vector3 source, Reaction reaction = Reaction.Pause, float angerIfClose = 0f)
    {
        if (!IsActive) return 0f;
        if (body != null) body.FlapEars();

        if (!IsLeader)
        {
            // A scared calf makes its mother more defensive.
            if (isCalf && leader != null) leader.agitation = Mathf.Min(100f, leader.agitation + strength * 0.3f);
            return leader != null ? leader.Scare(type, strength, source, reaction, angerIfClose) : 0f;
        }
        if (CurrentState == State.Charge) return 0f;

        // Noise works best on an elephant that has not reached the field yet.
        if (type == "tin cans" && CurrentState == State.Approach) strength *= 2f;

        float factor = habituation.TryGetValue(type, out var f) ? f : 1f;
        float applied = strength * factor;
        habituation[type] = Mathf.Max(minimumEffect, factor - strength * habituationPerStrength);
        if (!habituationKeys.Contains(type)) habituationKeys.Add(type);

        resolve -= applied;
        lastDeterredTime = Time.time;

        // Too close: fright turns into anger.
        if (angerIfClose > 0f && FlatDistance(transform.position, source) < 4f)
            agitation = Mathf.Min(100f, agitation + strength * angerIfClose);

        if (CurrentState != State.Agitated)
        {
            if (reaction == Reaction.Flee)
            {
                // Chased off the fence: it will have to walk back to it.
                if (CurrentState == State.AtFence) EnterState(State.Approach);
                Flee(source);
            }
            else
            {
                Pause(1f);   // at the fence this also stops a fence breaker pushing
            }
        }
        return applied;
    }

    // Real harm, for example being hit by a cracker.
    public void Harm(float amount)
    {
        if (CurrentState == State.Killed) return;
        if (body != null) body.FlapEars(2.5f);
        if (!IsLeader)
        {
            if (leader != null)
            {
                if (isCalf) leader.agitation = Mathf.Min(100f, leader.agitation + amount * 0.8f);
                leader.Harm(amount);
            }
            return;
        }
        agitation = Mathf.Min(100f, agitation + amount);
    }

    // Hakka patas was put in the field: this elephant goes for it.
    public void GoToBait(Transform target)
    {
        if (bait != target) baitSince = Time.time;   // called again every second: keep the first time
        bait = target;
    }
    float baitSince;

    // How far in front of the elephant's centre the trunk picks food off the ground (Eat clip:
    // just ahead of the front feet), for a full-size elephant.
    const float BaitReach = 1.7f;
    // In the Eat clip (from its start): the trunk takes the food, then the food is in the mouth.
    const float BaitPickUpAt = 1.9f, BaitInMouthAt = 3.1f;

    // Walks to where its trunk reaches the bait, facing it (not on top of it).
    void GoForBait()
    {
        var hakka = bait.GetComponent<HakkaPatas>();
        if (hakka != null && hakka.Taken)
        {
            bait = null;   // another elephant got there first
            return;
        }
        var toBait = bait.position - transform.position;
        toBait.y = 0f;
        float reach = BaitReach * transform.localScale.z;
        var eatPoint = bait.position - (toBait.sqrMagnitude > 0.01f ? toBait.normalized : transform.forward) * reach;
        followTimer -= Time.deltaTime;
        if (followTimer <= 0f)
        {
            followTimer = 0.5f;
            MoveTo(eatPoint, walkSpeed * 1.5f);
        }
        bool there = FlatDistance(transform.position, eatPoint) < 0.7f
            || (toBait.magnitude < reach + 0.8f && Vector3.Angle(transform.forward, toBait) < 40f)
            // As near as the walk map lets it get (the bait fell somewhere it cannot stand).
            || (Time.time - baitSince > 2f && Arrived(0.3f) && toBait.magnitude < 6f);
        if (there)
        {
            bait = null;
            if (hakka != null) hakka.Taken = true;
            Kill(hakka);
        }
    }

    public void Kill(HakkaPatas hakka = null)
    {
        if (dying || CurrentState == State.Killed) return;
        StartCoroutine(DieFromBait(hakka));
    }

    // Eats the bait (the Eat clip: the trunk picks it up and puts it in the mouth), then the blast
    // in the mouth, then the Die clip. The herd gathers round.
    IEnumerator DieFromBait(HakkaPatas hakka)
    {
        dying = true;
        CurrentState = State.Killed;
        if (agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }

        // Turns to face the food.
        if (hakka != null)
            for (float t = 0f; t < 1f; t += Time.deltaTime)
            {
                FaceTowards(hakka.transform.position);
                yield return null;
            }

        if (body != null) body.StartEating(BaitInMouthAt + 1f);
        yield return new WaitForSeconds(BaitPickUpAt);
        // The trunk wraps the food and lifts it to the mouth.
        if (hakka != null && body != null && body.TrunkTip != null) hakka.Carry(body.TrunkTip);
        yield return new WaitForSeconds(BaitInMouthAt - BaitPickUpAt);

        // It bites down: the blast is in the mouth.
        var head = body != null ? body.HeadPoint : transform.TransformPoint(new Vector3(0f, 1.8f, 2.4f));
        if (hakka != null) hakka.Explode();
        SoundBank.PlayAt(SoundBank.Bang, head, 1f, 8f);
        BurstFlash.At(head, new Color(1f, 0.7f, 0.4f), 12f, 15f, 0.4f);
        Effects.Dust(head, new Color(0.12f, 0.12f, 0.12f, 0.85f), 1.3f);
        Play(SoundBank.Trumpet, 1f);
        // The Die clip rears and staggers by itself; an older model rears (Attack), then tips over.
        bool dieClip = body != null && body.HasDieClip;
        if (dieClip) body.FallOver();
        else if (body != null) body.Rear();
        StartCoroutine(SlowMotion());
        yield return new WaitForSeconds(0.9f);

        if (body != null && !dieClip) body.FallOver();
        foreach (var other in Herd()) other.Mourn(this);
        yield return new WaitForSeconds(2.8f);

        GameManager.Instance?.ElephantKilled("An elephant ate the hakka patas and died. Hakka patas is illegal and cruel, and it does not solve the conflict.");
    }

    // Test button: in Play mode, select an elephant, right-click the Elephant AI header.
    [ContextMenu("Test: eats hakka patas")]
    void TestEatBait() => Kill();

    // A moment of slow motion at the blast.
    static IEnumerator SlowMotion()
    {
        Time.timeScale = 0.35f;
        yield return new WaitForSecondsRealtime(1.6f);
        Time.timeScale = 1f;
    }

    // The other elephants in the same herd.
    List<ElephantAI> Herd()
    {
        var herd = new List<ElephantAI>();
        var head = IsLeader ? this : leader;
        foreach (var e in All)
        {
            if (e == this || !e.IsActive) continue;
            if (e == head || e.leader == head) herd.Add(e);
        }
        return herd;
    }

    // Stays by a dead herd member for a while, calling, then leaves.
    public void Mourn(ElephantAI dead)
    {
        if (!IsActive) return;
        mournTarget = dead;
        mournUntil = Time.time + 9f;
        mournArrived = false;
        bait = null;
        trumpetTimer = Random.Range(1f, 3f);
    }

    void UpdateMourn()
    {
        if (Time.time > mournUntil || mournTarget == null)
        {
            mournTarget = null;
            LeaveForHome();
            return;
        }
        if (!mournArrived)
        {
            mournArrived = true;
            var side = transform.position - mournTarget.transform.position;
            side.y = 0f;
            MoveTo(mournTarget.transform.position + side.normalized * 3.5f, walkSpeed);
        }
        FaceTowards(mournTarget.transform.position);
        if (body != null && Random.value < 0.01f) body.FlapEars();
        if (trumpetTimer <= 0f)
        {
            trumpetTimer = Random.Range(3f, 5f);
            Play(SoundBank.Rumble, 1f);
        }
    }

    void LeaveForHome()
    {
        leaving = true;
        resolve = 0f;
        agitation = 0f;
        if (IsLeader)
        {
            EnterState(State.Retreat);
        }
        else
        {
            CurrentState = State.Retreat;
            stateTimer = 0f;
            MoveTo(HomePoint + followOffset, retreatSpeed);
        }
    }

    // At dawn, or when the night ends, every elephant goes back to the forest.
    public void GoHome()
    {
        if (!IsActive) return;
        mournTarget = null;
        bait = null;
        LeaveForHome();
    }

    public static void SendAllHome()
    {
        foreach (var e in All.ToArray()) e.GoHome();
    }

    public static ElephantAI NearestActive(Vector3 point)
    {
        ElephantAI best = null;
        float bestDistance = float.MaxValue;
        foreach (var e in All)
        {
            if (!e.IsActive) continue;
            float d = FlatDistance(point, e.transform.position);
            if (d < bestDistance)
            {
                bestDistance = d;
                best = e;
            }
        }
        return best;
    }

    // Scares every active elephant within range, weaker with distance.
    // Returns how many elephants it reached.
    public static int ScareAllNear(Vector3 point, float range, string type, float strength,
        Reaction reaction = Reaction.Pause, float angerIfClose = 0f)
    {
        int count = 0;
        foreach (var e in All.ToArray())
        {
            if (!e.IsActive) continue;
            float d = FlatDistance(point, e.transform.position);
            if (d > range) continue;
            float falloff = Mathf.Lerp(1f, 0.4f, d / range);
            e.Scare(type, strength * falloff, point, reaction, angerIfClose);
            count++;
        }
        return count;
    }

    public static int CountActive()
    {
        int n = 0;
        foreach (var e in All)
            if (e.IsActive) n++;
        return n;
    }

    // ---------------- Helpers ----------------

    // Runs fast, away from the source, for a few seconds.
    void Flee(Vector3 source)
    {
        var away = transform.position - source;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = transform.position - HutCenter;
        var target = transform.position + away.normalized * 12f;
        if (NavMesh.SamplePosition(target, out var hit, 5f, NavMesh.AllAreas))
            MoveTo(hit.position, fleeSpeed);
        // A startled trumpet, but only after the bang that scared it, so both are heard clearly.
        CancelInvoke(nameof(FleeTrumpet));
        Invoke(nameof(FleeTrumpet), 0.8f);
        lastFleeTime = Time.time;
        flinchTimer = 4f;
        needsResume = true;
    }

    void Pause(float seconds)
    {
        if (agent.isOnNavMesh) agent.isStopped = true;
        flinchTimer = Mathf.Max(flinchTimer, seconds);
        needsResume = true;
    }

    void RecoverHabituation(float dt)
    {
        foreach (var key in habituationKeys)
            habituation[key] = Mathf.Min(1f, habituation[key] + habituationRecoveryPerSecond * dt);
    }

    Vector3 ThreatPosition()
    {
        var head = PlayerState.Head;
        if (PlayerState.OnGround && head != null) return head.position;
        return HutCenter;
    }

    Vector3 ChargeTarget()
    {
        var head = PlayerState.Head;
        if (PlayerState.OnGround && head != null)
            return new Vector3(head.position.x, 0f, head.position.z);
        // A point just outside the hut, on the side the elephant is coming from.
        var dir = transform.position - HutCenter;
        dir.y = 0f;
        return HutCenter + dir.normalized * (HutKeepOut + agent.radius);
    }

    void MoveTo(Vector3 point, float speed)
    {
        if (!agent.isOnNavMesh) return;
        agent.isStopped = false;
        agent.speed = speed;
        agent.SetDestination(point);
    }

    bool Arrived(float tolerance)
    {
        return agent.isOnNavMesh && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + tolerance;
    }

    void FaceTowards(Vector3 point)
    {
        var dir = point - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) return;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), 90f * Time.deltaTime);
    }

    static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    // Branch cracks while walking in the forest, rumbles when near. These are the clues
    // the player uses to find the elephant in the dark.
    void MakeSounds()
    {
        if (soundTimer > 0f) return;
        soundTimer = Random.Range(4f, 9f);
        if (CurrentState == State.Approach && FlatDistance(transform.position, HutCenter) > 14f)
            Play(SoundBank.BranchCrack, 1f);
        else if (CurrentState == State.Approach || CurrentState == State.AtFence || CurrentState == State.Raiding)
            Play(SoundBank.Rumble, 0.9f);
    }

    void FleeTrumpet() => Play(SoundBank.Trumpet, 0.8f);

    void Play(AudioClip clip, float volume)
    {
        if (voice == null || clip == null) return;
        // The trumpet is loud and high: kept lower so the other sounds can still be heard.
        if (clip == SoundBank.Trumpet) volume *= trumpetVolume;
        voice.PlayOneShot(clip, volume);
    }
}

using UnityEngine;
using UnityEngine.AI;

// How the elephant's body moves. With the real model (an Animator with Idle, Walk, Run, Attack,
// Eat and Die, from elephant.glb) the clip follows what the AI does:
//  - Walk or Run by speed, played faster or slower so the feet do not slide
//  - Eat while it stands eating the crop (or the hakka patas bait)
//  - Idle when standing (at the fence, stopped by a deterrent)
//  - Attack when it rears (the moment of a blast) and while it pushes the fence
//  - FallOver (hakka patas): the Die clip (rears, staggers, kneels, rolls onto its side), held
//    on its last frame. A model with no Die clip is tipped over in code instead.
// Without a model (old stand-in shapes) the ears, trunk and body are moved in code instead.
// Tusks can be hidden: most Asian elephant cows (and most Sri Lankan bulls) have no tusks.
public class ElephantBody : MonoBehaviour
{
    public Transform bodyRoot;
    public Transform earLeft;
    public Transform earRight;
    public Transform trunk;

    [Header("Real model")]
    public Animator animator;
    [Tooltip("The model's skin; its second material slot is the tusks.")]
    public SkinnedMeshRenderer skin;
    [Tooltip("Walking speed (m/s) at which the Walk clip plays at normal speed, for a full-size elephant.")]
    public float walkClipSpeed = 1.7f;
    [Tooltip("Running speed (m/s) at which the Run clip plays at normal speed, for a full-size elephant.")]
    public float runClipSpeed = 4.6f;
    [Tooltip("Faster than this (m/s) it runs instead of walking.")]
    public float runFrom = 2.2f;

    string playing;
    Material[] withTusks;

    float flapTimer;
    float eatTimer;
    float rearTimer;
    bool agitated;
    bool pushing;
    bool fallen;
    float fallTime;
    NavMeshAgent agent;
    Vector3 bodyStart;
    CapsuleCollider hitbox;
    Vector3 hitboxStart;
    // The fall rolls the body round its left side at the ground (half the body's width out), so
    // it ends lying on its side on top of the ground. It used to roll round the feet and sink
    // 0.6 to 1.4 m into the ground.
    static readonly Vector3 RollPivot = new Vector3(-0.9f, 0f, 0f);

    // The model's own clips (the older model had no Eat and no Die).
    bool hasEat, hasDie;
    // Two bones in the middle of the body: the hit box follows them while the Die clip plays.
    Transform hips, chest;
    // The head, and the tip of the trunk (it carries the hakka patas bait to the mouth).
    Transform head, trunkTip;

    public Transform TrunkTip => trunkTip;
    // Where the blast happens: the head (the bait is in the mouth).
    public Vector3 HeadPoint => head != null ? head.position : transform.TransformPoint(new Vector3(0f, 1.8f, 2.4f));

    public bool HasDieClip => animator != null && hasDie;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (bodyRoot != null) bodyStart = bodyRoot.localPosition;
        hitbox = GetComponent<CapsuleCollider>();
        if (hitbox != null) hitboxStart = hitbox.center;
        if (skin != null) withTusks = skin.sharedMaterials;
        if (animator != null)
        {
            hasEat = animator.HasState(0, Animator.StringToHash("Eat"));
            hasDie = animator.HasState(0, Animator.StringToHash("Die"));
            foreach (var bone in animator.GetComponentsInChildren<Transform>(true))
            {
                if (bone.name == "hips") hips = bone;
                else if (bone.name == "chest") chest = bone;
                else if (bone.name == "head") head = bone;
                else if (bone.name == "trunk6") trunkTip = bone;
            }
        }
    }

    // Tusks shown or hidden (the tusk part of the mesh gets no material, so it is not drawn).
    public void SetTusks(bool on)
    {
        if (skin == null || withTusks == null || withTusks.Length < 2) return;
        skin.sharedMaterials = on ? withTusks : new[] { withTusks[0] };
    }

    public void FlapEars(float seconds = 1.5f) => flapTimer = Mathf.Max(flapTimer, seconds);
    public void SetAgitated(bool on) => agitated = on;
    public void SetPushing(bool on) => pushing = on;
    public void Eat(float seconds) => eatTimer = seconds;

    // Eats from the start of the Eat clip: foot scuff, trunk down (about 1.8 s), food up to the
    // mouth (about 3 s), chewing.
    public void StartEating(float seconds)
    {
        eatTimer = seconds;
        if (animator == null || !hasEat) return;
        animator.speed = 1f;
        animator.Play("Eat", 0, 0f);
        playing = "Eat";
    }
    public void Rear(float seconds = 0.9f)
    {
        rearTimer = seconds;
        if (animator == null) return;
        // The Attack clip from the start: head up, trunk raised.
        rearTimer = Mathf.Max(seconds, 1.6f);
        animator.speed = 1f;
        animator.Play("Attack", 0, 0f);
        playing = "Attack";
    }

    public void FallOver()
    {
        fallen = true;
        fallTime = 0f;
        if (!HasDieClip) return;
        animator.speed = 1f;
        animator.CrossFadeInFixedTime("Die", 0.2f);
        playing = "Die";
    }

    void Update()
    {
        float t = Time.time;
        float dt = Time.deltaTime;
        flapTimer -= dt;
        eatTimer -= dt;
        rearTimer -= dt;

        if (fallen && HasDieClip)
        {
            UpdateDie(dt);
            return;
        }
        if (fallen)
        {
            UpdateFall(dt);
            if (animator != null)
            {
                Play("Idle", 1f);
                if (fallTime > 2.8f) animator.speed = 0f;   // lies still
            }
            return;
        }

        if (animator != null)
        {
            UpdateModel();
            return;
        }

        // Ears: fast flapping when scared, wide when agitated or rearing, small sway otherwise.
        float ear = flapTimer > 0f ? 35f * Mathf.Sin(t * 14f) : ((agitated || rearTimer > 0f) ? 55f : 5f * Mathf.Sin(t * 1.5f));
        SetEars(ear);

        // Trunk: down to eat, raised when agitated or rearing, gentle swing otherwise.
        float trunkAngle = eatTimer > 0f ? 35f + 10f * Mathf.Sin(t * 4f)
                         : (agitated || rearTimer > 0f) ? -70f
                         : 10f * Mathf.Sin(t * 1.2f);
        if (trunk != null) trunk.localRotation = Quaternion.Euler(trunkAngle, 0f, 0f);

        if (bodyRoot == null) return;
        if (rearTimer > 0f)
        {
            // Head thrown up: the body tips back.
            float k = Mathf.Sin(Mathf.Clamp01(1f - rearTimer / 0.9f) * Mathf.PI);
            bodyRoot.localRotation = Quaternion.Euler(-20f * k, 0f, 0f);
            bodyRoot.localPosition = bodyStart;
            return;
        }

        // Rocks forward while pushing the fence, bobs while walking.
        float speed = agent != null ? agent.velocity.magnitude : 0f;
        float bob = speed > 0.1f ? 0.05f * Mathf.Sin(t * speed * 4f) : 0f;
        float rock = pushing ? 0.25f * Mathf.Max(0f, Mathf.Sin(t * 3f)) : 0f;
        bodyRoot.localRotation = Quaternion.identity;
        bodyRoot.localPosition = bodyStart + new Vector3(0f, bob, rock);
    }

    // Picks the clip from what the elephant is doing.
    void UpdateModel()
    {
        float size = Mathf.Max(0.2f, transform.lossyScale.x);
        float speed = agent != null ? agent.velocity.magnitude : 0f;
        if (rearTimer > 0f || pushing) Play("Attack", 1f);
        else if (speed > runFrom * size) Play("Run", Mathf.Clamp(speed / (runClipSpeed * size), 0.6f, 1.5f));
        else if (speed > 0.15f) Play("Walk", Mathf.Clamp(speed / (walkClipSpeed * size), 0.5f, 1.6f));
        else if (eatTimer > 0f && hasEat) PlayLooped("Eat");
        else Play("Idle", 1f);
    }

    // Plays a clip that must repeat, even if the file marks it as play-once.
    void PlayLooped(string state)
    {
        Play(state, 1f);
        var now = animator.GetCurrentAnimatorStateInfo(0);
        if (now.IsName(state) && !now.loop && now.normalizedTime >= 1f) animator.Play(state, 0, 0f);
    }

    // The Die clip plays once and holds its last frame. The hit box follows the body down, so the
    // player bumps into the body where it lies.
    void UpdateDie(float dt)
    {
        fallTime += dt;
        var now = animator.GetCurrentAnimatorStateInfo(0);
        if (now.IsName("Die") && now.normalizedTime >= 0.98f) animator.speed = 0f;
        if (hitbox != null && hips != null && chest != null)
        {
            var centre = transform.InverseTransformPoint((hips.position + chest.position) * 0.5f);
            centre.y = Mathf.Max(centre.y, hitbox.radius * 0.9f);
            hitbox.center = centre;
        }
    }

    void Play(string state, float speed)
    {
        if (playing != state)
        {
            animator.CrossFadeInFixedTime(state, 0.25f);
            playing = state;
        }
        animator.speed = speed;
    }

    // The fall, in stages over about 7 seconds.
    void UpdateFall(float dt)
    {
        fallTime += dt;
        float f = fallTime;
        float pitch = 0f, roll = 0f, drop = 0f, slide = 0f;

        // 0.0 to 0.8 s: staggers from side to side.
        roll = 8f * Mathf.Sin(f * 9f) * Mathf.Clamp01(1f - f / 0.8f);

        // 0.6 to 1.3 s: front legs buckle, the body tips forward and sinks.
        float knees = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 1.3f, f));
        pitch = 18f * knees;
        drop = 0.5f * knees;

        // 1.3 to 2.8 s: rolls onto its side.
        float side = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.3f, 2.8f, f));
        roll += 85f * side;
        pitch = Mathf.Lerp(pitch, 5f, side);
        drop = Mathf.Lerp(drop, 0f, side);

        // After that: a few slow breaths that fade away.
        float breath = 0f;
        if (f > 2.8f)
        {
            float fade = Mathf.Clamp01(1f - (f - 2.8f) / 4f);
            breath = 0.04f * fade * Mathf.Sin((f - 2.8f) * 2.2f);
        }

        var turn = Quaternion.Euler(pitch, 0f, roll);
        var lift = new Vector3(slide, -drop + breath, 0f);
        if (bodyRoot != null)
        {
            bodyRoot.localRotation = turn;
            bodyRoot.localPosition = RollPivot + turn * (bodyStart - RollPivot) + lift;
        }
        // The hit box lies down with the body, so the player bumps into the body where it lies.
        if (hitbox != null) hitbox.center = RollPivot + turn * (hitboxStart - RollPivot) + lift;
        // Ears flat, trunk limp.
        SetEars(Mathf.Lerp(5f, -10f, side));
        if (trunk != null) trunk.localRotation = Quaternion.Euler(Mathf.Lerp(0f, 40f, side), 0f, 0f);
    }

    void SetEars(float angle)
    {
        if (earLeft != null) earLeft.localRotation = Quaternion.Euler(0f, -angle, 0f);
        if (earRight != null) earRight.localRotation = Quaternion.Euler(0f, angle, 0f);
    }
}

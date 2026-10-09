using UnityEngine;
using UnityEngine.AI;

// How the elephant's body moves. With the real model (an Animator with Idle, Walk, Run and
// Attack, from the village's elephant.glb) the clip follows what the AI does:
//  - Walk or Run by speed, played faster or slower so the feet do not slide
//  - Idle when standing (at the fence, eating, stopped by a deterrent)
//  - Attack when it rears (the moment of a blast) and while it pushes the fence
//  - FallOver (hakka patas): staggers, front legs buckle, rolls onto its side, then still
//    (no death clip yet, so the whole model is tipped over in code)
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

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (bodyRoot != null) bodyStart = bodyRoot.localPosition;
        if (skin != null) withTusks = skin.sharedMaterials;
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
    }

    void Update()
    {
        float t = Time.time;
        float dt = Time.deltaTime;
        flapTimer -= dt;
        eatTimer -= dt;
        rearTimer -= dt;

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
        else Play("Idle", 1f);
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
        drop = Mathf.Lerp(drop, 0.75f, side);
        slide = 0.8f * side;

        // After that: a few slow breaths that fade away.
        float breath = 0f;
        if (f > 2.8f)
        {
            float fade = Mathf.Clamp01(1f - (f - 2.8f) / 4f);
            breath = 0.04f * fade * Mathf.Sin((f - 2.8f) * 2.2f);
        }

        if (bodyRoot != null)
        {
            bodyRoot.localRotation = Quaternion.Euler(pitch, 0f, roll);
            bodyRoot.localPosition = bodyStart + new Vector3(slide, -drop + breath, 0f);
        }
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

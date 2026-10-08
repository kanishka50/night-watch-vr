using UnityEngine;
using UnityEngine.AI;

// Simple body movement for the stand-in elephant, made in code:
//  - ears flap when scared, spread wide and trunk lifted when agitated
//  - body rocks while pushing the fence, bobs while walking
//  - Eat: trunk down to the ground
//  - Rear: head and trunk thrown up (the moment of the blast)
//  - FallOver: staggers, front legs buckle, rolls onto its side, a few slow breaths, then still
// A real model with an Animator replaces this later.
public class ElephantBody : MonoBehaviour
{
    public Transform bodyRoot;
    public Transform earLeft;
    public Transform earRight;
    public Transform trunk;

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
    }

    public void FlapEars(float seconds = 1.5f) => flapTimer = Mathf.Max(flapTimer, seconds);
    public void SetAgitated(bool on) => agitated = on;
    public void SetPushing(bool on) => pushing = on;
    public void Eat(float seconds) => eatTimer = seconds;
    public void Rear(float seconds = 0.9f) => rearTimer = seconds;

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

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// One section of the electric fence (north and west sides). It blocks the elephants' path
// (a carving NavMesh Obstacle). Touching it gives a shock, if the energiser is on. A fence breaker
// pushing on it slowly breaks it: the post falls, the wires drop, and elephants can walk through
// the gap. With the energiser off it is only wire, and any elephant pushes it down quickly.
[RequireComponent(typeof(NavMeshObstacle))]
public class FenceSection : MonoBehaviour
{
    public static readonly List<FenceSection> All = new List<FenceSection>();

    public float health = 100f;
    [Tooltip("The post that falls when this section breaks.")]
    public Transform post;
    [Tooltip("The wires that disappear when this section breaks.")]
    public GameObject wires;
    [Tooltip("The side elephants come from (north line: +Z, west line: -X).")]
    public Vector3 outside = Vector3.forward;

    public bool IsBroken { get; private set; }

    NavMeshObstacle obstacle;
    float sparkTimer;

    void Awake()
    {
        obstacle = GetComponent<NavMeshObstacle>();
        obstacle.carving = true;
    }

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    // Where an elephant stands to face this section, 3 m out on the outside: the head is 2.4 m in
    // front of the elephant's centre, so the trunk reaches the wires but the head stays out (at 2 m
    // it went 0.4 m through them).
    public Vector3 ApproachPoint => transform.position + outside.normalized * 3f;

    // A shock when an elephant touches the fence. Sound and a blue spark.
    public void Zap(Vector3 at)
    {
        SoundBank.PlayAt(SoundBank.Zap, at, 0.8f);
        BurstFlash.At(at + Vector3.up * 1.2f, new Color(0.6f, 0.8f, 1f), 3f, 6f, 0.2f);
    }

    // A fence breaker pushing on the section.
    public void Push(float damage, Vector3 from)
    {
        if (IsBroken) return;
        health -= damage;
        sparkTimer -= Time.deltaTime;
        if (sparkTimer <= 0f && FenceEnergiser.IsOn)
        {
            sparkTimer = Random.Range(1.2f, 2.5f);
            Zap(transform.position);
        }
        if (health <= 0f) Break(from);
    }

    void Break(Vector3 from)
    {
        IsBroken = true;
        Debug.Log($"Night Watch: {name} broke (energiser {(FenceEnergiser.IsOn ? "on" : "off")}).");
        obstacle.enabled = false;
        if (wires != null) wires.SetActive(false);
        SoundBank.PlayAt(SoundBank.BranchCrack, transform.position, 1f);
        SoundBank.PlayAt(SoundBank.Zap, transform.position, 1f);

        // The post falls away from the elephant.
        if (post != null)
        {
            // The village model's posts have no collider: one box round them, so they land on the ground.
            if (post.GetComponent<Collider>() == null) post.gameObject.AddComponent<BoxCollider>();
            var body = post.gameObject.AddComponent<Rigidbody>();
            body.mass = 20f;
            var push = (transform.position - from);
            push.y = 0f;
            body.AddForceAtPosition(push.normalized * 200f, post.position + Vector3.up * 1.2f, ForceMode.Impulse);
        }
        NoticeBoard.Message(FenceEnergiser.IsOn
            ? "The fence is broken! An elephant is coming through."
            : "The fence was not switched on. An elephant pushed straight through it!");
    }

    [ContextMenu("Test: break this section")]
    void TestBreak() => Break(transform.position + outside);

    public static FenceSection Nearest(Vector3 point)
    {
        FenceSection best = null;
        float bestDistance = float.MaxValue;
        foreach (var f in All)
        {
            float d = Vector3.Distance(point, f.transform.position);
            if (d < bestDistance)
            {
                bestDistance = d;
                best = f;
            }
        }
        return best;
    }
}

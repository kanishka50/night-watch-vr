using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// One section of the electric fence on the north side. It blocks the elephants' path
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

    // Where an elephant coming from the north stands to face this section.
    public Vector3 ApproachPoint => transform.position + new Vector3(0f, 0f, 2f);

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
        obstacle.enabled = false;
        if (wires != null) wires.SetActive(false);
        SoundBank.PlayAt(SoundBank.BranchCrack, transform.position, 1f);
        SoundBank.PlayAt(SoundBank.Zap, transform.position, 1f);

        // The post falls away from the elephant.
        if (post != null)
        {
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

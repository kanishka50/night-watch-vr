using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Hakka patas: an explosive hidden in food, used illegally against elephants.
// It is in the hut as a tempting wrong choice.
//  - Picking it up shows a warning panel and counts as a wrong choice.
//  - The warning offers a choice: G puts it down, T uses it anyway (throws it toward the
//    nearest elephant). In the field it is bait: the nearest elephant goes for it and dies.
//    That ends the night with the "elephant killed" ending.
public class HakkaPatas : PlayerAction
{
    [Tooltip("Below this height it has left the hut and lies in the field as bait.")]
    public float fieldHeight = 1.5f;

    bool warned;
    bool placed;
    float searchTimer;

    // True once an elephant has started eating it, so no other elephant comes for it.
    public bool Taken { get; set; }

    protected override void Awake()
    {
        base.Awake();
        returnIfDropped = false;
    }

    protected override void OnGrabbed()
    {
        WarningPanel.Show("HAKKA PATAS\n\nThis is explosive bait hidden in food. It is illegal. " +
                          "It kills and maims elephants, and it puts people in danger too.\n\n" +
                          "Let go (grip / G): put it down        Use (trigger / T): use it anyway");
        if (!warned)
        {
            warned = true;
            ActionLog.Record("Picked up hakka patas", false, "Hakka patas is illegal. Never use it.");
        }
    }

    protected override void OnReleased()
    {
        WarningPanel.Hide();
    }

    // The player chose to use it: throw it toward the nearest elephant (or straight ahead).
    protected override void OnUsed()
    {
        if (!IsHeld) return;
        StartCoroutine(ThrowTowardElephant());
    }

    IEnumerator ThrowTowardElephant()
    {
        var target = ElephantAI.NearestActive(transform.position);
        var head = PlayerState.Head;
        var direction = target != null ? target.transform.position - transform.position
                                       : (head != null ? head.forward : transform.forward);
        direction.y = 0f;
        float distance = Mathf.Clamp(direction.magnitude, 6f, 18f);
        direction.Normalize();

        // Let go first; the grab system sets its own velocity when it releases,
        // so ours is applied one physics step later.
        if (grab.interactionManager != null)
            grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)grab);
        yield return new WaitForFixedUpdate();
        if (body != null)
            body.linearVelocity = direction * (distance * 0.6f + 2f) + Vector3.up * 4f;
    }

    protected override void Update()
    {
        base.Update();
        if (IsHeld) return;

        if (!placed && transform.position.y < fieldHeight)
        {
            placed = true;
            ActionLog.Record("Threw hakka patas into the field", false, "Never use hakka patas. It kills elephants and it is a crime.");
        }
        if (!placed) return;

        // Send the nearest elephant to the bait; wait if none is around yet.
        if (Taken) return;
        searchTimer -= Time.deltaTime;
        if (searchTimer > 0f) return;
        searchTimer = 1f;
        var elephant = ElephantAI.NearestActive(transform.position);
        if (elephant != null) elephant.GoToBait(transform);
    }

    // Called by the elephant that eats it, at the moment of the blast.
    public void Explode()
    {
        gameObject.SetActive(false);
    }
}

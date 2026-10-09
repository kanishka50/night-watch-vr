using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// A firecracker (ali wedi). Light it at the fire pot, then throw it so it lands near the
// elephants. The strongest deterrent: when it bursts, every elephant nearby runs away and loses a
// lot of resolve. Limited in number, and elephants get used to it if it is overused.
//  - Held too long: it bursts in the hand and hurts the player.
//  - Thrown AT an elephant (hits it): the elephant is hurt and becomes agitated.
public class Firecracker : PlayerAction, IIgnitable
{
    public static readonly List<Firecracker> Unused = new List<Firecracker>();

    public float fuseSeconds = 4f;
    public float range = 15f;
    public float strength = 60f;
    [Tooltip("Speed of a throw made with the use button.")]
    public float throwSpeed = 9f;
    [Tooltip("Small spark light shown while the fuse burns.")]
    public Light fuseLight;
    public AudioSource fuseSound;

    public bool IsLit { get; private set; }
    float fuseLeft;
    bool burst;

    protected override void Awake()
    {
        base.Awake();
        if (fuseLight != null) fuseLight.enabled = false;
        if (fuseSound != null) fuseSound.clip = SoundBank.FireCrackle;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        Unused.Add(this);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        Unused.Remove(this);
    }

    protected override string Hint => "Firecracker: Use (trigger / T) to light it at the fire pot, Use again to throw. Throw it NEAR elephants, never at them.";

    // Use (trigger): near the fire pot it lights the cracker; on a lit cracker it throws it
    // where the player is looking. Throwing by hand (letting go while moving) also works.
    protected override void OnUsed()
    {
        if (!IsLit)
        {
            if (FirePot.IsNear(transform.position)) Ignite();
            else FirePot.TooFarMessage();
        }
        else if (IsHeld)
        {
            StartCoroutine(ThrowForward());
        }
    }

    IEnumerator ThrowForward()
    {
        var head = PlayerState.Head;
        var direction = head != null ? head.forward : transform.forward;
        direction.y = 0f;
        direction.Normalize();

        // Let go first; the grab system sets its own velocity when it releases,
        // so ours is applied one physics step later.
        if (grab.interactionManager != null)
            grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)grab);
        yield return new WaitForFixedUpdate();
        if (body != null && !burst)
        {
            body.linearVelocity = direction * throwSpeed + Vector3.up * 4f;
            body.angularVelocity = Random.insideUnitSphere * 5f;
        }
    }

    public void Ignite()
    {
        if (IsLit || burst) return;
        IsLit = true;
        returnIfDropped = false;   // a lit cracker is meant to be thrown off the hut
        fuseLeft = fuseSeconds;
        if (fuseLight != null) fuseLight.enabled = true;
        if (fuseSound != null) fuseSound.Play();
        NoticeBoard.Message($"The cracker is lit! Press Use (trigger / T) now to throw it. It bursts in {fuseSeconds:0} seconds.");
    }

    protected override void Update()
    {
        base.Update();
        if (!IsLit || burst) return;
        if (fuseLight != null) fuseLight.intensity = Random.Range(0.5f, 1.5f);
        fuseLeft -= Time.deltaTime;
        if (fuseLeft <= 0f) Burst();
    }

    void OnCollisionEnter(Collision hit)
    {
        if (!IsLit || burst) return;
        var elephant = hit.collider.GetComponentInParent<ElephantAI>();
        if (elephant == null || hit.relativeVelocity.magnitude < 2f) return;

        elephant.Harm(55f);
        GameManager.Instance?.HurtElephant(35f);
        ActionLog.Record("Hit an elephant with a firecracker", false, "Throw crackers near elephants, never at them.");
        Burst();
    }

    void Burst()
    {
        burst = true;
        IsLit = false;
        var at = transform.position;
        // Loud and clear from the hut: full volume up to 12 m away.
        SoundBank.PlayAt(SoundBank.Cracker, at, 1f, 12f);
        BurstFlash.At(at + Vector3.up * 0.3f, new Color(1f, 0.85f, 0.6f), 8f, 15f, 0.25f);

        if (IsHeld)
        {
            GameManager.Instance?.HurtPlayer(30f, "A firecracker burst in your hand. Throw it as soon as it is lit.");
            ActionLog.Record("Held a lit firecracker too long", false, "Throw a cracker as soon as it is lit.");
        }
        else
        {
            // Run away from the bang. Bursting right next to an elephant also angers it.
            int reached = ElephantAI.ScareAllNear(at, range, "firecracker", strength, ElephantAI.Reaction.Flee, 0.5f);
            if (reached > 0) NoticeBoard.Message("The bang made the elephants run!");
            if (reached > 0)
                ActionLog.Record("Scared elephants with a firecracker", true, "A bang from far away scares them without hurting them.");
            else
                ActionLog.Record("Set off a firecracker with no elephant near", false, "Save crackers for when elephants are close.");
        }

        // Let go of it and remove it from the scene.
        if (grab != null) grab.enabled = false;
        gameObject.SetActive(false);
    }
}

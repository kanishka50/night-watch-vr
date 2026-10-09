using UnityEngine;

// A scarecrow (pambaya) in the wind: its old shirt and sarong sway, and the tin can hanging from
// its arm swings and knocks against the stick now and then (a sound farmers use to scare birds,
// and in the dark, wild pigs and deer). Heard only near it.
public class ScarecrowLife : MonoBehaviour
{
    public Transform shirt, sarong, can;
    public AudioSource clapper;

    Quaternion shirtRest, sarongRest, canRest;
    float nextKnock, swing;

    void Start()
    {
        if (shirt != null) shirtRest = shirt.localRotation;
        if (sarong != null) sarongRest = sarong.localRotation;
        if (can != null) canRest = can.localRotation;
        nextKnock = Time.time + Random.Range(5f, 30f);
    }

    void Update()
    {
        var cam = Camera.main;
        float far = cam != null ? (cam.transform.position - transform.position).sqrMagnitude : 0f;
        if (far > 80f * 80f) return;
        float t = Time.time;
        float strength = WindDriver.Strength;
        var local = transform.InverseTransformDirection(new Vector3(WindDriver.Direction.x, 0f, WindDriver.Direction.y));
        float sway = (4f + 6f * strength) * (0.6f + 0.4f * Mathf.Sin(t * 1.7f + transform.position.x));
        float flap = Mathf.Sin(t * 5.3f + transform.position.z) * 2.5f * strength;
        // Bottom edges swing downwind (about x for wind along z, about z for wind along x).
        var tilt = new Vector3(-local.z, 0f, local.x) * (sway + flap);
        if (shirt != null) shirt.localRotation = shirtRest * Quaternion.Euler(tilt);
        if (sarong != null) sarong.localRotation = sarongRest * Quaternion.Euler(tilt * 0.7f);

        // The can swings more after a knock, then settles.
        swing = Mathf.MoveTowards(swing, 0f, Time.deltaTime * 8f);
        if (can != null) can.localRotation = canRest * Quaternion.Euler((5f + swing) * Mathf.Sin(t * 4f), 0f, (3f + swing) * Mathf.Sin(t * 3.1f));

        if (Time.time > nextKnock)
        {
            nextKnock = Time.time + Random.Range(18f, 50f) / Mathf.Max(0.4f, strength);
            if (far < 35f * 35f && clapper != null)
            {
                clapper.pitch = Random.Range(0.9f, 1.1f);
                clapper.PlayOneShot(SoundBank.Clapper, Random.Range(0.6f, 1f));
                swing = 25f;
            }
        }
    }
}

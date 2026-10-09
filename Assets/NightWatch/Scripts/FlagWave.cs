using UnityEngine;

// Strings of Buddhist flags (blue, yellow, red, white, orange) from the dagaba's spire: each small
// flag hangs from the string and swings in the wind, a wave running along the string.
public class FlagWave : MonoBehaviour
{
    [Tooltip("One pivot per flag, at the string; the flag hangs below it.")]
    public Transform[] flags;

    Quaternion[] rest;

    void Start()
    {
        rest = new Quaternion[flags.Length];
        for (int i = 0; i < flags.Length; i++) rest[i] = flags[i] != null ? flags[i].localRotation : Quaternion.identity;
    }

    void Update()
    {
        var cam = Camera.main;
        if (cam != null && (cam.transform.position - transform.position).sqrMagnitude > 130f * 130f) return;
        float t = Time.time;
        float strength = WindDriver.Strength;
        var wind = new Vector3(WindDriver.Direction.x, 0f, WindDriver.Direction.y);
        for (int i = 0; i < flags.Length; i++)
        {
            var f = flags[i];
            if (f == null) continue;
            // Lift the flag toward the wind's way (more in a strong wind), with a flutter on top.
            var parentWind = f.parent != null ? f.parent.InverseTransformDirection(wind) : wind;
            float lift = (20f + 25f * strength) * (0.75f + 0.25f * Mathf.Sin(t * 2.3f - i * 0.6f));
            float flutter = Mathf.Sin(t * 9f + i * 1.3f) * 8f * strength;
            var axis = Vector3.Cross(Vector3.up, parentWind);
            f.localRotation = Quaternion.AngleAxis(-(lift + flutter), axis.sqrMagnitude > 1e-4f ? axis.normalized : Vector3.right) * rest[i];
        }
    }
}

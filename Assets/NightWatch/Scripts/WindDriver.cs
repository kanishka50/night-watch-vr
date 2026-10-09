using UnityEngine;

// The wind for the whole scene, read by the foliage shader (NightWatch/Foliage) and by anything
// else that sways (flags, scarecrow shirts). October is the start of the north-east monsoon in
// Sri Lanka, so it blows from the north-east toward the south-west; it turns a little now and
// then, is fresh in the afternoon and drops at night (a still night, but never quite still).
[ExecuteAlways]
public class WindDriver : MonoBehaviour
{
    [Tooltip("Compass bearing the wind blows FROM (degrees, 0 = north, 90 = east).")]
    public float fromBearing = 45f;
    [Tooltip("How far the wind turns either way (degrees).")]
    public float veer = 15f;
    public float afternoonStrength = 1f;
    public float nightStrength = 0.55f;
    [Tooltip("Test: overrides the strength when 0 or more.")]
    [Range(-1f, 3f)] public float strengthOverride = -1f;

    static readonly int WindId = Shader.PropertyToID("_NightWatchWind");

    // Direction it blows toward (world x, z) and strength, for scripts.
    public static Vector2 Direction { get; private set; } = new Vector2(-0.707f, -0.707f);
    public static float Strength { get; private set; } = 1f;

    void OnEnable() => Apply();
    void Update() => Apply();

    void Apply()
    {
        float t = Application.isPlaying ? Time.time : (float)(System.DateTime.Now.TimeOfDay.TotalSeconds % 10000.0);
        float bearing = fromBearing + veer * (0.6f * Mathf.Sin(t * 0.021f) + 0.4f * Mathf.Sin(t * 0.057f + 1.3f));
        float rad = bearing * Mathf.Deg2Rad;
        // From the north-east means toward the south-west: the opposite of the bearing.
        Direction = new Vector2(-Mathf.Sin(rad), -Mathf.Cos(rad));

        float strength = afternoonStrength;
        var game = GameManager.Instance;
        if (game != null)
        {
            switch (game.CurrentPhase)
            {
                case GameManager.Phase.Dusk:
                    strength = Mathf.Lerp(afternoonStrength, nightStrength, game.PhaseTime / Mathf.Max(game.duskLength, 1f));
                    break;
                case GameManager.Phase.Night: strength = nightStrength; break;
                case GameManager.Phase.Dawn: strength = nightStrength * 0.8f; break;
            }
        }
        // A slow rise and fall, so it is never the same for long.
        strength *= 0.85f + 0.15f * Mathf.Sin(t * 0.09f);
        if (strengthOverride >= 0f) strength = strengthOverride;
        Strength = strength;
        Shader.SetGlobalVector(WindId, new Vector4(Direction.x, Direction.y, strength, 1f));
    }
}

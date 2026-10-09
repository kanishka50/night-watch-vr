using UnityEngine;

// Changes the light from afternoon to dusk to night to dawn, following GameManager.DayProgress.
// Two directional lights: the sun (afternoon, dusk, dawn) and the moon (night).
// It also changes ambient light, fog and the sky's brightness.
public class DayNightCycle : MonoBehaviour
{
    public Light sun;
    public Light moon;

    [Header("Sun")]
    public Color afternoonSun = new Color(1f, 0.95f, 0.85f);
    public Color duskSun = new Color(1f, 0.55f, 0.30f);
    public Color dawnSun = new Color(1f, 0.70f, 0.55f);

    [Header("Film")]
    [Tooltip("0 to 1 = show this moment of the run instead of the game clock (used by the story film). -1 = follow the game.")]
    [Range(-1f, 1f)] public float progressOverride = -1f;

    // One look of the world at one moment of the run.
    struct Look
    {
        public float t;
        public Color ambient;
        public Color fog;
        public float fogDensity;
        public float skyExposure;
        public Color skyTint;
        public float sun;   // sun intensity
        public float moon;  // moon intensity
    }

    Look[] looks;
    Material sky;
    GameManager game;

    void Start() => Setup();

    void Setup()
    {
        game = GameManager.Instance;
        if (game == null)
        {
            enabled = false;
            return;
        }

        // Work on a copy, so Play mode does not change the saved sky material.
        if (RenderSettings.skybox != null)
        {
            sky = new Material(RenderSettings.skybox);
            RenderSettings.skybox = sky;
        }

        float total = game.afternoonLength + game.duskLength + game.nightLength + game.dawnLength;
        float duskStart = game.afternoonLength / total;
        float nightStart = (game.afternoonLength + game.duskLength) / total;
        float dawnStart = (game.afternoonLength + game.duskLength + game.nightLength) / total;

        var dayAmbient = new Color(0.45f, 0.45f, 0.50f);
        var nightAmbient = new Color(0.06f, 0.08f, 0.13f);
        var nightFog = new Color(0.02f, 0.03f, 0.06f);

        looks = new[]
        {
            new Look { t = 0f, ambient = dayAmbient, fog = new Color(0.62f, 0.68f, 0.75f), fogDensity = 0.006f, skyExposure = 1.1f, skyTint = new Color(0.5f, 0.5f, 0.5f), sun = 1.1f, moon = 0f },
            new Look { t = duskStart, ambient = new Color(0.40f, 0.33f, 0.30f), fog = new Color(0.60f, 0.45f, 0.38f), fogDensity = 0.010f, skyExposure = 0.9f, skyTint = new Color(0.55f, 0.40f, 0.35f), sun = 0.8f, moon = 0f },
            new Look { t = Mathf.Lerp(duskStart, nightStart, 0.6f), ambient = new Color(0.18f, 0.15f, 0.18f), fog = new Color(0.20f, 0.14f, 0.15f), fogDensity = 0.020f, skyExposure = 0.45f, skyTint = new Color(0.40f, 0.30f, 0.40f), sun = 0.25f, moon = 0.05f },
            new Look { t = nightStart, ambient = nightAmbient, fog = nightFog, fogDensity = 0.035f, skyExposure = 0.12f, skyTint = new Color(0.20f, 0.25f, 0.45f), sun = 0f, moon = 0.35f },
            new Look { t = dawnStart, ambient = nightAmbient, fog = nightFog, fogDensity = 0.035f, skyExposure = 0.12f, skyTint = new Color(0.20f, 0.25f, 0.45f), sun = 0f, moon = 0.35f },
            new Look { t = Mathf.Lerp(dawnStart, 1f, 0.5f), ambient = new Color(0.25f, 0.22f, 0.25f), fog = new Color(0.45f, 0.38f, 0.40f), fogDensity = 0.020f, skyExposure = 0.5f, skyTint = new Color(0.55f, 0.40f, 0.40f), sun = 0.35f, moon = 0.05f },
            // Full morning (also the look of the results): the same clear daylight as the afternoon start.
            new Look { t = 1f, ambient = dayAmbient, fog = new Color(0.62f, 0.68f, 0.75f), fogDensity = 0.006f, skyExposure = 1.1f, skyTint = new Color(0.5f, 0.5f, 0.5f), sun = 1.1f, moon = 0f },
        };

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
    }

    void Update()
    {
        // Rebuilt if lost (for example after Unity reloads scripts during Play).
        if (looks == null || game == null)
        {
            Setup();
            if (looks == null) return;
        }
        float p = progressOverride >= 0f ? progressOverride : game.DayProgress;
        var look = Sample(p);

        RenderSettings.ambientLight = look.ambient;
        RenderSettings.fogColor = look.fog;
        RenderSettings.fogDensity = look.fogDensity;
        if (sky != null)
        {
            sky.SetFloat("_Exposure", look.skyExposure);
            sky.SetColor("_SkyTint", look.skyTint);
        }

        float total = game.afternoonLength + game.duskLength + game.nightLength + game.dawnLength;
        float nightStart = (game.afternoonLength + game.duskLength) / total;
        float dawnStart = (game.afternoonLength + game.duskLength + game.nightLength) / total;

        if (sun != null)
        {
            sun.intensity = look.sun;
            sun.enabled = look.sun > 0.01f;
            if (p < nightStart)
            {
                // Afternoon to sunset: from high in the west down to the horizon.
                float k = p / nightStart;
                sun.transform.rotation = Quaternion.Euler(Mathf.Lerp(45f, -2f, k), 250f, 0f);
                sun.color = Color.Lerp(afternoonSun, duskSun, Mathf.SmoothStep(0f, 1f, k));
            }
            else if (p >= dawnStart)
            {
                // Sunrise in the east.
                float k = Mathf.InverseLerp(dawnStart, 1f, p);
                sun.transform.rotation = Quaternion.Euler(Mathf.Lerp(-2f, 40f, k), 100f, 0f);
                sun.color = Color.Lerp(dawnSun, afternoonSun, k);
            }
        }

        if (moon != null)
        {
            moon.intensity = look.moon;
            moon.enabled = look.moon > 0.01f;
        }

        // The brighter light casts the main shadows.
        RenderSettings.sun = (sun != null && moon != null && sun.intensity >= moon.intensity) ? sun : moon;
    }

    Look Sample(float p)
    {
        for (int i = 0; i < looks.Length - 1; i++)
        {
            if (p <= looks[i + 1].t)
            {
                float k = Mathf.InverseLerp(looks[i].t, looks[i + 1].t, p);
                return Lerp(looks[i], looks[i + 1], k);
            }
        }
        return looks[looks.Length - 1];
    }

    static Look Lerp(Look a, Look b, float k)
    {
        return new Look
        {
            ambient = Color.Lerp(a.ambient, b.ambient, k),
            fog = Color.Lerp(a.fog, b.fog, k),
            fogDensity = Mathf.Lerp(a.fogDensity, b.fogDensity, k),
            skyExposure = Mathf.Lerp(a.skyExposure, b.skyExposure, k),
            skyTint = Color.Lerp(a.skyTint, b.skyTint, k),
            sun = Mathf.Lerp(a.sun, b.sun, k),
            moon = Mathf.Lerp(a.moon, b.moon, k),
        };
    }
}

using UnityEngine;
using UnityEngine.Rendering;

// Changes the light from afternoon to dusk to night to dawn, following GameManager.DayProgress.
// Two directional lights: the sun (afternoon, dusk, dawn) and the moon (night). It also sets the
// sky (NightWatch/Sky: gradient, sun glow, moon, stars), the clouds, the ambient light (a
// sky / horizon / ground gradient), the fog and the sky reflection, all from one table of looks,
// so the land and the sky always match. The warm colour is only in the sun and its glow.
// With the picture sky (NightWatch/PictureSky) the sky is painted panoramas instead, faded from
// one to the next and turned to line up with the sun or moon; fog and ambient light then take
// their colour from the pictures, keeping the brightness of the table below.
public class DayNightCycle : MonoBehaviour
{
    public Light sun;
    public Light moon;

    [Header("Sky")]
    [Tooltip("The low-poly cloud ring (NightWatch/Cloud material).")]
    public Renderer clouds;
    [Tooltip("Degrees per second the cloud ring turns.")]
    public float cloudDrift = 0.12f;
    [Tooltip("A realtime probe that sees only the sky; redrawn when the sky colour changes.")]
    public ReflectionProbe skyReflection;

    [System.Serializable]
    public struct SkyPicture
    {
        public Texture2D picture;
        [Tooltip("Where the sun or moon is across the picture (0 to 1).")]
        public float lightAcross;
        [Tooltip("The bright spot is the moon: it lines up with the moon light, not the sun.")]
        public bool isMoon;
        [Tooltip("Average colours measured from the picture.")]
        public Color horizon, zenith;
    }

    [Header("Sky pictures (used when the sky material is NightWatch/PictureSky)")]
    public SkyPicture daySky;
    public SkyPicture sunsetSky;
    public SkyPicture lateDuskSky;
    public SkyPicture nightSky;
    public SkyPicture dawnSky;

    [Header("Sun")]
    public Color afternoonSun = new Color(1f, 0.95f, 0.85f);
    public Color duskSun = new Color(1f, 0.55f, 0.30f);
    public Color dawnSun = new Color(1f, 0.70f, 0.55f);
    [Tooltip("Compass bearing of the sunset (degrees; 270 = due west). October in Sri Lanka: a little south of west.")]
    public float sunsetBearing = 262f;
    [Tooltip("Compass bearing of the sunrise (90 = due east).")]
    public float sunriseBearing = 98f;

    [Header("Moon")]
    public Color moonDiscColor = new Color(0.85f, 0.90f, 1f);
    [Tooltip("The moon rises here at dusk (compass bearing) ...")]
    public float moonriseBearing = 105f;
    [Tooltip("... crosses the southern sky and is here at dawn.")]
    public float moonsetBearing = 250f;

    [Header("Film")]
    [Tooltip("0 to 1 = show this moment of the run instead of the game clock (used by the story film). -1 = follow the game.")]
    [Range(-1f, 1f)] public float progressOverride = -1f;

    // The time of day on screen, 0 to 1: the game clock, or the film's moment while the story film
    // runs. The village's living things (lamps, fireflies, egrets, bats, wind, cattle) follow
    // this, so the film shows them at the right time too.
    public static float ShownProgress =>
        shownSet ? shown : (GameManager.Instance != null ? GameManager.Instance.DayProgress : 0f);
    static float shown;
    static bool shownSet;
    [Tooltip("Multiplies the fog (less than 1 = clearer, for the film's wide drone shots).")]
    [Range(0.1f, 2f)] public float fogScale = 1f;

    // One look of the world at one moment of the run.
    struct Look
    {
        public float t;
        public Color zenith, horizon;            // the sky
        public Color fog;                        // a little darker than the horizon at night, so tree tops stand out
        public float fogDensity;
        public Color ambientSky, ambientEquator, ambientGround;
        public float sun, moon;                  // light intensities
        public float sunGlow, moonShow, stars;   // what the sky shows
        public Color cloudLit, cloudShade;
        public int picture;                      // which sky picture (0 day, 1 sunset, 2 late dusk, 3 night, 4 dawn)
        public float exposure;                   // brightness of the sky picture
    }

    Look[] looks;
    SkyPicture[] pictures;
    bool pictureSky;
    int pictureA, pictureB;
    float pictureBlend;
    Material sky;
    Material cloudMaterial;
    GameManager game;
    float nightStart, dawnStart, duskStart;
    Color probeSky;
    int probeId = -1;

    void Start() => Setup();

    void Setup()
    {
        game = GameManager.Instance;
        if (game == null)
        {
            enabled = false;
            return;
        }

        // Work on copies, so Play mode does not change the saved materials.
        if (RenderSettings.skybox != null)
        {
            sky = new Material(RenderSettings.skybox);
            RenderSettings.skybox = sky;
        }
        if (clouds != null) cloudMaterial = clouds.material;
        pictureSky = sky != null && sky.HasProperty("_SkyA");
        pictures = new[] { daySky, sunsetSky, lateDuskSky, nightSky, dawnSky };
        // The pictures have their own clouds.
        if (pictureSky && clouds != null) clouds.gameObject.SetActive(false);

        float total = game.afternoonLength + game.duskLength + game.nightLength + game.dawnLength;
        duskStart = game.afternoonLength / total;
        nightStart = (game.afternoonLength + game.duskLength) / total;
        dawnStart = (game.afternoonLength + game.duskLength + game.nightLength) / total;

        var day = new Look
        {
            zenith = new Color(0.28f, 0.50f, 0.85f), horizon = new Color(0.70f, 0.80f, 0.90f),
            fog = new Color(0.68f, 0.77f, 0.87f), fogDensity = 0.006f,
            ambientSky = new Color(0.50f, 0.58f, 0.72f), ambientEquator = new Color(0.50f, 0.50f, 0.48f), ambientGround = new Color(0.28f, 0.26f, 0.22f),
            sun = 1.1f, moon = 0f, sunGlow = 0.25f, moonShow = 0f, stars = 0f,
            cloudLit = new Color(0.98f, 0.97f, 0.95f), cloudShade = new Color(0.62f, 0.68f, 0.78f),
            picture = 0, exposure = 1f,
        };
        // Late afternoon: still clear and blue, only the horizon warms.
        var late = new Look
        {
            zenith = new Color(0.30f, 0.48f, 0.80f), horizon = new Color(0.80f, 0.78f, 0.74f),
            fog = new Color(0.74f, 0.74f, 0.74f), fogDensity = 0.007f,
            ambientSky = new Color(0.48f, 0.54f, 0.66f), ambientEquator = new Color(0.55f, 0.50f, 0.44f), ambientGround = new Color(0.28f, 0.25f, 0.20f),
            sun = 1.05f, moon = 0f, sunGlow = 0.5f, moonShow = 0.15f, stars = 0f,
            cloudLit = new Color(1f, 0.93f, 0.82f), cloudShade = new Color(0.60f, 0.62f, 0.72f),
            picture = 0, exposure = 1f,
        };
        // Sunset: orange only on the sun's side (the glow); the rest of the sky goes mauve-blue.
        var dusk = new Look
        {
            zenith = new Color(0.22f, 0.30f, 0.55f), horizon = new Color(0.62f, 0.55f, 0.64f),
            fog = new Color(0.55f, 0.52f, 0.60f), fogDensity = 0.010f,
            ambientSky = new Color(0.34f, 0.38f, 0.52f), ambientEquator = new Color(0.48f, 0.40f, 0.38f), ambientGround = new Color(0.20f, 0.17f, 0.15f),
            sun = 0.8f, moon = 0f, sunGlow = 1f, moonShow = 0.35f, stars = 0f,
            cloudLit = new Color(1f, 0.62f, 0.45f), cloudShade = new Color(0.42f, 0.36f, 0.50f),
            picture = 1, exposure = 1f,
        };
        var lateDusk = new Look
        {
            zenith = new Color(0.10f, 0.13f, 0.30f), horizon = new Color(0.38f, 0.30f, 0.42f),
            fog = new Color(0.24f, 0.21f, 0.30f), fogDensity = 0.020f,
            ambientSky = new Color(0.16f, 0.17f, 0.26f), ambientEquator = new Color(0.20f, 0.16f, 0.18f), ambientGround = new Color(0.08f, 0.07f, 0.08f),
            sun = 0.25f, moon = 0.08f, sunGlow = 0.6f, moonShow = 0.8f, stars = 0.3f,
            cloudLit = new Color(0.55f, 0.32f, 0.35f), cloudShade = new Color(0.16f, 0.14f, 0.22f),
            picture = 2, exposure = 0.85f,
        };
        var night = new Look
        {
            zenith = new Color(0.015f, 0.025f, 0.07f), horizon = new Color(0.05f, 0.07f, 0.13f),
            fog = new Color(0.03f, 0.042f, 0.078f), fogDensity = 0.035f,
            ambientSky = new Color(0.08f, 0.10f, 0.17f), ambientEquator = new Color(0.06f, 0.08f, 0.13f), ambientGround = new Color(0.03f, 0.035f, 0.05f),
            sun = 0f, moon = 0.32f, sunGlow = 0f, moonShow = 1f, stars = 1f,
            cloudLit = new Color(0.22f, 0.25f, 0.33f), cloudShade = new Color(0.05f, 0.06f, 0.10f),
            picture = 3, exposure = 0.6f,
        };
        var dawn = new Look
        {
            zenith = new Color(0.25f, 0.32f, 0.55f), horizon = new Color(0.80f, 0.62f, 0.58f),
            fog = new Color(0.55f, 0.50f, 0.55f), fogDensity = 0.015f,
            ambientSky = new Color(0.30f, 0.32f, 0.42f), ambientEquator = new Color(0.40f, 0.34f, 0.34f), ambientGround = new Color(0.15f, 0.13f, 0.13f),
            sun = 0.35f, moon = 0.05f, sunGlow = 0.9f, moonShow = 0.5f, stars = 0.1f,
            cloudLit = new Color(1f, 0.70f, 0.60f), cloudShade = new Color(0.40f, 0.36f, 0.48f),
            picture = 4, exposure = 0.95f,
        };

        looks = new[]
        {
            At(day, 0f),
            At(late, duskStart * 0.8f),
            At(dusk, duskStart),
            At(lateDusk, Mathf.Lerp(duskStart, nightStart, 0.6f)),
            At(night, nightStart),
            At(night, dawnStart),
            At(dawn, Mathf.Lerp(dawnStart, 1f, 0.5f)),
            // Full morning (also the look of the results): the same clear daylight as the afternoon start.
            At(day, 1f),
        };

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        probeSky = Color.clear;
    }

    static Look At(Look look, float t)
    {
        look.t = t;
        return look;
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
        shown = p;
        shownSet = true;
        var look = Sample(p);
        if (pictureSky) TakeColoursFromPictures(ref look);

        RenderSettings.ambientSkyColor = look.ambientSky;
        RenderSettings.ambientEquatorColor = look.ambientEquator;
        RenderSettings.ambientGroundColor = look.ambientGround;
        RenderSettings.fogColor = look.fog;
        RenderSettings.fogDensity = look.fogDensity * fogScale;

        MoveSun(p, look);
        MoveMoon(p, look);

        // The brighter light casts the main shadows.
        RenderSettings.sun = (sun != null && moon != null && sun.intensity >= moon.intensity) ? sun : moon;

        SetSky(look);
        SetClouds(look);
        UpdateReflection(look);
    }

    void MoveSun(float p, Look look)
    {
        if (sun == null) return;
        sun.intensity = look.sun;
        sun.enabled = look.sun > 0.01f;
        // A light shines away from where it is in the sky: its turn is the bearing plus 180.
        if (p < nightStart)
        {
            // Afternoon to sunset: from high in the west down to the horizon.
            float k = p / nightStart;
            sun.transform.rotation = Quaternion.Euler(Mathf.Lerp(45f, -2f, k), sunsetBearing + 180f, 0f);
            sun.color = Color.Lerp(afternoonSun, duskSun, Mathf.SmoothStep(0f, 1f, k));
        }
        else if (p >= dawnStart)
        {
            // Sunrise in the east.
            float k = Mathf.InverseLerp(dawnStart, 1f, p);
            sun.transform.rotation = Quaternion.Euler(Mathf.Lerp(-2f, 40f, k), sunriseBearing + 180f, 0f);
            sun.color = Color.Lerp(dawnSun, afternoonSun, k);
        }
    }

    // A near-full moon: low in the east at dusk, across the southern sky, low in the west at dawn.
    void MoveMoon(float p, Look look)
    {
        if (moon == null) return;
        moon.intensity = look.moon;
        moon.enabled = look.moon > 0.01f;
        float k = Mathf.InverseLerp(duskStart, 1f, p);
        float height = 15f + 30f * Mathf.Sin(k * Mathf.PI);
        float bearing = Mathf.Lerp(moonriseBearing, moonsetBearing, k);
        moon.transform.rotation = Quaternion.Euler(height, bearing + 180f, 0f);
    }

    // The fog and ambient light get the pictures' colours (horizon and top of the sky) at the
    // brightness the table asks for, so the land matches the sky but the night stays as dark.
    void TakeColoursFromPictures(ref Look look)
    {
        var a = pictures[pictureA];
        var b = pictures[pictureB];
        var horizon = Color.Lerp(a.horizon, b.horizon, pictureBlend);
        var zenith = Color.Lerp(a.zenith, b.zenith, pictureBlend);
        look.fog = Match(horizon, look.fog);
        look.ambientSky = Match(zenith, look.ambientSky);
        look.ambientEquator = Match(horizon, look.ambientEquator);
        look.horizon = horizon * look.exposure;
        look.zenith = zenith * look.exposure;
    }

    static float Brightness(Color c) => 0.3f * c.r + 0.5f * c.g + 0.2f * c.b;

    // The colour of `hue` at the brightness of `target`.
    static Color Match(Color hue, Color target)
    {
        float k = Brightness(target) / Mathf.Max(0.001f, Brightness(hue));
        return new Color(Mathf.Clamp01(hue.r * k), Mathf.Clamp01(hue.g * k), Mathf.Clamp01(hue.b * k));
    }

    // Compass bearing of where a light is in the sky (it shines the other way).
    static float Bearing(Light light)
    {
        var from = -light.transform.forward;
        return Mathf.Atan2(from.x, from.z) * Mathf.Rad2Deg;
    }

    // Turns a picture so its sun or moon sits where our sun or moon light comes from.
    float Turn(SkyPicture picture)
    {
        var light = picture.isMoon ? moon : sun;
        return light != null ? picture.lightAcross * 360f - Bearing(light) : 0f;
    }

    void SetPictureSky(Look look)
    {
        var a = pictures[pictureA];
        var b = pictures[pictureB];
        if (a.picture != null) sky.SetTexture("_SkyA", a.picture);
        if (b.picture != null) sky.SetTexture("_SkyB", b.picture);
        sky.SetFloat("_Blend", pictureBlend);
        sky.SetFloat("_TurnA", Turn(a));
        sky.SetFloat("_TurnB", Turn(b));
        sky.SetFloat("_Exposure", look.exposure);
        sky.SetColor("_GroundColor", look.fog);
        sky.SetFloat("_Stars", look.stars);
    }

    void SetSky(Look look)
    {
        if (pictureSky)
        {
            SetPictureSky(look);
            return;
        }
        if (sky == null || !sky.HasProperty("_ZenithColor")) return;
        sky.SetColor("_ZenithColor", look.zenith);
        sky.SetColor("_HorizonColor", look.horizon);
        sky.SetColor("_GroundColor", look.fog);
        sky.SetFloat("_SunGlow", look.sunGlow);
        sky.SetFloat("_MoonShow", look.moonShow);
        sky.SetFloat("_Stars", look.stars);
        sky.SetColor("_MoonColor", moonDiscColor);
        if (sun != null)
        {
            sky.SetVector("_SunDir", -sun.transform.forward);
            sky.SetColor("_SunColor", sun.color);
        }
        if (moon != null) sky.SetVector("_MoonDir", -moon.transform.forward);
    }

    void SetClouds(Look look)
    {
        if (clouds == null || cloudMaterial == null || !clouds.gameObject.activeInHierarchy) return;
        clouds.transform.Rotate(0f, cloudDrift * Time.deltaTime, 0f, Space.World);
        cloudMaterial.SetColor("_LitColor", look.cloudLit);
        cloudMaterial.SetColor("_ShadeColor", look.cloudShade);
        cloudMaterial.SetColor("_HazeColor", look.horizon);
        var light = (sun != null && look.sun >= look.moon) ? sun : moon;
        if (light != null) cloudMaterial.SetVector("_LightDir", -light.transform.forward);
    }

    // Redraws the sky reflection only when the sky has changed enough to see (not at all through
    // the long night), so the water and wet mud always mirror the sky of that moment.
    void UpdateReflection(Look look)
    {
        if (skyReflection == null) return;
        var now = look.zenith + look.horizon;
        float change = Mathf.Abs(now.r - probeSky.r) + Mathf.Abs(now.g - probeSky.g) + Mathf.Abs(now.b - probeSky.b);
        if (change < 0.02f) return;
        if (probeId >= 0 && !skyReflection.IsFinishedRendering(probeId)) return;
        probeSky = now;
        probeId = skyReflection.RenderProbe();
        // The water shader (NightWatch/Water) reads the sky from this probe's picture.
        if (skyReflection.texture != null)
        {
            Shader.SetGlobalTexture("_NightWatchSkyCube", skyReflection.texture);
            Shader.SetGlobalFloat("_NightWatchSkyCubeOn", 1f);
        }
    }

    Look Sample(float p)
    {
        for (int i = 0; i < looks.Length - 1; i++)
        {
            if (p <= looks[i + 1].t)
            {
                float k = Mathf.InverseLerp(looks[i].t, looks[i + 1].t, p);
                pictureA = looks[i].picture;
                pictureB = looks[i + 1].picture;
                pictureBlend = pictureA == pictureB ? 0f : k;
                return Lerp(looks[i], looks[i + 1], k);
            }
        }
        pictureA = pictureB = looks[looks.Length - 1].picture;
        pictureBlend = 0f;
        return looks[looks.Length - 1];
    }

    static Look Lerp(Look a, Look b, float k)
    {
        return new Look
        {
            zenith = Color.Lerp(a.zenith, b.zenith, k),
            horizon = Color.Lerp(a.horizon, b.horizon, k),
            fog = Color.Lerp(a.fog, b.fog, k),
            fogDensity = Mathf.Lerp(a.fogDensity, b.fogDensity, k),
            ambientSky = Color.Lerp(a.ambientSky, b.ambientSky, k),
            ambientEquator = Color.Lerp(a.ambientEquator, b.ambientEquator, k),
            ambientGround = Color.Lerp(a.ambientGround, b.ambientGround, k),
            sun = Mathf.Lerp(a.sun, b.sun, k),
            moon = Mathf.Lerp(a.moon, b.moon, k),
            sunGlow = Mathf.Lerp(a.sunGlow, b.sunGlow, k),
            moonShow = Mathf.Lerp(a.moonShow, b.moonShow, k),
            stars = Mathf.Lerp(a.stars, b.stars, k),
            cloudLit = Color.Lerp(a.cloudLit, b.cloudLit, k),
            cloudShade = Color.Lerp(a.cloudShade, b.cloudShade, k),
            exposure = Mathf.Lerp(a.exposure, b.exposure, k),
        };
    }
}

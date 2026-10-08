using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort;

// A short dark "blink" of the comfort vignette for moves the locomotion system does not know
// about: climbing the ladder, being moved to the start or to the debrief spot.
// A sudden jump of the view with no blink can cause motion sickness (course week 5: comfort).
// Sits on the TunnelingVignette object next to its controller.
public class VignetteBlink : MonoBehaviour, ITunnelingVignetteProvider
{
    static VignetteBlink instance;

    [SerializeField]
    VignetteParameters parameters = new VignetteParameters
    {
        apertureSize = 0.1f,
        featheringEffect = 0.2f,
        easeInTime = 0.1f,
        easeOutTime = 0.35f,
        easeInTimeLock = true,
        easeOutDelayTime = 0.05f,
        vignetteColor = Color.black,
        vignetteColorBlend = Color.black,
    };

    TunnelingVignetteController controller;

    public VignetteParameters vignetteParameters => parameters;

    void Awake()
    {
        instance = this;
        controller = GetComponent<TunnelingVignetteController>();
    }

    public static void Play()
    {
        if (instance == null || instance.controller == null) return;
        instance.StopAllCoroutines();
        instance.StartCoroutine(instance.Blink());
    }

    IEnumerator Blink()
    {
        controller.BeginTunnelingVignette(this);
        yield return null;
        // The ease-in lock keeps it closing fully before it opens again.
        controller.EndTunnelingVignette(this);
    }
}

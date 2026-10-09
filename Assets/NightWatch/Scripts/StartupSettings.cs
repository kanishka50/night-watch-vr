using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

// Settings applied when the game starts (made by Night Watch > Build Full Game, StartupSetup):
//  - The Windows build uses the same quality level as the editor ("Low": Performance URP Config,
//    Forward+). Unity's own default for Windows was "Ultra", a different renderer.
//  - The XR Interaction Simulator (keyboard and mouse pretend to be a headset) starts only when no
//    real headset is running. It used to start always, and then it switched the real headset off.
[DefaultExecutionOrder(-1000)]
public class StartupSettings : MonoBehaviour
{
    public const string QualityLevel = "Low";

    [Tooltip("The XR Interaction Simulator prefab (Unity's automatic start is switched off).")]
    public GameObject simulatorPrefab;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void SetQuality()
    {
        if (Application.isEditor) return;
        int level = System.Array.IndexOf(QualitySettings.names, QualityLevel);
        if (level >= 0) QualitySettings.SetQualityLevel(level, true);
    }

    void Awake()
    {
        // Kept across Restart (a scene reload), so it is made only once.
        if (XRInteractionSimulator.instance != null) return;
        if (HeadsetRunning())
        {
            Debug.Log("Night Watch: a headset is running, so the keyboard and mouse simulator is not started.");
            return;
        }
        if (simulatorPrefab == null)
        {
            Debug.LogError("Night Watch: no simulator prefab on StartupSettings. Run Night Watch > Build Full Game.");
            return;
        }
        DontDestroyOnLoad(Instantiate(simulatorPrefab));
    }

    static bool HeadsetRunning()
    {
        var displays = new List<XRDisplaySubsystem>();
        SubsystemManager.GetSubsystems(displays);
        foreach (var display in displays)
            if (display.running) return true;
        return false;
    }
}

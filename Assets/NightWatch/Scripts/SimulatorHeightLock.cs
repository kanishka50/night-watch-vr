using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

// Keeps the player's eye height fixed in the XR Interaction Simulator (keyboard and mouse).
// The simulator moves the pretend headset down with Q and up with E. In our game that only
// confuses players (eyes on the floor, a short pointer line), so Q and E are switched off.
// A real headset is not affected: its height comes from the person wearing it.
public class SimulatorHeightLock : MonoBehaviour
{
    // Made automatically in every Play session and in the build, so no scene object is needed.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        var go = new GameObject("SimulatorHeightLock");
        DontDestroyOnLoad(go);
        go.AddComponent<SimulatorHeightLock>();
    }

    // The simulator is created after the scene starts, so keep checking for it.
    void Update()
    {
        var simulator = XRInteractionSimulator.instance;
        if (simulator != null && simulator.translateYSpeed != 0f) simulator.translateYSpeed = 0f;
    }
}

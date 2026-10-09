using UnityEditor;
using UnityEngine;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.Features.Interactions;

// Project and start-up settings (called by Night Watch > Build Full Game and Build Windows):
//  - StartupSettings in the scene: quality level in the build, simulator only with no headset
//  - Unity's automatic simulator start switched off (StartupSettings starts it instead)
//  - more controller types for OpenXR on Windows, so the build works across controllers
//  - the company name in the build (it was "DefaultCompany")
public static class StartupSetup
{
    const string SimulatorSettingsPath = "Assets/XRI/Settings/Resources/XRDeviceSimulatorSettings.asset";
    public const string CompanyName = "INTE42312 Night Watch team";

    public static void Build(Transform game)
    {
        var go = new GameObject("StartupSettings");
        go.transform.SetParent(game, false);
        var startup = go.AddComponent<StartupSettings>();

        var settings = AssetDatabase.LoadMainAssetAtPath(SimulatorSettingsPath);
        if (settings == null)
        {
            Debug.LogWarning($"Night Watch: no {SimulatorSettingsPath}; the simulator keeps Unity's automatic start.");
            return;
        }
        var so = new SerializedObject(settings);
        var prefab = so.FindProperty("m_SimulatorPrefab").objectReferenceValue as GameObject;
        if (prefab == null)
        {
            Debug.LogWarning("Night Watch: the simulator settings name no prefab; the simulator keeps Unity's automatic start.");
            return;
        }
        startup.simulatorPrefab = prefab;
        so.FindProperty("m_AutomaticallyInstantiateSimulatorPrefab").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);

        ProjectSettings();
    }

    public static void ProjectSettings()
    {
        PlayerSettings.companyName = CompanyName;

        var openxr = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Standalone);
        if (openxr != null)
        {
            Enable<OculusTouchControllerProfile>(openxr);
            Enable<MetaQuestTouchPlusControllerProfile>(openxr);
            Enable<ValveIndexControllerProfile>(openxr);
            Enable<HTCViveControllerProfile>(openxr);
            Enable<MicrosoftMotionControllerProfile>(openxr);
            Enable<KHRSimpleControllerProfile>(openxr);
            EditorUtility.SetDirty(openxr);
        }
        AssetDatabase.SaveAssets();
    }

    static void Enable<T>(OpenXRSettings openxr) where T : OpenXRFeature
    {
        var feature = openxr.GetFeature<T>();
        if (feature == null)
        {
            Debug.LogWarning($"Night Watch: OpenXR has no {typeof(T).Name} for Windows.");
            return;
        }
        if (feature.enabled) return;
        feature.enabled = true;
        EditorUtility.SetDirty(feature);
    }
}

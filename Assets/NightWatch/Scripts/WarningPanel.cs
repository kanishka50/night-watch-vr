using TMPro;
using UnityEngine;

// A warning that appears in front of the player (used when hakka patas is picked up).
public class WarningPanel : MonoBehaviour
{
    static WarningPanel instance;

    public GameObject panel;
    public TextMeshPro text;
    public float distance = 0.9f;

    void Awake()
    {
        instance = this;
        if (panel != null) panel.SetActive(false);
    }

    public static void Show(string message)
    {
        if (instance == null || instance.panel == null) return;
        var head = PlayerState.Head;
        if (head != null)
        {
            var forward = head.forward;
            forward.y = 0f;
            forward.Normalize();
            instance.panel.transform.position = head.position + forward * instance.distance;
            instance.panel.transform.rotation = Quaternion.LookRotation(forward);
        }
        if (instance.text != null) instance.text.text = message;
        instance.panel.SetActive(true);
    }

    public static void Hide()
    {
        if (instance != null && instance.panel != null) instance.panel.SetActive(false);
    }
}

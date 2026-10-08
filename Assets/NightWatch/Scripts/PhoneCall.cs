using UnityEngine;

// The mobile phone in the hut. Point at it and press G to call the neighbour and warn them that
// elephants are about. Farmers really do warn each other by phone when elephants come near a village.
public class PhoneCall : PressTarget
{
    [Tooltip("The small screen that lights up during a call.")]
    public Renderer screen;

    MaterialPropertyBlock screenColor;
    float lit;

    protected override void OnPressed()
    {
        if (NeighbourHouse.Instance != null) NeighbourHouse.Instance.Call();
        lit = 3f;
        ShowScreen(true);
    }

    protected override void Update()
    {
        base.Update();
        if (lit <= 0f) return;
        lit -= Time.deltaTime;
        if (lit <= 0f) ShowScreen(false);
    }

    void ShowScreen(bool on)
    {
        if (screen == null) return;
        if (screenColor == null) screenColor = new MaterialPropertyBlock();
        screenColor.SetColor("_BaseColor", on ? new Color(0.6f, 0.9f, 1f) : new Color(0.05f, 0.08f, 0.1f));
        screen.SetPropertyBlock(screenColor);
    }

    protected override void Awake()
    {
        base.Awake();
        ShowScreen(false);
    }
}

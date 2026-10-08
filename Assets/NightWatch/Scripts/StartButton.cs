// The START button on the start screen. Point at it and press G.
public class StartButton : PressTarget
{
    protected override void OnPressed()
    {
        if (StartScreen.Instance != null) StartScreen.Instance.Begin();
    }
}

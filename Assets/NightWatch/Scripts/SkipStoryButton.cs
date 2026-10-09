// The "Skip story" button on the ola leaf. Point at it and press G.
public class SkipStoryButton : PressTarget
{
    public StoryIntro story;

    protected override void OnPressed()
    {
        if (story != null) story.Skip();
    }
}

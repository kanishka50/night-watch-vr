// The "Skip story" button on the ola leaf, or "Skip film" by the film screen. Point at it and press G.
public class SkipStoryButton : PressTarget
{
    public StoryIntro story;
    public StoryFilm film;

    protected override void OnPressed()
    {
        if (story != null) story.Skip();
        if (film != null) film.Skip();
    }
}

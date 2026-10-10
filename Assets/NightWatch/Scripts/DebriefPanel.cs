using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

// The results board at the end of the night, kept short and simple:
//   the ending and one line on why, the three scores, the good choices (short labels), and
//   "Next time" (one short tip for each mistake). Then the credits and a Restart button.
public class DebriefPanel : MonoBehaviour
{
    public GameObject panel;
    [Tooltip("The ending title and its one line, at the top of the board.")]
    public TextMeshPro header;
    [Tooltip("The three score plaques: you, elephants, crop.")]
    public TextMeshPro[] scores;
    [Tooltip("Good choices, next time, and how to restart.")]
    public TextMeshPro results;
    public TextMeshPro credits;
    public GameObject restartButton;
    public GameObject quitButton;

    // Every third-party asset in the build. The full list with links and
    // licences is CREDITS.md in the project folder; keep the two the same.
    [TextArea(3, 20)]
    public string creditsText =
        "<size=130%><color=#7A2C10><b>Credits</b></color></size>\n" +
        "Night Watch, INTE 42312 Virtual and Augmented Reality group project.\n\n" +
        "<color=#7A2C10><b>3D models, CC BY 4.0</b></color>\n" +
        "Elephant: \"Low poly Realistic Elephant 3d model free\" by iRahulRajput (Sketchfab). Rigged and animated for this project.\n" +
        "Electric fence: \"Wire Fence\" by Pixel-bit (Sketchfab).\n" +
        "Shed: \"Worn Shed PBR Game-Ready\" by donnichols (Sketchfab).\n\n" +
        "<color=#7A2C10><b>Free assets, CC0</b></color>\n" +
        "Village models and textures: Poly Haven (Amal Kumar, Rob Tuytel, Rico Cilliers, Greg Zaal, " +
        "Charlotte Baglioni, Dario Barresi, Dimitrios Savva, Aron Łyczek, Jenelle van Heerden, Kless Gyzen, " +
        "Sebastian Platen, Kuutti Siitonen, James Ray Cock, Miroslav Turura, eye-candy.xyz).\n" +
        "Neighbour, cow and bull: Quaternius.\n" +
        "Sky: Cloudy Skyboxes by Screaming Brain Studios.\n" +
        "Elephant trumpet: \"Cartoon Animal Elephant Trumpet\" from Tunetank (tunetank.com).\n" +
        "Cow moo: \"Cow mooing in south of France (Limousin)\" by Felix Blume (Freesound).\n\n" +
        "<color=#7A2C10><b>Font</b></color>\n" +
        "Cinzel by The Cinzel Project Authors, SIL Open Font License 1.1.\n\n" +
        "Built with Unity, XR Interaction Toolkit, OpenXR and AI Navigation. Other sounds made in code by the team.\n" +
        "Full list with links: CREDITS.md";

    const string Good = "#2F6B1E", Warn = "#A0461A";

    void Awake()
    {
        if (panel != null) panel.SetActive(false);
        if (restartButton != null) restartButton.SetActive(false);
        if (quitButton != null) quitButton.SetActive(false);
    }

    void Start()
    {
        var game = GameManager.Instance;
        if (game != null) game.onEnded.AddListener(Show);
    }

    void Show(GameManager.Ending ending)
    {
        var game = GameManager.Instance;
        var sb = new StringBuilder();
        // Inks for the ola-leaf page (BoardStyle): dark green for good, burnt orange for warnings.
        string colour = ending == GameManager.Ending.EveryoneSafe ? Good : Warn;
        string title = $"<size=150%><color={colour}><b>{game.ResultTitle}</b></color></size>\n{game.ResultLine}";
        // The scores on their own wooden plaques (like the buttons), so they stand out.
        if (header != null && scores != null && scores.Length >= 3)
        {
            header.text = title;
            scores[0].text = $"You  {game.peopleSafe:0}%";
            scores[1].text = $"Elephants  {game.elephantsSafe:0}%";
            scores[2].text = $"Crop  {game.CropSaved:0}%";
        }
        else
        {
            sb.AppendLine(title);
            sb.AppendLine();
            sb.AppendLine($"You <b>{game.peopleSafe:0}%</b>        Elephants <b>{game.elephantsSafe:0}%</b>        Crop <b>{game.CropSaved:0}%</b>");
            sb.AppendLine();
        }

        // Each choice once, in the order it first happened, with how many times.
        var good = new List<string>();
        var tips = new List<string>();
        var counts = new Dictionary<string, int>();
        foreach (var e in ActionLog.Entries)
            counts[e.action] = counts.TryGetValue(e.action, out var n) ? n + 1 : 1;
        var seen = new HashSet<string>();
        foreach (var e in ActionLog.Entries)
        {
            if (!seen.Add(e.action)) continue;
            if (e.safe) good.Add(counts[e.action] > 1 ? $"{e.action} (x{counts[e.action]})" : e.action);
            else if (!tips.Contains(e.reason)) tips.Add(e.reason);
        }
        if (game.GroupsTurnedBack > 0)
            good.Insert(0, $"Turned back {game.GroupsTurnedBack} of {game.GroupsArrived} elephant groups");
        // Tips that come from what happened, not from one choice.
        if (game.HutHits > 0 && game.Result != GameManager.Ending.PlayerDied)
            tips.Add("Scare elephants away before they reach the hut.");
        if (game.CropSaved < game.cropGoal)
            tips.Add("Act early, on every side, to save the crop.");

        const int MaxGood = 5;
        if (good.Count > 0)
        {
            sb.AppendLine($"<color={Good}><b>Good choices</b></color>");
            for (int i = 0; i < good.Count && i < MaxGood; i++) sb.AppendLine("-  " + good[i]);
            if (good.Count > MaxGood) sb.AppendLine($"-  and {good.Count - MaxGood} more");
            sb.AppendLine();
        }

        sb.AppendLine($"<color={Warn}><b>Next time</b></color>");
        if (tips.Count == 0) sb.AppendLine("-  Nothing to change. Well done!");
        foreach (var tip in tips) sb.AppendLine("-  " + tip);
        sb.AppendLine();
        sb.AppendLine("Point at RESTART or QUIT and Grab (grip / G).");

        if (results != null) results.text = sb.ToString();
        if (credits != null) credits.text = creditsText;
        if (panel != null) panel.SetActive(true);
        if (restartButton != null) restartButton.SetActive(true);
        if (quitButton != null) quitButton.SetActive(true);
    }
}

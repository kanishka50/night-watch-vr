using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

// The big results panel shown at the end of the night: the ending, the reason, the three scores,
// every choice the player made (right or wrong, and why), the credits, and a Restart button.
public class DebriefPanel : MonoBehaviour
{
    public GameObject panel;
    public TextMeshPro results;
    public TextMeshPro credits;
    public GameObject restartButton;

    [TextArea(3, 10)]
    public string creditsText =
        "<b>Credits</b>\n" +
        "Night Watch: INTE 42312 Virtual and Augmented Reality group project.\n" +
        "Stand-in shapes and all sounds were made by the team in Unity.\n" +
        "Built with Unity, XR Interaction Toolkit, OpenXR and AI Navigation.\n" +
        "Full list of assets and licences: CREDITS.md";

    void Awake()
    {
        if (panel != null) panel.SetActive(false);
        if (restartButton != null) restartButton.SetActive(false);
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
        string colour = ending == GameManager.Ending.EveryoneSafe ? "#9CFF8A" : "#FFB36B";
        sb.AppendLine($"<size=150%><color={colour}><b>{game.ResultTitle}</b></color></size>");
        sb.AppendLine(game.ResultReason);
        sb.AppendLine();
        sb.AppendLine($"Your health <b>{game.peopleSafe:0}%</b>     Elephants safe <b>{game.elephantsSafe:0}%</b>     Crop saved <b>{game.CropSaved:0}%</b>");
        sb.AppendLine($"Elephant groups you turned back: <b>{game.GroupsTurnedBack} of {game.GroupsArrived}</b>     Hut hit: <b>{game.HutHits} of {game.hutStrength}</b>");
        sb.AppendLine();
        sb.AppendLine("<b>Your choices</b>");

        // Group repeated choices, keeping the order they first happened.
        var order = new List<string>();
        var counts = new Dictionary<string, int>();
        var first = new Dictionary<string, ActionLog.Entry>();
        foreach (var e in ActionLog.Entries)
        {
            if (!counts.ContainsKey(e.action))
            {
                order.Add(e.action);
                counts[e.action] = 0;
                first[e.action] = e;
            }
            counts[e.action]++;
        }
        if (order.Count == 0) sb.AppendLine("You did not use anything tonight.");
        foreach (var action in order)
        {
            var e = first[action];
            string mark = e.safe ? "<color=#9CFF8A>RIGHT</color>" : "<color=#FF7070>WRONG</color>";
            string times = counts[action] > 1 ? $" (x{counts[action]})" : "";
            sb.AppendLine($"{mark}  {action}{times}: {e.reason}");
        }
        sb.AppendLine();
        sb.AppendLine("Point at RESTART and press G to play again.");

        if (results != null) results.text = sb.ToString();
        if (credits != null) credits.text = creditsText;
        if (panel != null) panel.SetActive(true);
        if (restartButton != null) restartButton.SetActive(true);
    }
}

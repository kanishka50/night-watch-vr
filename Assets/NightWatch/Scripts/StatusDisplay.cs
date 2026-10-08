using TMPro;
using UnityEngine;

// A small status text: time to dawn, the three scores, crackers left and elephants near.
// Used on the wrist (left controller) and at the bottom of the notice board.
public class StatusDisplay : MonoBehaviour
{
    public TextMeshPro text;
    [Tooltip("Short form for the wrist, long form for the board.")]
    public bool compact;

    float timer;

    void Update()
    {
        timer -= Time.deltaTime;
        if (timer > 0f || text == null) return;
        timer = 0.25f;

        var game = GameManager.Instance;
        if (game == null) return;

        int seconds = Mathf.CeilToInt(game.TimeToDawn);
        string clock = $"{seconds / 60}:{seconds % 60:00}";
        string people = $"{game.peopleSafe:0}%";
        string elephants = $"{game.elephantsSafe:0}%";
        string crop = $"{game.CropSaved:0}%";
        int crackers = Firecracker.Unused.Count;
        int near = ElephantAI.CountActive();

        text.text = compact
            ? $"{game.CurrentPhase}  Dawn in {clock}\nPeople {people}  Elephants {elephants}\nCrop {crop}  Crackers {crackers}"
            : $"<b>{game.CurrentPhase}</b>   Dawn in {clock}   Elephants around: {near}\n" +
              $"Health {people}   Elephants safe {elephants}   Crop saved {crop}   Crackers left {crackers}";
    }
}

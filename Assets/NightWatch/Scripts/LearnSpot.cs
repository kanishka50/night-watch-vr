using UnityEngine;

// Something in the village the player can point at to learn one short fact (LearnPointer shows
// it). Sits on an invisible trigger shape on the Ignore Raycast layer, so it never blocks
// teleporting, grabbing or the elephants' own checks.
public class LearnSpot : MonoBehaviour
{
    public string title;
    [TextArea] public string line;
}

using System.Collections.Generic;
using UnityEngine;

// Remembers what the player did tonight, in order, so the debrief can show each choice
// with whether it was right or wrong and why.
public static class ActionLog
{
    public struct Entry
    {
        public string action;
        public bool safe;
        public string reason;
        public float time;
    }

    static readonly List<Entry> entries = new List<Entry>();

    public static IReadOnlyList<Entry> Entries => entries;

    public static void Record(string action, bool safe, string reason)
    {
        entries.Add(new Entry { action = action, safe = safe, reason = reason, time = Time.timeSinceLevelLoad });
        Debug.Log($"Night Watch: {(safe ? "right" : "WRONG")}: {action}");
    }

    public static void Clear() => entries.Clear();

    // Start each Play session with an empty log, even when Unity keeps static data between runs.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay() => entries.Clear();
}

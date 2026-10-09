using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

// Sends the elephants in waves that follow the player:
//  - A tutorial wave at dusk, watched from the hut: a bull at the north fence.
//    The shock turns it back, but only if the fence energiser was switched on.
//  - Night waves, one after another. The next wave comes 20 seconds after the previous one has
//    left, or after 2 minutes at most if the player cannot deal with it.
//  - When the last wave has left, the game manager brings dawn early.
// A wave can have several groups at once (the peak: north and the village side together). A
// group is one elephant or a herd (leader, followers, maybe a calf). Three ways in: North from the
// tank (lake) to the fence, South from the jungle through the field, and the Village side from
// behind the houses to the north-east, past the neighbour's house.
public class ArrivalManager : MonoBehaviour
{
    public enum Side { North, South, Village }

    [Serializable]
    public class Group
    {
        public string name = "A lone bull";
        public Side side = Side.South;
        [Min(1)] public int adults = 1;
        public bool withCalf;
        public bool fenceBreaker;
        [Tooltip("Comes past the neighbour's house (see NeighbourHouse).")]
        public bool villageSide;
    }

    [Serializable]
    public class Wave
    {
        public string message = "Listen: branches are cracking somewhere. Look around.";
        public List<Group> groups = new List<Group>();
        [NonSerialized] public List<ElephantAI> members = new List<ElephantAI>();
        [NonSerialized] public float startTime;
    }

    [Tooltip("A hidden, ready-made elephant. Each arrival is a copy of it.")]
    public GameObject elephantTemplate;
    public Transform northSpawn, southSpawn, villageSpawn;
    [Tooltip("How far from the spawn point an arrival can start.")]
    public float spawnSpread = 5f;

    [Header("Timing")]
    [Tooltip("Seconds into dusk when the tutorial wave comes.")]
    public float tutorialTime = 10f;
    [Tooltip("Seconds into the night when the first night wave comes.")]
    public float firstNightWaveDelay = 10f;
    [Tooltip("Quiet seconds after a wave has left before the next one comes.")]
    public float gapAfterWave = 20f;
    [Tooltip("The next wave comes after this many seconds even if the last one is still here.")]
    public float maxWaveSeconds = 120f;

    public Wave tutorialWave = new Wave
    {
        message = "Look north: an elephant is coming to the fence. Watch what the fence does.",
        groups = { new Group { name = "The bull at the fence", side = Side.North } }
    };

    public List<Wave> nightWaves = new List<Wave>
    {
        new Wave { groups = { new Group { name = "A lone bull", side = Side.South } } },
        new Wave
        {
            message = "Listen: several elephants are moving. A herd is coming.",
            groups = { new Group { name = "The herd", side = Side.South, adults = 2, withCalf = true } }
        },
        new Wave
        {
            message = "Elephants on two sides! Check the fence AND the village side.",
            groups =
            {
                new Group { name = "The fence breaker", side = Side.North, fenceBreaker = true },
                new Group { name = "The bull from the village side", side = Side.Village, villageSide = true },
            }
        },
    };

    bool tutorialDone;
    int current = -1;
    float clearedSince = -1f;
    bool reportedDone;

    void Update()
    {
        var game = GameManager.Instance;
        if (game == null || elephantTemplate == null) return;

        if (!tutorialDone && game.CurrentPhase == GameManager.Phase.Dusk && game.PhaseTime >= tutorialTime)
        {
            tutorialDone = true;
            StartWave(tutorialWave, false);
        }

        if (game.CurrentPhase != GameManager.Phase.Night) return;

        if (current < 0)
        {
            if (game.PhaseTime >= firstNightWaveDelay) Next();
            return;
        }

        var wave = nightWaves[current];
        bool cleared = wave.members.TrueForAll(e => e == null || !e.gameObject.activeInHierarchy || !e.IsActive);
        if (cleared && clearedSince < 0f) clearedSince = Time.time;

        bool last = current >= nightWaves.Count - 1;
        if (!last)
        {
            bool timeUp = Time.time - wave.startTime >= maxWaveSeconds;
            if ((cleared && Time.time - clearedSince >= gapAfterWave) || timeUp) Next();
        }
        else if (cleared && !reportedDone && AllNightWavesCleared())
        {
            reportedDone = true;
            game.AllWavesDone();
        }
    }

    // For testing: send one of the night waves straight away (0 = lone bull, 1 = herd, 2 = peak).
    public void SpawnNow(int nightWaveIndex)
    {
        if (nightWaveIndex < 0 || nightWaveIndex >= nightWaves.Count) return;
        StartWave(nightWaves[nightWaveIndex], false);
    }

    // For testing: only the bull from the village side (the neighbour's rescue moment).
    public void SpawnVillageBull()
    {
        var members = new List<ElephantAI>();
        SpawnGroup(new Group { name = "The bull from the village side", side = Side.Village, villageSide = true }, members);
        NoticeBoard.Message("A bull is coming from the village side.");
    }

    // For testing: only the fence breaker from the north (the fence break and the gap).
    public void SpawnFenceBreaker()
    {
        var members = new List<ElephantAI>();
        SpawnGroup(new Group { name = "The fence breaker", side = Side.North, fenceBreaker = true }, members);
        NoticeBoard.Message("A big bull is coming to the fence.");
    }

    void Next()
    {
        current++;
        clearedSince = -1f;
        StartWave(nightWaves[current], true);
    }

    bool AllNightWavesCleared()
    {
        foreach (var w in nightWaves)
            if (!w.members.TrueForAll(e => e == null || !e.gameObject.activeInHierarchy || !e.IsActive))
                return false;
        return true;
    }

    void StartWave(Wave wave, bool counts)
    {
        wave.startTime = Time.time;
        wave.members.Clear();
        foreach (var g in wave.groups)
        {
            SpawnGroup(g, wave.members);
            if (counts) GameManager.Instance?.GroupArrived();
        }
        NoticeBoard.Message(wave.message);
    }

    void SpawnGroup(Group g, List<ElephantAI> members)
    {
        var side = g.side;
        var spawn = SpawnFor(side);
        if (spawn == null) return;

        // Spread sideways (east-west), not toward the field.
        var along = Vector3.right;
        var start = spawn.position + along * Random.Range(-spawnSpread, spawnSpread);
        bool fromNorth = side == Side.North;

        var leader = Create(start, spawn.position, fromNorth, 1f);
        leader.name = g.name;
        leader.isFenceBreaker = g.fenceBreaker;
        if (g.fenceBreaker) leader.resolve = 140f;
        else if (g.adults > 1 || g.withCalf) leader.resolve = 130f;
        members.Add(leader);
        if (g.villageSide && NeighbourHouse.Instance != null) NeighbourHouse.Instance.VillageElephantCame(leader);

        for (int i = 1; i < g.adults; i++)
        {
            var follower = Create(start + Flat(Random.insideUnitSphere) * 3f, spawn.position, fromNorth, 1f);
            follower.leader = leader;
            follower.name = g.name + " (member)";
            members.Add(follower);
        }
        if (g.withCalf)
        {
            var calf = Create(start + Flat(Random.insideUnitSphere) * 2f, spawn.position, fromNorth, 0.55f);
            calf.leader = leader;
            calf.isCalf = true;
            calf.eatRateMultiplier = 0.4f;
            calf.name = g.name + " (calf)";
            members.Add(calf);
        }

        // A herd is cows and calves, led by the oldest cow: no tusks. Bulls keep theirs, so the
        // player can tell them apart (in Sri Lanka few bulls are tuskers).
        if (g.adults > 1 || g.withCalf)
            foreach (var m in members)
                if (m.name.StartsWith(g.name) && m.body != null) m.body.SetTusks(false);
    }

    ElephantAI Create(Vector3 position, Vector3 home, bool fromNorth, float size)
    {
        if (NavMesh.SamplePosition(position, out var hit, 6f, NavMesh.AllAreas)) position = hit.position;
        var go = Instantiate(elephantTemplate, position, Quaternion.LookRotation(Flat(ElephantAI.HutCenter - position)));
        go.transform.localScale = Vector3.one * size;
        go.SetActive(true);

        var agent = go.GetComponent<NavMeshAgent>();
        if (agent != null) agent.Warp(position);

        var ai = go.GetComponent<ElephantAI>();
        ai.HomePoint = home;
        ai.FromNorth = fromNorth;
        return ai;
    }

    Transform SpawnFor(Side side)
    {
        switch (side)
        {
            case Side.North: return northSpawn;
            case Side.Village: return villageSpawn;
            default: return southSpawn;
        }
    }

    static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude < 0.001f ? Vector3.forward : v;
    }
}

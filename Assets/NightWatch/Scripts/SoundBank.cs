using UnityEngine;

// All the game's sounds, made in code by simple sound synthesis (no downloaded files).
// Each clip is made the first time it is needed, then reused.
// These are placeholders: recorded sounds (with credit in CREDITS.md) can replace them later.
public static class SoundBank
{
    const int Rate = 22050;

    static AudioClip rumble, trumpet, crack, bang, tin, crackle, crickets, zap, heartbeat, tick, drum;

    public static AudioClip Heartbeat => heartbeat != null ? heartbeat : heartbeat = Make("Heartbeat", 0.9f, MakeHeartbeat);

    public static AudioClip Rumble => rumble != null ? rumble : rumble = Make("Rumble", 2.5f, MakeRumble);
    public static AudioClip Trumpet => trumpet != null ? trumpet : trumpet = Make("Trumpet", 1.4f, MakeTrumpet);
    public static AudioClip BranchCrack => crack != null ? crack : crack = Make("BranchCrack", 0.4f, MakeCrack);
    public static AudioClip Bang => bang != null ? bang : bang = Make("Bang", 1.2f, MakeBang);
    public static AudioClip TinBang => tin != null ? tin : tin = Make("TinBang", 0.6f, MakeTin);
    public static AudioClip FireCrackle => crackle != null ? crackle : crackle = Make("FireCrackle", 2f, MakeCrackle);
    public static AudioClip Crickets => crickets != null ? crickets : crickets = Make("Crickets", 3f, MakeCrickets);
    public static AudioClip Zap => zap != null ? zap : zap = Make("Zap", 0.6f, MakeZap);
    public static AudioClip Tick => tick != null ? tick : tick = Make("Tick", 0.12f, MakeTick);
    public static AudioClip Drum => drum != null ? drum : drum = Make("Drum", 0.7f, MakeDrum);

    // Plays a clip once at a place in the world, as a 3D sound.
    public static void PlayAt(AudioClip clip, Vector3 position, float volume = 1f, float minDistance = 4f)
    {
        if (clip == null) return;
        var go = new GameObject("Sound_" + clip.name);
        go.transform.position = position;
        var source = go.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = volume;
        source.spatialBlend = 1f;
        source.minDistance = minDistance;
        source.maxDistance = 80f;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.Play();
        Object.Destroy(go, clip.length + 0.1f);
    }

    delegate void Fill(float[] data, System.Random random);

    static AudioClip Make(string name, float seconds, Fill fill)
    {
        var data = new float[Mathf.CeilToInt(seconds * Rate)];
        fill(data, new System.Random(name.GetHashCode()));
        var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static float Noise(System.Random r) => (float)(r.NextDouble() * 2.0 - 1.0);
    static float Sin(float freq, float t) => Mathf.Sin(2f * Mathf.PI * freq * t);

    // Smooth rise, hold, fall over the clip.
    static float Envelope(float t, float length, float attack, float release)
    {
        if (t < attack) return t / attack;
        if (t > length - release) return Mathf.Max(0f, (length - t) / release);
        return 1f;
    }

    // Low, wobbling hum with breath noise: an elephant rumble.
    static void MakeRumble(float[] d, System.Random r)
    {
        float len = d.Length / (float)Rate;
        float low = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float f = 70f + 6f * Mathf.Sin(2f * Mathf.PI * 0.8f * t);
            low += 0.02f * (Noise(r) - low);
            float s = 0.55f * Sin(f, t) + 0.30f * Sin(2f * f, t) + 0.15f * Sin(3f * f, t) + 0.6f * low;
            d[i] = 0.7f * s * Envelope(t, len, 0.4f, 0.8f);
        }
    }

    // A rising brassy tone with vibrato: a trumpet call.
    static void MakeTrumpet(float[] d, System.Random r)
    {
        float len = d.Length / (float)Rate;
        float phase = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float k = t / len;
            float f = Mathf.Lerp(420f, 640f, Mathf.Sin(k * Mathf.PI * 0.8f)) + 12f * Mathf.Sin(2f * Mathf.PI * 6f * t);
            phase += f / Rate;
            float saw = 2f * (phase - Mathf.Floor(phase + 0.5f));
            float s = (float)System.Math.Tanh(2.5f * saw) + 0.15f * Noise(r);
            d[i] = 0.45f * s * Envelope(t, len, 0.05f, 0.35f);
        }
    }

    // A few sharp clicks: a branch breaking.
    static void MakeCrack(float[] d, System.Random r)
    {
        for (int k = 0; k < 5; k++)
        {
            int start = (int)(r.NextDouble() * 0.2 * Rate);
            float amp = 0.5f + 0.5f * (float)r.NextDouble();
            for (int i = start; i < d.Length; i++)
            {
                float t = (i - start) / (float)Rate;
                d[i] += amp * Noise(r) * Mathf.Exp(-t * 90f);
            }
        }
        Normalise(d, 0.8f);
    }

    // A noise burst with a low thump: a firecracker.
    static void MakeBang(float[] d, System.Random r)
    {
        float low = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            low += 0.25f * (Noise(r) - low);
            d[i] = low * Mathf.Exp(-t * 5f) + 0.8f * Sin(55f, t) * Mathf.Exp(-t * 12f);
        }
        Normalise(d, 0.95f);
    }

    // Metal ringing tones: banging tin cans.
    static void MakeTin(float[] d, System.Random r)
    {
        float[] tones = { 620f, 1010f, 1580f, 2240f };
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float s = 0f;
            for (int k = 0; k < tones.Length; k++) s += Sin(tones[k], t) * Mathf.Exp(-t * (6f + 3f * k)) / (k + 1);
            if (t < 0.01f) s += Noise(r);
            d[i] = s;
        }
        Normalise(d, 0.7f);
    }

    // Soft hiss with random pops: a fire.
    static void MakeCrackle(float[] d, System.Random r)
    {
        float low = 0f;
        float pop = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            low += 0.05f * (Noise(r) - low);
            if (r.NextDouble() < 0.0015) pop = 0.6f + 0.4f * (float)r.NextDouble();
            pop *= 0.985f;
            d[i] = 0.25f * low + pop * Noise(r);
        }
        Normalise(d, 0.5f);
    }

    // Two crickets chirping in short pulses.
    static void MakeCrickets(float[] d, System.Random r)
    {
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            d[i] = 0.25f * Chirp(t, 0.45f, 4200f) + 0.18f * Chirp(t + 0.2f, 0.6f, 4700f);
        }
    }

    static float Chirp(float t, float period, float freq)
    {
        float inGroup = t % period;
        if (inGroup > 0.11f) return 0f;
        float inPulse = inGroup % 0.037f;
        return inPulse < 0.022f ? Sin(freq, t) : 0f;
    }

    // A harsh buzz with crackle: an electric fence shock.
    static void MakeZap(float[] d, System.Random r)
    {
        float len = d.Length / (float)Rate;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float square = Sin(100f, t) > 0f ? 1f : -1f;
            d[i] = (0.5f * square + 0.5f * Noise(r)) * Mathf.Exp(-t * 4f) * Envelope(t, len, 0.005f, 0.1f);
        }
        Normalise(d, 0.6f);
    }

    // A short sharp click with a little buzz: one pulse of a fence energiser.
    static void MakeTick(float[] d, System.Random r)
    {
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float click = Mathf.Exp(-t * 300f) * Noise(r);
            float buzz = 0.3f * Mathf.Exp(-t * 40f) * (Sin(120f, t) > 0f ? 1f : -1f);
            d[i] = click + buzz;
        }
        Normalise(d, 0.5f);
    }

    // One hit on a big low drum: a skin that drops in pitch, with a soft slap at the start.
    static void MakeDrum(float[] d, System.Random r)
    {
        float phase = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float freq = 55f + 45f * Mathf.Exp(-t * 18f);
            phase += 2f * Mathf.PI * freq / Rate;
            float body = Mathf.Sin(phase) * Mathf.Exp(-t * 6f);
            float slap = 0.25f * Noise(r) * Mathf.Exp(-t * 80f);
            d[i] = body + slap;
        }
        Normalise(d, 0.9f);
    }

    // Two low thumps: a heartbeat (one beat per loop).
    static void MakeHeartbeat(float[] d, System.Random r)
    {
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float first = Mathf.Exp(-t * 25f) * Sin(55f, t);
            float t2 = t - 0.22f;
            float second = t2 > 0f ? 0.7f * Mathf.Exp(-t2 * 25f) * Sin(50f, t2) : 0f;
            d[i] = first + second + 0.3f * (first + second) * Sin(110f, t);
        }
        Normalise(d, 0.9f);
    }

    static void Normalise(float[] d, float peak)
    {
        float max = 0.0001f;
        foreach (var v in d) max = Mathf.Max(max, Mathf.Abs(v));
        float k = peak / max;
        for (int i = 0; i < d.Length; i++) d[i] *= k;
    }
}

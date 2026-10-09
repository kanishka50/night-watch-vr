using UnityEngine;

// All the game's sounds, made in code by simple sound synthesis, except the elephant's trumpet
// (Tunetank) and the cattle's moo (CC0): recordings in Resources/Sounds, credited in CREDITS.md. If a recording is
// missing, the code-made sound is used instead.
// Each clip is made (or loaded) the first time it is needed, then reused.
public static class SoundBank
{
    const int Rate = 22050;

    static AudioClip rumble, trumpet, crack, bang, tin, crackle, crickets, zap, heartbeat, tick, drum, birds, ring, cracker, trickle, moo, frogs, bark, clapper;

    public static AudioClip Heartbeat => heartbeat != null ? heartbeat : heartbeat = Make("Heartbeat", 0.9f, MakeHeartbeat);

    public static AudioClip Rumble => rumble != null ? rumble : rumble = Make("Rumble", 2.5f, MakeRumble);
    // The elephant trumpet (Tunetank): when it gets angry, charges, hits, or is scared off.
    public static AudioClip Trumpet => Recording("ElephantTrumpet", ref realTrumpet)
        ?? (trumpet != null ? trumpet : trumpet = Make("Trumpet", 1.4f, MakeTrumpet));

    static AudioClip realTrumpet, realMoo;

    static AudioClip Recording(string name, ref AudioClip cache)
    {
        if (cache == null) cache = Resources.Load<AudioClip>("Sounds/" + name);
        return cache != null ? cache : null;
    }
    public static AudioClip BranchCrack => crack != null ? crack : crack = Make("BranchCrack", 0.4f, MakeCrack);
    public static AudioClip Bang => bang != null ? bang : bang = Make("Bang", 1.2f, MakeBang);
    public static AudioClip TinBang => tin != null ? tin : tin = Make("TinBang", 0.6f, MakeTin);
    public static AudioClip FireCrackle => crackle != null ? crackle : crackle = Make("FireCrackle", 2f, MakeCrackle);
    public static AudioClip Crickets => crickets != null ? crickets : crickets = Make("Crickets", 3f, MakeCrickets);
    public static AudioClip Zap => zap != null ? zap : zap = Make("Zap", 0.6f, MakeZap);
    public static AudioClip Tick => tick != null ? tick : tick = Make("Tick", 0.12f, MakeTick);
    public static AudioClip Drum => drum != null ? drum : drum = Make("Drum", 0.7f, MakeDrum);
    public static AudioClip Birds => birds != null ? birds : birds = Make("Birds", 4f, MakeBirds);
    public static AudioClip Ring => ring != null ? ring : ring = Make("Ring", 1.2f, MakeRing);
    public static AudioClip Cracker => cracker != null ? cracker : cracker = Make("Cracker", 1.4f, MakeCracker);
    public static AudioClip Trickle => trickle != null ? trickle : trickle = Make("Trickle", 3f, MakeTrickle);
    // A real cow's moo (the code-made one sounded like a buffalo).
    public static AudioClip Moo => Recording("CowMoo", ref realMoo) ?? (moo != null ? moo : moo = Make("Moo", 1.7f, MakeMoo));
    public static AudioClip Frogs => frogs != null ? frogs : frogs = Make("Frogs", 6f, MakeFrogs);
    public static AudioClip Bark => bark != null ? bark : bark = Make("Bark", 0.35f, MakeBark);
    public static AudioClip Clapper => clapper != null ? clapper : clapper = Make("Clapper", 0.9f, MakeClapper);

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

    // An elephant rumble: a low, rough, wobbling growl with breath noise. Real rumbles are very deep,
    // but laptop speakers cannot play deep bass, so the growl is carried by its higher tones
    // (140 to 400 Hz) to be heard on any speaker.
    static void MakeRumble(float[] d, System.Random r)
    {
        float len = d.Length / (float)Rate;
        float low = 0f;
        float phase = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float f = 72f + 8f * Mathf.Sin(2f * Mathf.PI * 0.7f * t);
            phase += f / Rate;
            float p = 2f * Mathf.PI * phase;
            low += 0.03f * (Noise(r) - low);
            float growl = 0.35f * Mathf.Sin(p) + 0.45f * Mathf.Sin(2f * p) + 0.4f * Mathf.Sin(3f * p)
                        + 0.3f * Mathf.Sin(4f * p) + 0.2f * Mathf.Sin(5f * p);
            float rough = 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 23f * t);
            d[i] = (float)System.Math.Tanh(1.5f * (growl * rough + 0.8f * low)) * Envelope(t, len, 0.35f, 0.8f);
        }
        Normalise(d, 0.85f);
    }

    // An elephant trumpet: a loud, rough blare that rises and falls, with breath noise.
    // The roughness (a fast flutter in loudness) keeps it from sounding like a clean beep.
    static void MakeTrumpet(float[] d, System.Random r)
    {
        float len = d.Length / (float)Rate;
        float phase = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float k = t / len;
            float f = 320f + 420f * Mathf.Sin(k * Mathf.PI) + 25f * Mathf.Sin(2f * Mathf.PI * 7f * t);
            phase += f / Rate;
            float saw = 2f * (phase - Mathf.Floor(phase + 0.5f));
            float rough = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 33f * t + 0.6f * Noise(r));
            float blare = (float)System.Math.Tanh(3.5f * saw) * rough;
            float breath = 0.45f * Noise(r) * (0.5f + 0.5f * Mathf.Exp(-t * 6f));
            d[i] = 0.4f * (blare + breath) * Envelope(t, len, 0.03f, 0.4f);
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
        // A soft cut of the top: the snap stays, the painful hiss goes (it was 45% above 6 kHz).
        float low = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            low += 0.45f * (d[i] - low);
            d[i] = low;
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
            // A deep thump, plus mid tones so it can be heard on laptop speakers too.
            float deep = low * Mathf.Exp(-t * 5f) + 0.8f * Sin(55f, t) * Mathf.Exp(-t * 12f);
            float mid = 0.6f * Sin(140f, t) * Mathf.Exp(-t * 8f) + 0.4f * Noise(r) * Mathf.Exp(-t * 20f);
            d[i] = (float)System.Math.Tanh(1.8f * (deep + mid));
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

    // Morning birds: short whistles that slide up or down, at random times and pitches.
    static void MakeBirds(float[] d, System.Random r)
    {
        float len = d.Length / (float)Rate;
        for (int call = 0; call < 14; call++)
        {
            float start = (float)r.NextDouble() * (len - 0.4f);
            float f0 = 2200f + (float)r.NextDouble() * 1800f;
            float slide = ((float)r.NextDouble() - 0.5f) * 2000f;
            float dur = 0.08f + (float)r.NextDouble() * 0.15f;
            int notes = 1 + r.Next(3);
            float phase = 0f;
            for (int n = 0; n < notes; n++)
            {
                float noteStart = start + n * (dur + 0.04f);
                int i0 = Mathf.FloorToInt(noteStart * Rate);
                int count = Mathf.FloorToInt(dur * Rate);
                for (int i = 0; i < count && i0 + i < d.Length; i++)
                {
                    float t = i / (float)Rate;
                    float freq = f0 + slide * (t / dur);
                    phase += 2f * Mathf.PI * freq / Rate;
                    float env = Mathf.Sin(Mathf.PI * t / dur);
                    d[i0 + i] += 0.3f * env * Mathf.Sin(phase);
                }
            }
        }
        Normalise(d, 0.5f);
    }

    // A firecracker (ali wedi): a loud bang with a crackle and an echo off the forest.
    // Built mostly from mid tones (150 Hz and up): laptop speakers cannot play deep bass, so a
    // bass-only bang sounds like a small click on them.
    static void MakeCracker(float[] d, System.Random r)
    {
        var pops = new float[12];
        for (int p = 0; p < pops.Length; p++) pops[p] = 0.12f + (float)r.NextDouble() * 0.55f;
        float echoAt = 0.3f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float blast = Noise(r) * Mathf.Exp(-t * 14f);
            float body = 0.8f * Sin(180f, t) * Mathf.Exp(-t * 10f) + 0.4f * Sin(320f, t) * Mathf.Exp(-t * 16f);
            float crackle = 0f;
            foreach (var p in pops)
                if (t > p) crackle += 0.5f * Noise(r) * Mathf.Exp(-(t - p) * 120f);
            float te = t - echoAt;
            float echo = te > 0f ? 0.35f * Noise(r) * Mathf.Exp(-te * 10f) : 0f;
            // Soft clipping makes it sound louder without going over full scale.
            d[i] = (float)System.Math.Tanh(2.2f * (blast + body + crackle + echo));
        }
        Normalise(d, 0.98f);
    }

    // Running water (the canal): a soft hiss of mid tones and many tiny bubbles, each a short tone
    // that rises in pitch as it fades. Bubbles that run past the end carry on at the start, and the
    // hiss swells twice per loop, so the loop has no seam.
    static void MakeTrickle(float[] d, System.Random r)
    {
        float len = d.Length / (float)Rate;
        float low = 0f, deep = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            low += 0.35f * (Noise(r) - low);    // keeps it under about 1.5 kHz
            deep += 0.04f * (low - deep);       // the deep part, taken away again
            float swell = 0.75f + 0.25f * Sin(2f / len, t);
            d[i] = 0.3f * (low - deep) * swell;
        }
        int bubbles = Mathf.RoundToInt(40f * len);
        for (int b = 0; b < bubbles; b++)
        {
            int start = (int)(r.NextDouble() * d.Length);
            float f0 = 450f + (float)(r.NextDouble() * r.NextDouble()) * 1600f;
            float fade = 0.006f + (float)r.NextDouble() * 0.02f;
            float amp = 0.25f + 0.75f * (float)r.NextDouble();
            int count = Mathf.CeilToInt(fade * 6f * Rate);
            float phase = 0f;
            for (int k = 0; k < count; k++)
            {
                float t = k / (float)Rate;
                phase += 2f * Mathf.PI * f0 * (1f + 2.5f * t / (fade * 6f)) / Rate;
                d[(start + k) % d.Length] += 0.5f * amp * Mathf.Exp(-t / fade) * Mathf.Sin(phase);
            }
        }
        Normalise(d, 0.6f);
    }

    // A cow's moo: a low hum ("mmm") that opens into "ooo", rises a little and falls away.
    // The voice is near 130 Hz, but most of the sound is in its overtones (300 to 900 Hz, where the
    // mouth shapes it), so laptop speakers play it.
    static void MakeMoo(float[] d, System.Random r)
    {
        float len = d.Length / (float)Rate;
        float phase = 0f, breath = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float k = t / len;
            float f = 118f + 32f * Mathf.Sin(Mathf.PI * Mathf.Min(k * 1.5f, 1f)) - 18f * k + 2f * Mathf.Sin(2f * Mathf.PI * 5f * t);
            phase += f / Rate;
            float open = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.05f, 0.3f, k)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 1f, k)));
            float centre = 280f + 380f * open;
            float s = 0f;
            for (int h = 1; h <= 12; h++)
            {
                float fh = f * h;
                float w = Mathf.Exp(-Mathf.Pow((fh - centre) / 230f, 2f)) + 0.3f * Mathf.Exp(-Mathf.Pow((fh - centre * 2.6f) / 420f, 2f)) + 0.12f / h;
                s += w * Mathf.Sin(2f * Mathf.PI * h * phase);
            }
            breath += 0.2f * (Noise(r) - breath);
            d[i] = (s + 0.15f * breath * open) * Envelope(t, len, 0.15f, 0.45f);
        }
        Normalise(d, 0.8f);
    }

    // Frogs at night near water: five frogs, each croaking now and then in its own voice. A croak
    // is a quick run of clicks (a rattle) on a 500 to 1200 Hz tone. Croaks past the end carry on at
    // the start, so the loop has no seam.
    static void MakeFrogs(float[] d, System.Random r)
    {
        for (int frog = 0; frog < 5; frog++)
        {
            float tone = 500f + (float)r.NextDouble() * 700f;
            float pulseRate = 22f + (float)r.NextDouble() * 20f;
            int pulses = 6 + r.Next(8);
            float gap = 0.5f + (float)r.NextDouble() * 1.2f;
            float loud = 0.4f + 0.6f * (float)r.NextDouble();
            for (float start = (float)r.NextDouble() * gap; start < d.Length / (float)Rate; start += gap * (0.7f + 0.6f * (float)r.NextDouble()))
            {
                int i0 = (int)(start * Rate);
                int count = (int)(pulses / pulseRate * Rate);
                for (int k = 0; k < count; k++)
                {
                    float t = k / (float)Rate;
                    float inPulse = (t * pulseRate) % 1f;
                    float env = Mathf.Exp(-inPulse * 9f) * Mathf.Sin(Mathf.PI * Mathf.Min(1f, t * pulseRate / pulses * 1.2f));
                    d[(i0 + k) % d.Length] += loud * env * (Sin(tone, t) + 0.4f * Sin(tone * 2.1f, t));
                }
            }
        }
        Normalise(d, 0.6f);
    }

    // A village dog's bark: a sharp "woof" that drops in pitch, rough with breath noise. Its tones
    // are 300 to 1500 Hz, so it carries on laptop speakers.
    static void MakeBark(float[] d, System.Random r)
    {
        float len = d.Length / (float)Rate;
        float phase = 0f, rough = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float f = 520f - 260f * Mathf.Min(1f, t / 0.18f);
            phase += f / Rate;
            float p = 2f * Mathf.PI * phase;
            float voice = Mathf.Sin(p) + 0.7f * Mathf.Sin(2f * p) + 0.5f * Mathf.Sin(3f * p) + 0.25f * Mathf.Sin(4f * p);
            rough += 0.5f * (Noise(r) - rough);
            float env = Mathf.Min(1f, t / 0.008f) * Mathf.Exp(-t * 11f);
            d[i] = (float)System.Math.Tanh(1.6f * (voice + 0.8f * rough)) * env * Envelope(t, len, 0.001f, 0.05f);
        }
        Normalise(d, 0.85f);
    }

    // A scarecrow's tin clapper: a can knocking against its stick three or four times.
    static void MakeClapper(float[] d, System.Random r)
    {
        int knocks = 3 + r.Next(2);
        float[] tones = { 880f, 1460f, 2270f };
        for (int k = 0; k < knocks; k++)
        {
            int i0 = (int)((0.04f + k * 0.17f + 0.04f * (float)r.NextDouble()) * Rate);
            float amp = 1f - 0.18f * k;
            for (int i = 0; i + i0 < d.Length; i++)
            {
                float t = i / (float)Rate;
                float s = 0f;
                for (int n = 0; n < tones.Length; n++) s += Sin(tones[n] * (1f + 0.01f * k), t) * Mathf.Exp(-t * (28f + 10f * n)) / (n + 1);
                if (t < 0.004f) s += Noise(r);
                d[i0 + i] += amp * s;
            }
        }
        Normalise(d, 0.6f);
    }

    // A phone ringing: two quick tones, twice.
    static void MakeRing(float[] d, System.Random r)
    {
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float inCycle = t % 0.6f;
            bool on = inCycle < 0.4f;
            d[i] = on ? 0.5f * (Sin(440f, t) + Sin(480f, t)) * (Sin(20f, t) > 0f ? 1f : 0.3f) : 0f;
        }
        Normalise(d, 0.5f);
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

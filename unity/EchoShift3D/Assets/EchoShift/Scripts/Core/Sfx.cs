using System;
using System.Collections.Generic;
using UnityEngine;

namespace EchoShift
{
    /// <summary>Every sound in the game is synthesized at startup: no audio files needed.</summary>
    public static class Sfx
    {
        const int Rate = 44100;
        static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();
        static readonly Dictionary<int, AudioClip> Notes = new Dictionary<int, AudioClip>();
        static readonly System.Random Rng = new System.Random(731);
        static readonly float[] UnlockNotes = { 523f, 659f, 784f, 1046f };
        static readonly float[] VictoryNotes = { 523f, 659f, 784f, 1046f, 1318f };
        static AudioSource[] pool;
        static int next;

        delegate float Gen(float t, float phase);

        public static void Init()
        {
            if (pool != null) return;
            var host = new GameObject("[Sfx]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            pool = new AudioSource[16];
            for (int i = 0; i < pool.Length; i++)
            {
                pool[i] = host.AddComponent<AudioSource>();
                pool[i].playOnAwake = false;
                pool[i].spatialBlend = 0f;
            }
            Build();
        }

        static void Build()
        {
            Clips["blip"] = Make(0.07f, _ => 880, (t, p) => Sq(p) * 0.15f);
            Clips["micOn"] = Make(0.16f, t => t < 0.07f ? 520 : 1040, (t, p) => Sin(p) * 0.4f);
            Clips["micOff"] = Make(0.16f, t => t < 0.07f ? 1040 : 620, (t, p) => Sin(p) * 0.4f);
            Clips["accept"] = Make(0.36f, t => t < 0.12f ? 660 : t < 0.24f ? 880 : 1320, (t, p) => Tri(p) * 0.35f);
            Clips["deny"] = Make(0.32f, t => Mathf.Lerp(220, 130, t / 0.32f), (t, p) => Saw(p) * 0.22f);
            Clips["jump"] = Make(0.22f, t => Mathf.Lerp(260, 720, t / 0.22f), (t, p) => Sq(p) * 0.1f);
            Clips["step"] = Make(0.05f, _ => 90, (t, p) => (Noise() * 0.5f + Sin(p) * 0.4f) * Mathf.Exp(-t * 70f));
            Clips["hit"] = Make(0.45f, t => Mathf.Lerp(170, 50, t / 0.45f), (t, p) => Noise() * 0.5f * Mathf.Exp(-t * 6f) + Saw(p) * 0.4f);
            Clips["shatter"] = Make(1.3f, t => 2600 + Mathf.Sin(t * 90f) * 900, (t, p) => Noise() * 0.8f * Mathf.Exp(-t * 3.5f) + Tri(p) * 0.18f * Mathf.Exp(-t * 5f));
            Clips["unlock"] = Make(0.62f, t => UnlockNotes[Mathf.Min(3, (int)(t / 0.13f))], (t, p) => Tri(p) * 0.35f);
            Clips["objective"] = Make(0.34f, t => t < 0.12f ? 784 : 1175, (t, p) => Sin(p) * 0.4f);
            Clips["victory"] = Make(1.4f, t => VictoryNotes[Mathf.Min(4, (int)(t / 0.2f))], (t, p) => (Tri(p) * 0.3f + Sin(p * 2f) * 0.1f));
            Clips["teleport"] = Make(1.3f, t => Mathf.Lerp(110, 1900, t / 1.3f), (t, p) => Saw(p) * 0.18f + Noise() * 0.08f * t);
            Clips["rewind"] = Make(1.1f, t => Mathf.Lerp(1700, 80, t / 1.1f), (t, p) => Saw(p) * 0.22f + Sin(p * 0.5f) * 0.2f);
            Clips["build"] = Make(0.95f, t => Mathf.Lerp(180, 960, t / 0.95f), (t, p) => Sin(p) * 0.28f + Sq(p * 2f) * 0.04f);
            Clips["alarm"] = Make(1.6f, t => 700 + Mathf.Sin(t * Mathf.PI * 6f) * 260, (t, p) => Saw(p) * 0.2f);
            Clips["freeze"] = Make(0.5f, t => Mathf.Lerp(600, 90, t / 0.5f), (t, p) => Sin(p) * 0.3f);
            Clips["thaw"] = Make(0.35f, t => Mathf.Lerp(120, 700, t / 0.35f), (t, p) => Sin(p) * 0.25f);
            Clips["door"] = Make(0.6f, t => Mathf.Lerp(90, 60, t / 0.6f), (t, p) => (Noise() * 0.25f + Saw(p) * 0.15f) * Mathf.Exp(-t * 3f));
            // Seamless loops: every frequency completes an integer number of cycles over the clip.
            Clips["ambience"] = Make(4f, _ => 55, (t, p) => Sin(p) * 0.12f + Mathf.Sin(t * 2 * Mathf.PI * 82.5f) * 0.06f + Mathf.Sin(t * 2 * Mathf.PI * 110f) * 0.03f + Noise() * 0.01f, false);
            Clips["laserHum"] = Make(1f, _ => 120, (t, p) => Saw(p) * 0.05f + Mathf.Sin(t * 2 * Mathf.PI * 240f) * 0.04f, false);
        }

        static AudioClip Make(float duration, Func<float, float> freq, Gen gen, bool envelope = true)
        {
            int n = Mathf.CeilToInt(duration * Rate);
            var data = new float[n];
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                phase += freq(t) / Rate;
                float s = gen(t, (float)(phase - Math.Floor(phase)));
                if (envelope) s *= Mathf.Clamp01(t / 0.004f) * Mathf.Clamp01((duration - t) / 0.06f);
                data[i] = Mathf.Clamp(s, -1f, 1f);
            }
            var clip = AudioClip.Create("sfx", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float Sin(float p) => Mathf.Sin(p * 2f * Mathf.PI);
        static float Sq(float p) => (p - Mathf.Floor(p)) < 0.5f ? 1f : -1f;
        static float Saw(float p) => 2f * (p - Mathf.Floor(p)) - 1f;
        static float Tri(float p) => 1f - 4f * Mathf.Abs((p - Mathf.Floor(p)) - 0.5f);
        static float Noise() => (float)(Rng.NextDouble() * 2.0 - 1.0);

        public static AudioClip Get(string id) => Clips.TryGetValue(id, out var c) ? c : null;

        public static void Play(string id, float volume = 1f, float pitch = 1f)
        {
            if (pool == null || !Clips.TryGetValue(id, out var clip)) return;
            var src = pool[next];
            next = (next + 1) % pool.Length;
            src.pitch = pitch;
            src.PlayOneShot(clip, volume);
        }

        public static void PlayAt(string id, Vector3 position, float volume = 1f)
        {
            if (Clips.TryGetValue(id, out var clip)) AudioSource.PlayClipAtPoint(clip, position, volume);
        }

        /// <summary>A pure musical note, used by the resonance vault to play back what you should hum.</summary>
        public static void Note(float frequency, float volume = 0.5f, float duration = 0.6f)
        {
            int key = Mathf.RoundToInt(frequency);
            if (!Notes.TryGetValue(key, out var clip))
            {
                clip = Make(duration, _ => frequency, (t, p) => (Sin(p) * 0.35f + Sin(p * 2f) * 0.12f + Sin(p * 3f) * 0.05f) * Mathf.Exp(-t * 2.2f));
                Notes[key] = clip;
            }
            if (pool == null) return;
            var src = pool[next];
            next = (next + 1) % pool.Length;
            src.pitch = 1f;
            src.PlayOneShot(clip, volume);
        }

        public static AudioSource Loop(string id, GameObject host, float volume, bool spatial, float maxDistance = 30f)
        {
            if (!Clips.TryGetValue(id, out var clip)) return null;
            var src = host.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = true;
            src.volume = volume;
            src.spatialBlend = spatial ? 1f : 0f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.maxDistance = maxDistance;
            src.minDistance = 1.5f;
            src.Play();
            return src;
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EchoShift
{
    /// <summary>A holographic replay of a previous run that also speaks the player's recorded voice.</summary>
    public sealed class EchoClone : MonoBehaviour
    {
        List<EchoSample> samples;
        List<EchoVoice> voices;
        SubjectRig rig;
        AudioSource voice;
        WorldLabel tag;
        float t;
        int cursor, voiceCursor;
        bool dissolving;

        static readonly Color[] Tints = { new Color(0.75f, 0.45f, 1f), new Color(1f, 0.45f, 0.8f), new Color(0.45f, 0.7f, 1f) };

        public float Speed { get; private set; }
        public bool Grounded { get; private set; } = true;
        public bool Crouching { get; private set; }

        public void Init(List<EchoSample> recorded, List<EchoVoice> recordedVoices, int index)
        {
            samples = recorded;
            voices = recordedVoices;
            var tint = Tints[index % Tints.Length];
            rig = SubjectRig.Build(transform, true, tint);
            rig.Footsteps = false;
            if (SideView.Enabled)
            {
                var ghost = tint;
                ghost.a = 0.6f;
                RobotSprite.Attach(transform, rig.transform, ghost, () => (Speed, Grounded, Crouching, false));
            }

            voice = gameObject.AddComponent<AudioSource>();
            voice.spatialBlend = 1f;
            voice.minDistance = 2f;
            voice.maxDistance = 30f;
            voice.pitch = 0.93f;
            var echo = gameObject.AddComponent<AudioEchoFilter>();
            echo.delay = 260f;
            echo.decayRatio = 0.45f;
            echo.wetMix = 0.6f;
            gameObject.AddComponent<AudioReverbFilter>().reverbPreset = AudioReverbPreset.Hallway;

            var trail = gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.8f;
            trail.startWidth = 0.35f;
            trail.endWidth = 0f;
            trail.sharedMaterial = Mats.Hologram(tint, 1.5f, 0.35f);
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            tag = WorldLabel.Create(transform, new Vector3(0, 2.25f, 0), "ECHO", 0.26f, tint, 2.2f, true, true);
            Restart();
        }

        public void Restart()
        {
            t = 0f;
            cursor = 0;
            voiceCursor = 0;
            Apply(samples[0], samples[0], 0f, 0f);
        }

        void Update()
        {
            if (samples == null || samples.Count < 2 || dissolving) return;
            float dt = Time.deltaTime;
            t += dt;
            while (cursor < samples.Count - 2 && samples[cursor + 1].T < t) cursor++;
            var a = samples[cursor];
            var b = samples[cursor + 1];
            float k = t >= b.T ? 1f : Mathf.InverseLerp(a.T, b.T, t);
            bool finished = t > samples[samples.Count - 1].T;
            Apply(a, b, k, finished ? 0f : dt);
            if (finished) rig.Drive(0f, true, b.Crouching, 0f, dt);

            while (voiceCursor < voices.Count && voices[voiceCursor].T <= t)
            {
                var clip = voices[voiceCursor++].Clip;
                if (clip) voice.PlayOneShot(clip, 1f);
            }
        }

        void Apply(EchoSample a, EchoSample b, float k, float dt)
        {
            transform.position = Vector3.Lerp(a.Position, b.Position, k);
            transform.rotation = Quaternion.Euler(0, Mathf.LerpAngle(a.Yaw, b.Yaw, k), 0);
            Speed = dt > 0f ? Mathf.Lerp(a.Speed, b.Speed, k) : 0f;
            Grounded = b.Grounded;
            Crouching = b.Crouching;
            if (dt > 0f) rig.Drive(Mathf.Lerp(a.Speed, b.Speed, k), b.Grounded, b.Crouching, b.VerticalVelocity, dt);
        }

        public void Dissolve()
        {
            if (dissolving || !this) return;
            dissolving = true;
            StartCoroutine(Fade());
        }

        IEnumerator Fade()
        {
            float s = 1f;
            while (s > 0.01f)
            {
                s -= Time.unscaledDeltaTime * 2f;
                transform.localScale = new Vector3(1f, Mathf.Max(0.01f, s), 1f);
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}

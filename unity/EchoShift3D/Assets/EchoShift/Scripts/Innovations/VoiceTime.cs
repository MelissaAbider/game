using System;
using UnityEngine;

namespace EchoShift
{
    /// <summary>
    /// Chrono chamber rule: time flows only while the player makes sound. Silence freezes the world
    /// (lasers, the subject, physics) so you can plan; humming, talking or singing lets it run,
    /// and louder means faster. Hold F to flow time without a microphone.
    /// </summary>
    public sealed class VoiceTime : MonoBehaviour
    {
        public static VoiceTime I { get; private set; }

        public Func<bool> InChamber = () => false;
        public bool Active { get; private set; }
        public float Flow { get; private set; } = 1f;

        bool wasFrozen;

        void Awake() => I = this;

        void Update()
        {
            bool active = InChamber() && !(GameDirector.I && GameDirector.I.InCinematic);
            if (active != Active)
            {
                Active = active;
                if (active) GameDirector.I?.OnChronoEntered();
            }

            float target = 1f;
            if (Active)
            {
                var v = VoiceInput.I;
                float level = v ? v.EffectiveLevel : 0f;
                float floor = v ? v.NoiseFloor : 0.003f;
                float voice = Mathf.InverseLerp(floor * 2.2f, Mathf.Max(floor * 7f, 0.02f), level);
                if (Input.GetKey(KeyCode.F)) voice = 1f;
                target = Mathf.Lerp(0.015f, 1f, voice);
            }

            Flow = Mathf.MoveTowards(Flow, target, (target > Flow ? 7f : 2.2f) * Time.unscaledDeltaTime);
            Time.timeScale = Flow;
            PostFx.SetFrozen(Active ? 1f - Flow : 0f);

            bool frozen = Active && Flow < 0.2f;
            if (frozen != wasFrozen)
            {
                wasFrozen = frozen;
                Sfx.Play(frozen ? "freeze" : "thaw", 0.35f);
            }
        }

        void OnDisable()
        {
            Time.timeScale = 1f;
            PostFx.SetFrozen(0f);
        }
    }
}

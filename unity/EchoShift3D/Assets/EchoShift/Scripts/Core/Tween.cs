using System;
using System.Collections;
using UnityEngine;

namespace EchoShift
{
    /// <summary>Coroutine tweens. Unscaled by default so UI keeps animating while voice-time freezes the world.</summary>
    public static class Tween
    {
        public static Coroutine Run(MonoBehaviour host, float duration, Action<float> step, Action done = null, bool unscaled = true)
        {
            if (!host || !host.isActiveAndEnabled) { step?.Invoke(1f); done?.Invoke(); return null; }
            return host.StartCoroutine(Routine(duration, step, done, unscaled));
        }

        static IEnumerator Routine(float duration, Action<float> step, Action done, bool unscaled)
        {
            float t = 0f;
            while (t < duration)
            {
                t += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
                step?.Invoke(Mathf.Clamp01(t / duration));
                yield return null;
            }
            step?.Invoke(1f);
            done?.Invoke();
        }

        public static Coroutine Delay(MonoBehaviour host, float seconds, Action action, bool unscaled = true)
            => Run(host, seconds, null, action, unscaled);

        public static float OutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);
        public static float InOutSine(float x) => -(Mathf.Cos(Mathf.PI * x) - 1f) / 2f;

        public static float OutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }
    }
}

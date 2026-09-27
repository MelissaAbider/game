using UnityEngine;

namespace EchoShift
{
    /// <summary>YIN fundamental-frequency estimator, tuned for humming (70–900 Hz).</summary>
    public sealed class PitchDetector
    {
        float[] diff = new float[1024];

        public bool Detect(float[] x, int n, int sampleRate, out float hz, out float clarity, float minHz = 70f, float maxHz = 900f)
        {
            hz = 0f;
            clarity = 0f;
            int tauMin = Mathf.Max(2, Mathf.FloorToInt(sampleRate / maxHz));
            int tauMax = Mathf.Min(Mathf.CeilToInt(sampleRate / minHz), n / 2);
            if (tauMax <= tauMin + 2) return false;
            if (diff.Length <= tauMax + 1) diff = new float[tauMax + 2];

            int window = n - tauMax;
            for (int tau = 1; tau <= tauMax; tau++)
            {
                float sum = 0f;
                for (int j = 0; j < window; j++)
                {
                    float d = x[j] - x[j + tau];
                    sum += d * d;
                }
                diff[tau] = sum;
            }

            // Cumulative mean normalized difference.
            diff[0] = 1f;
            float running = 0f;
            for (int tau = 1; tau <= tauMax; tau++)
            {
                running += diff[tau];
                diff[tau] = running > 0f ? diff[tau] * tau / running : 1f;
            }

            int best = -1;
            for (int tau = tauMin; tau <= tauMax; tau++)
            {
                if (diff[tau] < 0.15f)
                {
                    while (tau + 1 <= tauMax && diff[tau + 1] < diff[tau]) tau++;
                    best = tau;
                    break;
                }
            }
            if (best < 0)
            {
                float min = float.MaxValue;
                for (int tau = tauMin; tau <= tauMax; tau++)
                    if (diff[tau] < min) { min = diff[tau]; best = tau; }
                if (min > 0.35f) return false;
            }

            float refined = best;
            if (best > 1 && best < tauMax)
            {
                float a = diff[best - 1], b = diff[best], c = diff[best + 1];
                float denom = 2f * (a - 2f * b + c);
                if (Mathf.Abs(denom) > 1e-6f) refined = best + (a - c) / denom;
            }
            hz = sampleRate / refined;
            clarity = Mathf.Clamp01(1f - diff[best]);
            return hz >= minHz && hz <= maxHz;
        }

        public static float Semitones(float hz, float referenceHz) => 12f * Mathf.Log(hz / referenceHz, 2f);
    }
}

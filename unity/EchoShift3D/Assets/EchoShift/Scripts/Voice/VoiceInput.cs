using System;
using System.Collections.Generic;
using UnityEngine;

namespace EchoShift
{
    /// <summary>
    /// Always-on microphone. Continuously measures loudness and pitch (for voice-time, shout/whisper
    /// and musical locks) and, while push-to-talk is held, streams 24 kHz PCM16 to speech-to-text
    /// and keeps the raw utterance so echo clones can replay the player's own voice.
    /// </summary>
    public sealed class VoiceInput : MonoBehaviour
    {
        public static VoiceInput I { get; private set; }

        public bool Available { get; private set; }
        public string DeviceName { get; private set; }
        public int SampleRate { get; private set; }
        public float Level { get; private set; }
        public float NoiseFloor { get; private set; } = 0.003f;
        public float SpeechBaseline { get; private set; } = 0.05f;
        public float PitchHz { get; private set; }
        public float PitchClarity { get; private set; }
        public bool Capturing { get; private set; }
        public float CapturePeak { get; private set; }

        /// <summary>Raised on the main thread with PCM16 little-endian bytes at 24 kHz while capturing.</summary>
        public event Action<byte[]> PcmChunk;

        /// <summary>ECHO's own voice leaking from the speakers must not count as the player's voice.</summary>
        public float SuppressedUntil { get; set; }
        public bool Suppressed => Time.unscaledTime < SuppressedUntil;
        // While the player holds the mic, ECHO has been cut off (barge-in), so the level is always the player's.
        public float EffectiveLevel => Suppressed && !Capturing ? 0f : Level;
        public bool HasPitch => !Suppressed && PitchHz > 0f && PitchClarity > 0.72f && Level > NoiseFloor * 2.5f;

        public float WhisperMax => Mathf.Clamp(SpeechBaseline * 0.5f, NoiseFloor * 2.5f, 0.05f);
        // SpeechBaseline is a *peak* of normal speech, so 1.5x is already a clear shout; the cap keeps it reachable.
        public float ShoutMin => Mathf.Clamp(SpeechBaseline * 1.5f, 0.06f, 0.25f);

        AudioClip clip;
        int lastPos;
        readonly float[] analysis = new float[2048];
        readonly float[] downsampled = new float[1024];
        readonly List<float> utterance = new List<float>(48000 * 8);
        readonly PitchDetector pitch = new PitchDetector();
        double resamplePos;
        int calibratedUtterances;
        int frame;

        void Awake() => I = this;

        /// <summary>Autotest only: fake the microphone level so demo captures work without a real voice.</summary>
        public void Simulate(float level, bool resetPeak = false)
        {
            if (resetPeak) CapturePeak = 0f;
            Level = level;
            CapturePeak = Mathf.Max(CapturePeak, level);
        }

        public bool StartMic()
        {
            if (Available) return true;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
            {
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
                return false;
            }
#endif
            if (Microphone.devices.Length == 0)
            {
                Debug.LogWarning("[Voice] No microphone found.");
                return false;
            }
            DeviceName = Microphone.devices[0];
            Microphone.GetDeviceCaps(DeviceName, out int min, out int max);
            SampleRate = (min == 0 && max == 0) ? 48000 : Mathf.Clamp(48000, min, max);
            clip = Microphone.Start(DeviceName, true, 4, SampleRate);
            Available = clip != null;
            lastPos = 0;
            Debug.Log($"[Voice] Microphone '{DeviceName}' @ {SampleRate} Hz");
            return Available;
        }

        void OnDestroy()
        {
            if (Available) Microphone.End(DeviceName);
        }

        void Update()
        {
            if (!Available) return;
            int pos = Microphone.GetPosition(DeviceName);
            if (pos < 0) return;
            int total = clip.samples;
            int count = pos - lastPos;
            if (count < 0) count += total;
            if (count <= 0) return;
            if (count > SampleRate) count = SampleRate;

            var samples = Read((pos - count + total) % total, count, total);
            lastPos = pos;
            Process(samples);
        }

        float[] Read(int start, int count, int total)
        {
            var result = new float[count];
            int first = Mathf.Min(count, total - start);
            var a = new float[first];
            clip.GetData(a, start);
            Array.Copy(a, result, first);
            if (first < count)
            {
                var b = new float[count - first];
                clip.GetData(b, 0);
                Array.Copy(b, 0, result, first, b.Length);
            }
            return result;
        }

        void Process(float[] samples)
        {
            double sum = 0;
            for (int i = 0; i < samples.Length; i++) sum += samples[i] * samples[i];
            float rms = (float)Math.Sqrt(sum / samples.Length);
            Level = Mathf.Lerp(Level, rms, rms > Level ? 0.7f : 0.3f);

            if (!Capturing && !Suppressed)
            {
                float rate = rms < NoiseFloor ? 0.1f : 0.0015f;
                NoiseFloor = Mathf.Clamp(Mathf.Lerp(NoiseFloor, rms, rate), 0.0005f, 0.03f);
            }

            // Sliding analysis window for pitch.
            int n = analysis.Length;
            if (samples.Length >= n) Array.Copy(samples, samples.Length - n, analysis, 0, n);
            else
            {
                Array.Copy(analysis, samples.Length, analysis, 0, n - samples.Length);
                Array.Copy(samples, 0, analysis, n - samples.Length, samples.Length);
            }
            if ((++frame & 1) == 0) DetectPitch();

            if (!Capturing) return;
            CapturePeak = Mathf.Max(CapturePeak, rms);
            utterance.AddRange(samples);
            var pcm = ToPcm24k(samples);
            if (pcm.Length > 0) PcmChunk?.Invoke(pcm);
        }

        void DetectPitch()
        {
            if (Level < NoiseFloor * 2f)
            {
                PitchHz = 0f;
                PitchClarity = 0f;
                return;
            }
            int factor = Mathf.Max(1, SampleRate / 24000);
            int len = analysis.Length / factor;
            for (int i = 0; i < len; i++)
            {
                float acc = 0f;
                for (int k = 0; k < factor; k++) acc += analysis[i * factor + k];
                downsampled[i] = acc / factor;
            }
            if (pitch.Detect(downsampled, len, SampleRate / factor, out float hz, out float clarity))
            {
                PitchHz = PitchHz > 0f && Mathf.Abs(PitchDetector.Semitones(hz, PitchHz)) < 1f ? Mathf.Lerp(PitchHz, hz, 0.5f) : hz;
                PitchClarity = clarity;
            }
            else
            {
                PitchHz = 0f;
                PitchClarity = 0f;
            }
        }

        byte[] ToPcm24k(float[] samples)
        {
            double step = SampleRate / 24000.0;
            var output = new List<byte>(samples.Length);
            while (resamplePos < samples.Length)
            {
                int i0 = (int)resamplePos;
                int i1 = Mathf.Min(i0 + 1, samples.Length - 1);
                float frac = (float)(resamplePos - i0);
                float s = Mathf.Clamp(samples[i0] * (1f - frac) + samples[i1] * frac, -1f, 1f);
                short v = (short)(s < 0 ? s * 32768f : s * 32767f);
                output.Add((byte)(v & 0xff));
                output.Add((byte)((v >> 8) & 0xff));
                resamplePos += step;
            }
            resamplePos -= samples.Length;
            return output.ToArray();
        }

        public void BeginCapture()
        {
            Capturing = true;
            CapturePeak = 0f;
            utterance.Clear();
            resamplePos = 0;
        }

        /// <summary>Stops capturing, learns the player's normal speaking level, and returns the raw utterance.</summary>
        public AudioClip EndCapture(bool learnLevel)
        {
            Capturing = false;
            if (learnLevel && CapturePeak > NoiseFloor * 3f)
            {
                SpeechBaseline = calibratedUtterances == 0 ? CapturePeak : Mathf.Lerp(SpeechBaseline, CapturePeak, 0.35f);
                calibratedUtterances++;
            }
            if (utterance.Count < SampleRate / 5) return null;
            var voiceClip = AudioClip.Create("player_voice", utterance.Count, 1, SampleRate, false);
            voiceClip.SetData(utterance.ToArray(), 0);
            return voiceClip;
        }
    }
}

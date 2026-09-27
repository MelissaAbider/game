using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace EchoShift
{
    /// <summary>ECHO, the lab AI: text-to-speech through the backend /ws/tts bridge (Gradium).</summary>
    public sealed class EchoSpeaker : MonoBehaviour
    {
        public static EchoSpeaker I { get; private set; }

        AudioSource source;
        int token;
        readonly float[] spectrum = new float[64];

        public bool Speaking => source && source.isPlaying;

        void Awake()
        {
            I = this;
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0.95f;
            var hp = gameObject.AddComponent<AudioHighPassFilter>();
            hp.cutoffFrequency = 180f;
        }

        /// <summary>Current output loudness 0..1, used to animate ECHO's waveform in the HUD.</summary>
        public float Amplitude
        {
            get
            {
                if (!Speaking) return 0f;
                source.GetOutputData(spectrum, 0);
                float peak = 0f;
                foreach (var s in spectrum) peak = Mathf.Max(peak, Mathf.Abs(s));
                return Mathf.Clamp01(peak * 2.2f);
            }
        }

        /// <summary>Barge-in: the player started talking, ECHO goes quiet immediately (and stops muting the mic).</summary>
        public void Stop()
        {
            token++;
            if (source) source.Stop();
            if (VoiceInput.I) VoiceInput.I.SuppressedUntil = 0f;
        }

        public void Say(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            int mine = ++token;
            _ = Synthesize(text, mine);
        }

        async Task Synthesize(string text, int mine)
        {
            var chunks = new List<byte[]>();
            try
            {
                using (var ws = new ClientWebSocket())
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(12)))
                {
                    await ws.ConnectAsync(new Uri(Config.Ws("/ws/tts")), cts.Token);
                    var payload = Encoding.UTF8.GetBytes("{\"text\":" + Json.Quote(text) + "}");
                    await ws.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Text, true, cts.Token);
                    var buffer = new byte[65536];
                    var current = new List<byte>();
                    while (ws.State == WebSocketState.Open)
                    {
                        var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
                        if (result.MessageType == WebSocketMessageType.Close) break;
                        if (result.MessageType == WebSocketMessageType.Text) break; // {"type":"end"}
                        for (int i = 0; i < result.Count; i++) current.Add(buffer[i]);
                        if (result.EndOfMessage)
                        {
                            chunks.Add(current.ToArray());
                            current.Clear();
                        }
                    }
                    try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None); }
                    catch { /* ignore */ }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ECHO TTS] " + e.Message);
                return;
            }

            int total = 0;
            foreach (var c in chunks) total += c.Length;
            var wav = new byte[total];
            int offset = 0;
            foreach (var c in chunks) { Buffer.BlockCopy(c, 0, wav, offset, c.Length); offset += c.Length; }
            MainThread.Post(() => Play(wav, mine));
        }

        void Play(byte[] wav, int mine)
        {
            if (mine != token || !this) return; // a newer line superseded this one
            var clip = WavToClip(wav);
            if (!clip) return;
            source.Stop();
            source.clip = clip;
            source.Play();
            if (VoiceInput.I) VoiceInput.I.SuppressedUntil = Time.unscaledTime + clip.length + 0.35f;
        }

        /// <summary>Parses streamed WAV (header sizes are placeholders, so the data length is taken from the buffer).</summary>
        public static AudioClip WavToClip(byte[] wav)
        {
            if (wav == null || wav.Length < 44) return null;
            int channels = BitConverter.ToInt16(wav, 22);
            int rate = BitConverter.ToInt32(wav, 24);
            int bits = BitConverter.ToInt16(wav, 34);
            int pos = 12, dataStart = -1;
            while (pos + 8 <= wav.Length)
            {
                string id = Encoding.ASCII.GetString(wav, pos, 4);
                int size = BitConverter.ToInt32(wav, pos + 4);
                if (id == "data") { dataStart = pos + 8; break; }
                if (size < 0 || size > wav.Length) break;
                pos += 8 + size;
            }
            if (dataStart < 0 || bits != 16 || channels < 1) return null;
            int sampleCount = (wav.Length - dataStart) / 2;
            var data = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++) data[i] = BitConverter.ToInt16(wav, dataStart + i * 2) / 32768f;
            var clip = AudioClip.Create("echo_line", sampleCount / channels, channels, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}

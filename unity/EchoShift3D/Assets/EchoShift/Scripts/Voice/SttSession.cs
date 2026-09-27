using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace EchoShift
{
    /// <summary>
    /// One push-to-talk utterance streamed to the backend /ws/stt bridge (Gradium). Audio pushed before
    /// the socket is open is buffered, so releasing the key early never leaks a server session.
    /// All callbacks are delivered on the main thread.
    /// </summary>
    public sealed class SttSession
    {
        public event Action<string> Partial;
        public event Action<string> Final;
        public event Action<string> Error;

        readonly ClientWebSocket socket = new ClientWebSocket();
        readonly CancellationTokenSource cts = new CancellationTokenSource();
        readonly ConcurrentQueue<byte[]> outbox = new ConcurrentQueue<byte[]>();
        readonly SemaphoreSlim signal = new SemaphoreSlim(0);
        readonly StringBuilder partial = new StringBuilder();
        volatile bool ending;
        int finished;

        public void Start()
        {
            _ = Run();
        }

        public void Push(byte[] pcm)
        {
            if (ending) return;
            outbox.Enqueue(pcm);
            signal.Release();
        }

        public void End()
        {
            ending = true;
            signal.Release();
            // Don't wait for the server's flush confirmation forever: if words already arrived, a short grace
            // for the last one is enough; with nothing heard yet, give the recogniser a bit longer.
            int grace;
            lock (partial) grace = partial.Length > 0 ? 1100 : 3000;
            Task.Delay(grace).ContinueWith(_ => { string text; lock (partial) text = partial.ToString().Trim(); Finish(text); });
        }

        async Task Run()
        {
            try
            {
                await socket.ConnectAsync(new Uri(Config.Ws("/ws/stt")), cts.Token);
                var receive = ReceiveLoop();
                await SendLoop();
                await receive;
            }
            catch (Exception e)
            {
                if (Interlocked.CompareExchange(ref finished, 1, 0) == 0)
                    MainThread.Post(() => Error?.Invoke("Voice backend unreachable: " + e.Message));
            }
            finally
            {
                try { if (socket.State == WebSocketState.Open) await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None); }
                catch { /* already closed */ }
                socket.Dispose();
            }
        }

        async Task SendLoop()
        {
            while (!cts.IsCancellationRequested)
            {
                await signal.WaitAsync(cts.Token);
                while (outbox.TryDequeue(out var chunk))
                    await socket.SendAsync(new ArraySegment<byte>(chunk), WebSocketMessageType.Binary, true, cts.Token);
                if (ending && outbox.IsEmpty)
                {
                    var end = Encoding.UTF8.GetBytes("{\"type\":\"end\"}");
                    await socket.SendAsync(new ArraySegment<byte>(end), WebSocketMessageType.Text, true, cts.Token);
                    return;
                }
            }
        }

        async Task ReceiveLoop()
        {
            var buffer = new byte[16384];
            var message = new StringBuilder();
            while (socket.State == WebSocketState.Open && !cts.IsCancellationRequested)
            {
                WebSocketReceiveResult result;
                try { result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token); }
                catch { break; }
                if (result.MessageType == WebSocketMessageType.Close) break;
                message.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                if (!result.EndOfMessage) continue;

                var data = Json.ParseObject(message.ToString());
                message.Clear();
                var type = data.Str("type");
                if (type == "text")
                {
                    string snapshot;
                    lock (partial)
                    {
                        if (partial.Length > 0) partial.Append(' ');
                        partial.Append(data.Str("text"));
                        snapshot = partial.ToString();
                    }
                    MainThread.Post(() => Partial?.Invoke(snapshot));
                }
                else if (type == "error")
                {
                    var msg = data.Str("message", "speech error");
                    MainThread.Post(() => Error?.Invoke(msg));
                }
                else if (type == "final")
                {
                    string fallback;
                    lock (partial) fallback = partial.ToString();
                    Finish(data.Str("transcript", fallback).Trim());
                    break;
                }
            }
            string rest;
            lock (partial) rest = partial.ToString();
            Finish(rest.Trim());
        }

        void Finish(string transcript)
        {
            if (Interlocked.CompareExchange(ref finished, 1, 0) != 0) return;
            cts.CancelAfter(500);
            MainThread.Post(() => Final?.Invoke(transcript));
        }
    }
}

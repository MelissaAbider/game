using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace EchoShift
{
    /// <summary>Marshals callbacks from background tasks (WebSockets, HTTP) onto Unity's main thread.</summary>
    public sealed class MainThread : MonoBehaviour
    {
        static readonly ConcurrentQueue<Action> Queue = new ConcurrentQueue<Action>();
        static MainThread instance;

        public static void Ensure()
        {
            if (instance) return;
            var go = new GameObject("[MainThread]");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<MainThread>();
        }

        public static void Post(Action action)
        {
            if (action != null) Queue.Enqueue(action);
        }

        void Update()
        {
            while (Queue.TryDequeue(out var action))
            {
                try { action(); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }
    }
}

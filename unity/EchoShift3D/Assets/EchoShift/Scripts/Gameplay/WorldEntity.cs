using System;
using System.Collections.Generic;
using UnityEngine;

namespace EchoShift
{
    /// <summary>Anything Gemini may refer to by voice ("the terminal", "the gap", "the plate").</summary>
    public sealed class WorldEntity : MonoBehaviour
    {
        public static readonly List<WorldEntity> All = new List<WorldEntity>();

        public string Id = "";
        public string Type = "object";
        public string Label = "";
        public string ColorName = "";
        public string[] Tags = Array.Empty<string>();
        public int Room;
        /// <summary>How close the subject walks when told to go to this entity.</summary>
        public float ApproachDistance = 1.4f;
        public Func<bool> IsActive = () => true;
        public Action Interact;

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() => All.Remove(this);

        public Vector3 Position => transform.position;

        public static WorldEntity Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var e in All) if (e && e.Id == id) return e;
            return null;
        }

        public static WorldEntity Register(GameObject go, string id, string type, string label, int room, params string[] tags)
        {
            var e = go.AddComponent<WorldEntity>();
            e.Id = id;
            e.Type = type;
            e.Label = label;
            e.Room = room;
            e.Tags = tags;
            return e;
        }
    }

    public enum BarrierKind { Jump, Crouch, Shout, Whisper, Hum, Echo, Bridge, Climb, VoiceKey }

    /// <summary>
    /// A challenge spanning the corridor at a given Z. The subject never walks blindly into one:
    /// the executor halts in front of it and ECHO explains which voice mechanic solves it.
    /// </summary>
    public sealed class Barrier
    {
        public static readonly List<Barrier> All = new List<Barrier>();

        public string Id;
        public float Z;
        /// <summary>Length along Z of the obstacle (a chasm is 7 m deep), used for lane steering.</summary>
        public float Depth;
        public BarrierKind Kind;
        public string Hint;
        /// <summary>True while the barrier stops the subject.</summary>
        public Func<bool> Blocking = () => true;
        /// <summary>Preferred X lane to cross (bridge / stairs position), or NaN.</summary>
        public Func<float> Lane = () => float.NaN;
        /// <summary>Some barriers only need to be announced once (e.g. the sentinel).</summary>
        public bool AnnounceOnly;
        public bool Acknowledged;

        public static Barrier Add(string id, float z, BarrierKind kind, string hint)
        {
            var b = new Barrier { Id = id, Z = z, Kind = kind, Hint = hint };
            All.Add(b);
            return b;
        }

        public bool Stops => AnnounceOnly ? !Acknowledged && Blocking() : Blocking();
    }
}

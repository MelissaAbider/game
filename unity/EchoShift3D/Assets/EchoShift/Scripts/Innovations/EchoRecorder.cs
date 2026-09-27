using System;
using System.Collections.Generic;
using UnityEngine;

namespace EchoShift
{
    public struct EchoSample
    {
        public float T;
        public Vector3 Position;
        public float Yaw;
        public float Speed;
        public float VerticalVelocity;
        public bool Crouching;
        public bool Grounded;
    }

    public struct EchoVoice
    {
        public float T;
        public AudioClip Clip;
    }

    /// <summary>
    /// Echo chamber: everything the subject does in the room is recorded, including the player's own
    /// voice. Saying "echo" rewinds the subject to the entrance while a ghost clone replays the
    /// previous run, speaking your past commands with your own voice. Clones can hold plates for you.
    /// </summary>
    public sealed class EchoRecorder : MonoBehaviour
    {
        public const int MaxClones = 2;
        public float ZStart, ZEnd;
        public Vector3 Entry;
        public readonly List<EchoClone> Clones = new List<EchoClone>();
        public int EchoCount { get; private set; }
        public bool Recording => recording;
        public float RecordedSeconds => recording ? Time.time - t0 : 0f;

        readonly List<EchoSample> samples = new List<EchoSample>();
        readonly List<EchoVoice> voices = new List<EchoVoice>();
        bool recording;
        float t0, nextSample;

        public bool InRoom(Vector3 p) => p.z > ZStart && p.z < ZEnd;

        void Update()
        {
            var s = GameDirector.I ? GameDirector.I.Subject : null;
            if (!s) return;
            var pos = s.transform.position;
            if (InRoom(pos))
            {
                if (!recording) Begin();
                if (Time.time >= nextSample)
                {
                    nextSample = Time.time + 0.05f;
                    samples.Add(new EchoSample
                    {
                        T = Time.time - t0,
                        Position = pos,
                        Yaw = s.Yaw,
                        Speed = s.PlanarSpeed,
                        VerticalVelocity = s.VerticalVelocity,
                        Crouching = s.Crouching,
                        Grounded = s.Grounded,
                    });
                }
            }
            else if (recording && pos.z > ZEnd + 2f)
            {
                recording = false;
                foreach (var c in Clones) if (c) c.Dissolve();
                Clones.Clear();
            }
        }

        void Begin()
        {
            recording = true;
            t0 = Time.time;
            nextSample = 0f;
            samples.Clear();
            voices.Clear();
        }

        public void RecordVoice(AudioClip clip)
        {
            if (recording && clip) voices.Add(new EchoVoice { T = Time.time - t0, Clip = clip });
        }

        public bool Trigger()
        {
            var director = GameDirector.I;
            var s = director.Subject;
            if (!InRoom(s.transform.position))
            {
                director.Say("Echoes only exist inside the Echo Chamber.", Hud.Warning);
                return false;
            }
            if (samples.Count < 12)
            {
                director.Say("Nothing to echo yet. Walk somewhere first, then say echo.", Hud.Warning);
                return false;
            }

            var clone = new GameObject("EchoClone").AddComponent<EchoClone>();
            clone.Init(new List<EchoSample>(samples), new List<EchoVoice>(voices), Clones.Count);
            Clones.Add(clone);
            if (Clones.Count > MaxClones)
            {
                Clones[0].Dissolve();
                Clones.RemoveAt(0);
            }
            foreach (var c in Clones) c.Restart();

            EchoCount++;
            director.RewindSubject(Entry);
            Begin();
            return true;
        }

        public bool AnyClone(Func<Vector3, bool> predicate)
        {
            foreach (var c in Clones) if (c && predicate(c.transform.position)) return true;
            return false;
        }

        public bool AnyCloneNear(Vector3 point, float radius)
        {
            foreach (var c in Clones)
                if (c && (c.transform.position - point).sqrMagnitude < radius * radius) return true;
            return false;
        }
    }
}

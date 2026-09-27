using System;
using System.Collections;
using System.Collections.Generic;

namespace EchoShift
{
    public enum ActionStatus { Pending, Running, Success, Failed, Cancelled }

    /// <summary>One validated step proposed by Gemini (the engine stays authoritative).</summary>
    public sealed class GameAction
    {
        public string Type = "";
        public string Target = "";
        public string Speed = "";
        public float Distance;
        public int DurationMs;
        public string BuildKind = "";
        public string Material = "";
        public ActionStatus Status = ActionStatus.Pending;

        public static GameAction From(Dictionary<string, object> d) => new GameAction
        {
            Type = d.Str("type"),
            Target = d.Str("target"),
            Speed = d.Str("speed"),
            Distance = (float)d.Num("distance"),
            DurationMs = (int)d.Num("duration_ms"),
            BuildKind = d.Str("build_kind"),
            Material = d.Str("material"),
        };

        public static GameAction Of(string type, string target = "") => new GameAction { Type = type, Target = target ?? "" };
    }

    public sealed class Intent
    {
        public readonly List<GameAction> Actions = new List<GameAction>();
        public float Confidence;
        public bool NeedsClarification;
        public string Clarification = "";
        public string Echo = "";
    }

    /// <summary>Sends the transcript + semantic world state to the backend Gemini interpreter.</summary>
    public static class IntentClient
    {
        public static IEnumerator Interpret(string transcript, string worldStateJson, Action<Intent> ok, Action<string> fail)
        {
            var body = "{\"transcript\":" + Json.Quote(transcript) + ",\"world_state\":" + worldStateJson + "}";
            string response = null;
            string error = null;
            yield return Backend.PostJson("/api/interpret", body, t => response = t, e => error = e, 40);
            if (response == null)
            {
                fail?.Invoke(error ?? "no response");
                yield break;
            }
            var d = Json.ParseObject(response);
            if (d == null)
            {
                fail?.Invoke("invalid response");
                yield break;
            }
            var intent = new Intent
            {
                Confidence = (float)d.Num("confidence"),
                NeedsClarification = d.Bool("requires_clarification"),
                Clarification = d.Str("clarification"),
                Echo = d.Str("echo"),
            };
            var actions = d.Arr("actions");
            if (actions != null)
                foreach (var a in actions)
                    if (a is Dictionary<string, object> obj) intent.Actions.Add(GameAction.From(obj));
            ok?.Invoke(intent);
        }
    }
}

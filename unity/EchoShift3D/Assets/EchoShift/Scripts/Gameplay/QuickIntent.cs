using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace EchoShift
{
    /// <summary>
    /// Instant on-device parser for the everyday orders (walk, jump, crouch, turn, use…). They run the moment
    /// the transcript arrives, without the Gemini round-trip. Anything with a word outside this small
    /// vocabulary (targets, building, sequences with objects…) returns false and goes to Gemini.
    /// </summary>
    public static class QuickIntent
    {
        static readonly HashSet<string> Filler = new HashSet<string>
        {
            "please", "now", "ok", "okay", "hey", "subject", "731", "echo", "the", "a", "an", "it", "that", "this", "just", "again",
            "go", "on", "up", "down", "over", "under", "past", "through", "quickly", "quick", "fast", "slowly", "slow", "quietly",
            "carefully", "gently", "little", "bit", "more", "you", "can", "could", "lets", "let's", "let", "us", "me", "then", "and",
            "keep", "going", "move", "walk", "run", "head", "continue", "advance", "proceed", "forward", "forwards", "ahead", "straight",
            "onward", "onwards", "jump", "hop", "leap", "crouch", "duck", "get", "stay", "low", "lower", "stand", "back", "backward",
            "backwards", "turn", "around", "left", "right", "laser", "lasers", "beam", "beams", "obstacle", "barrier", "break", "use",
            "activate", "interact", "press", "push", "touch", "button", "sneak", "step", "steps", "go-ahead", "yourself", "to", "way",
            "all", "long", "far", "big", "high",
            // Sonic glass words: the loudness does the breaking, the words just mean "go through".
            "shout", "scream", "yell", "smash", "shatter", "glass", "window", "wall", "louder", "loud", "now", "open", "hard",
            "come", "yeah", "ah", "aah", "aaah", "hey", "ha", "yes", "boom", "bang",
        };

        /// <summary>Words that mean "I'm shouting at the glass", whatever the rest of the sentence is.</summary>
        public static bool IsShout(string text) => Regex.IsMatch(text.ToLowerInvariant(), @"\b(shout|scream|yell|break|smash|shatter|glass|louder|boom|bang|a{2,}h*)\b");

        static readonly Regex Splitter = new Regex(@"\s*(?:,|;|\band then\b|\bthen\b|\band\b)\s*");

        public static bool TryParse(string text, out List<GameAction> actions)
        {
            actions = new List<GameAction>();
            var clean = Regex.Replace(text.ToLowerInvariant(), @"[^a-z0-9' ,;]", " ").Trim();
            if (clean.Length == 0) return false;
            // "go to the terminal", "walk to the door": a destination needs the world model → Gemini.
            if (Regex.IsMatch(clean, @"\b(to|toward|towards)\s+(the\s+)?(?!left|right)\w")) return false;
            foreach (var word in clean.Split(new[] { ' ', ',', ';' }, System.StringSplitOptions.RemoveEmptyEntries))
                if (!Filler.Contains(word)) return false;

            foreach (var clause in Splitter.Split(clean))
            {
                var c = " " + clause.Trim() + " ";
                if (c.Trim().Length == 0) continue;
                var a = Parse(c);
                if (a == null) continue;
                actions.Add(a);
            }
            return actions.Count > 0 && actions.Count <= 4;
        }

        static bool Has(string c, string pattern) => Regex.IsMatch(c, @"\b(" + pattern + @")\b");

        static GameAction Parse(string c)
        {
            if (Has(c, "jump|hop|leap")) return GameAction.Of("JUMP_OVER");
            if (Has(c, "crouch|duck|sneak|get down|stay low|get low|lower") || (Has(c, "under") && !Has(c, "walk|run|move|go"))) return GameAction.Of("CROUCH");
            if (Has(c, "stand")) return GameAction.Of("STAND");
            if (Has(c, "use|activate|interact|press|push|touch")) return GameAction.Of("INTERACT");
            if (Has(c, "turn"))
            {
                if (Has(c, "around|back")) return GameAction.Of("TURN_AROUND");
                if (Has(c, "left")) return GameAction.Of("TURN_LEFT");
                if (Has(c, "right")) return GameAction.Of("TURN_RIGHT");
                return null;
            }
            if (Has(c, "back|backward|backwards")) return GameAction.Of("MOVE_BACK");
            if (Has(c, "left")) return GameAction.Of("MOVE_LEFT");
            if (Has(c, "right")) return GameAction.Of("MOVE_RIGHT");
            if (Has(c, "walk|run|move|go|head|keep|continue|advance|proceed|forward|forwards|ahead|straight|onward|onwards|through|step|break|shout|scream|yell|smash|shatter|open|boom|bang|a{2,}h*"))
            {
                var move = GameAction.Of("MOVE_FORWARD");
                if (Has(c, "run|fast|quick|quickly")) move.Speed = "run";
                return move;
            }
            return null;
        }
    }
}

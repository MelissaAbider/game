using System.Linq;
using UnityEngine;

namespace EchoShift
{
    /// <summary>Final terminal: unlocked by reading its random voice key aloud.</summary>
    public sealed class CoreTerminal : MonoBehaviour
    {
        public static readonly string[] Keys = { "SILVER COMET", "ORBIT SEVEN", "BLUE FALCON", "NEON TIGER", "SOLAR RIVER", "ECHO NINE" };

        public string VoiceKey { get; private set; }
        public bool Activated { get; private set; }
        WorldLabel keyLabel, titleLabel;
        Material screen;
        Transform holo;
        float t;

        public static CoreTerminal Build(Transform parent, Vector3 position, int room)
        {
            var root = new GameObject("CoreTerminal").transform;
            root.SetParent(parent, false);
            root.position = position;
            var term = root.gameObject.AddComponent<CoreTerminal>();
            term.VoiceKey = Keys[Random.Range(0, Keys.Length)];

            var body = Mats.Solid(new Color(0.09f, 0.11f, 0.15f), 0.75f, 0.8f);
            Prims.Box("Base", root, new Vector3(0, 0.55f, 0), new Vector3(1.1f, 1.1f, 0.7f), body, true);
            term.screen = Mats.Emissive(Mats.Cyan, 2.5f, Color.black);
            if (Mats.HasTexture("tex_holo")) term.screen = Mats.Environment("tex_holo", Color.white, Vector2.one, 0.9f, 0f);
            var panel = Prims.Box("Screen", root, new Vector3(0, 1.35f, -0.1f), new Vector3(0.95f, 0.6f, 0.05f), term.screen, false, false);
            panel.transform.localRotation = Quaternion.Euler(-20f, 0f, 0f);

            term.holo = new GameObject("Hologram").transform;
            term.holo.SetParent(root, false);
            term.holo.localPosition = new Vector3(0, 2.7f, 0);
            Prims.Box("HoloFrame", term.holo, Vector3.zero, new Vector3(3.2f, 0.95f, 0.02f), Mats.Hologram(Mats.Cyan, 0.5f, 0.25f), false, false);
            term.titleLabel = WorldLabel.Create(term.holo, new Vector3(0, 0.24f, -0.05f), "VOICE KEY REQUIRED", 0.2f, Mats.Cyan, 2f);
            term.keyLabel = WorldLabel.Create(term.holo, new Vector3(0, -0.12f, -0.05f), $"\"{term.VoiceKey}\"", 0.42f, Color.white, 2.6f, true);

            var e = WorldEntity.Register(root.gameObject, "core_terminal", "terminal", "core terminal", room, "terminal", "computer", "console", "screen", "core");
            e.ColorName = "blue";
            e.ApproachDistance = 1.4f;
            e.IsActive = () => !term.Activated;
            e.Interact = () => GameDirector.I?.OnTerminalUsed(term);
            return term;
        }

        void Update()
        {
            t += Time.unscaledDeltaTime;
            holo.localPosition = new Vector3(0, 2.7f + Mathf.Sin(t * 1.6f) * 0.06f, 0);
            var cam = Camera.main;
            if (cam) holo.rotation = Quaternion.LookRotation(holo.position - cam.transform.position);
        }

        /// <summary>Tolerant match so STT variations ("silver comets", "orbit 7") still count.</summary>
        public bool Matches(string transcript)
        {
            var words = transcript.ToLowerInvariant().Replace(" 7", " seven").Replace(" 9", " nine")
                .Split(new[] { ' ', ',', '.', '!', '?', '"', '\'' }, System.StringSplitOptions.RemoveEmptyEntries);
            return VoiceKey.ToLowerInvariant().Split(' ').All(k => words.Any(w => w.StartsWith(k.Substring(0, 4)) || Distance(w, k) <= 1));
        }

        static int Distance(string a, string b)
        {
            var dp = new int[a.Length + 1, b.Length + 1];
            for (int i = 0; i <= a.Length; i++) dp[i, 0] = i;
            for (int j = 0; j <= b.Length; j++) dp[0, j] = j;
            for (int i = 1; i <= a.Length; i++)
            for (int j = 1; j <= b.Length; j++)
                dp[i, j] = Mathf.Min(Mathf.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1), dp[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            return dp[a.Length, b.Length];
        }

        public void Activate()
        {
            if (Activated) return;
            Activated = true;
            titleLabel.Text = "ACCESS GRANTED";
            titleLabel.SetColor(Mats.Green, 2.4f);
            keyLabel.Text = "PORTAL ONLINE";
            keyLabel.SetColor(Mats.Green, 2.6f);
            Mats.SetEmission(screen, Mats.Green, 3f);
        }

        public void Deny()
        {
            titleLabel.Text = "VOICE KEY REJECTED";
            titleLabel.SetColor(Mats.Red, 2.4f);
            Tween.Delay(this, 1.6f, () =>
            {
                if (Activated) return;
                titleLabel.Text = "VOICE KEY REQUIRED";
                titleLabel.SetColor(Mats.Cyan, 2f);
            });
        }
    }
}

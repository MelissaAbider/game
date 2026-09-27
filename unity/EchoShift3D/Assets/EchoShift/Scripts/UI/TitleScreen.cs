using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace EchoShift
{
    /// <summary>
    /// Landing page (same design as the web home): lab key-art backdrop, big logo and pitch on the left,
    /// Subject 731 standing on a lit platform on the right in two moods (saluting captain / peace-sign clown),
    /// a mood switch, a speech bubble and a how-to-play card.
    /// </summary>
    public sealed class TitleScreen : MonoBehaviour
    {
        public const string DefaultStatus = "ENTER  play   ·   H  how to play   ·   click the robot to change its mood";
        public event Action StartRequested;

        enum Mood { Captain, Chaos }

        static readonly Color Cyan = new Color(0.25f, 0.9f, 1f);
        static readonly Color Pink = new Color(1f, 0.42f, 0.82f);
        static readonly Color Soft = new Color(0.78f, 0.88f, 0.93f);
        static readonly Color Dim = new Color(0.5f, 0.63f, 0.7f);
        static readonly Color Ink = new Color(0.02f, 0.05f, 0.08f);
        const float FloorY = -290f;
        const float LogoY = 190f;

        static readonly string[] CaptainLines = { "READY FOR ORDERS.", "SAY THE WORD, OPERATOR.", "8 CHAMBERS. ONE WAY OUT." };
        static readonly string[] ChaosLines = { "SAY CHEESE!", "WHO PUT ME IN A LAB?!", "PEACE, OPERATOR!" };

        CanvasGroup group, howto;
        RectTransform root, background, heroRoot, bubble, thumb;
        RawImage[] rings = new RawImage[3];
        readonly CanvasGroup[] heroes = new CanvasGroup[2];
        readonly RawImage[] glows = new RawImage[2];
        Text codename, bubbleText, status, logoA, logoB;
        readonly List<(RectTransform rt, Image img, float speed, float phase)> motes = new List<(RectTransform, Image, float, float)>();
        Mood mood = Mood.Captain;
        bool starting;
        float glitch, lastInteraction, bubbleTimer, nextTease = 9f, nextLogoGlitch = 3f;
        Vector2 parallax;

        public static TitleScreen Create()
        {
            var t = new GameObject("TitleScreen").AddComponent<TitleScreen>();
            t.Build();
            return t;
        }

        // ── Construction ─────────────────────────────────────────────────────

        void Build()
        {
            var canvas = Ui.CreateCanvas("Title Canvas", 50);
            canvas.transform.SetParent(transform, false);
            root = Ui.Stretch("Root", canvas.transform);
            group = root.gameObject.AddComponent<CanvasGroup>();
            root.gameObject.AddComponent<Image>().color = new Color(0.01f, 0.02f, 0.04f, 1f);

            BuildBackdrop();
            BuildMotes();
            BuildLeftColumn();
            BuildStage();
            BuildCorners();
            BuildHowTo();

            lastInteraction = Time.unscaledTime;
            ShowMood(Mood.Captain, false);
            // Materialize: the hero rises out of the platform.
            heroRoot.localScale = new Vector3(1f, 0f, 1f);
            Tween.Run(this, 0.9f, k => heroRoot.localScale = new Vector3(1f, Tween.OutBack(k), 1f));
            Tween.Delay(this, 1.2f, () => Say(CaptainLines[0]));
        }

        void BuildBackdrop()
        {
            var bg = Resources.Load<Texture2D>("Images/home_bg");
            var img = Ui.RawImage(root, "Backdrop", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1920, 1080), bg, bg ? new Color(0.85f, 0.9f, 1f) : Color.clear);
            background = img.rectTransform;
            if (bg)
            {
                var fit = img.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = bg.width / (float)bg.height;
            }
            background.localScale = Vector3.one * 1.08f;
            // Readability: dark on the left (text) and at the bottom (ticker), open on the right (hero).
            Ui.Fill(root, "ShadeLeft", Ui.Ramp(true, 1.3f), new Color(0.01f, 0.02f, 0.05f, 0.93f));
            Ui.Fill(root, "ShadeBottom", Ui.Ramp(false, 2.2f), new Color(0.01f, 0.02f, 0.05f, 0.9f));
            var top = Ui.Fill(root, "ShadeTop", Ui.Ramp(false, 3f), new Color(0.01f, 0.02f, 0.05f, 0.75f));
            top.rectTransform.localScale = new Vector3(1f, -1f, 1f);
        }

        void BuildMotes()
        {
            var layer = Ui.Stretch("Motes", root);
            var rng = new System.Random(731);
            for (int i = 0; i < 46; i++)
            {
                float size = 2f + (float)rng.NextDouble() * 5f;
                var c = rng.NextDouble() < 0.7 ? Cyan : Pink;
                var img = Ui.Panel(layer, new Vector2(0.5f, 0.5f), new Vector2((float)rng.NextDouble() * 1920f - 960f, (float)rng.NextDouble() * 1080f - 540f), new Vector2(size, size), c, "Mote");
                motes.Add((img.rectTransform, img, 12f + (float)rng.NextDouble() * 40f, (float)rng.NextDouble() * 10f));
            }
        }


        void BuildLeftColumn()
        {
            var col = Ui.Rect("Left", root, new Vector2(0, 0.5f), new Vector2(120, 20), new Vector2(820, 760));
            Vector2 a = new Vector2(0, 0.5f);

            // Only the essentials: the name, its short description and the two buttons.
            logoA = Ui.Label(col, "ECHOSHIFT", Ui.Title, 132, new Color(1f, 0.25f, 0.7f, 0.55f), TextAnchor.MiddleLeft, a, new Vector2(-5, LogoY), new Vector2(900, 160));
            logoB = Ui.Label(col, "ECHOSHIFT", Ui.Title, 132, new Color(0.2f, 0.95f, 1f, 0.55f), TextAnchor.MiddleLeft, a, new Vector2(5, LogoY), new Vector2(900, 160));
            Ui.Glow(Ui.Label(col, "ECHOSHIFT", Ui.Title, 132, Color.white, TextAnchor.MiddleLeft, a, new Vector2(0, LogoY), new Vector2(900, 160)), new Color(0.2f, 0.8f, 1f, 0.6f), 6f);

            Ui.Label(col, "Guide <b>Subject 731</b> out of a living laboratory.\nNo buttons: <b>speak</b>, <b>shout</b>, <b>whisper</b> and <b>hum</b> — every chamber listens differently.",
                Ui.Body, 30, Soft, TextAnchor.UpperLeft, a, new Vector2(0, 30), new Vector2(760, 120));

            var play = Ui.Button(col, "PLAY", a, new Vector2(0, -120), new Vector2(340, 92), Cyan, RequestStart);
            var playLabel = play.GetComponentInChildren<Text>();
            playLabel.font = Ui.Title;
            playLabel.fontSize = 40;
            Ui.Border(play.GetComponent<Image>(), new Color(0.6f, 1f, 1f, 0.8f), 2f);

            var how = Ui.Button(col, "HOW TO PLAY", a, new Vector2(362, -120), new Vector2(290, 92), new Color(0.04f, 0.1f, 0.14f, 0.85f), () => ToggleHowTo(true));
            Ui.Border(how.GetComponent<Image>(), Cyan, 2f);
            how.GetComponentInChildren<Text>().color = Cyan;
        }

        void BuildStage()
        {
            var stage = Ui.Rect("Stage", root, new Vector2(0.5f, 0.5f), new Vector2(440, 50), new Vector2(760, 1000));
            var c = new Vector2(0.5f, 0.5f);

            Ui.RawImage(stage, "Beam", c, new Vector2(0, 60), new Vector2(440, 760), Ui.Ramp(false, 1.6f), new Color(Cyan.r, Cyan.g, Cyan.b, 0.13f));

            float floorY = FloorY;
            Ui.RawImage(stage, "FloorGlow", c, new Vector2(0, floorY), new Vector2(720, 190), Ui.Radial(), new Color(Cyan.r, Cyan.g, Cyan.b, 0.45f));
            rings[0] = Ui.RawImage(stage, "Ring0", c, new Vector2(0, floorY), new Vector2(560, 132), Ui.Radial(0.92f, 1.4f), new Color(Cyan.r, Cyan.g, Cyan.b, 0.95f));
            rings[1] = Ui.RawImage(stage, "Ring1", c, new Vector2(0, floorY), new Vector2(420, 98), Ui.Radial(0.9f, 1.6f), new Color(Pink.r, Pink.g, Pink.b, 0.7f));
            rings[2] = Ui.RawImage(stage, "Ring2", c, new Vector2(0, floorY), new Vector2(680, 160), Ui.Radial(0.95f, 1f), new Color(Cyan.r, Cyan.g, Cyan.b, 0.35f));

            heroRoot = Ui.Rect("Hero", stage, c, new Vector2(0, floorY + 8f), new Vector2(560, 720));
            heroRoot.pivot = new Vector2(0.5f, 0f);
            heroRoot.anchoredPosition = new Vector2(0, floorY + 8f);
            string[] files = { "Images/hero_serious", "Images/hero_fun" };
            for (int i = 0; i < 2; i++)
            {
                var tex = HeroArt.Cutout(files[i]);
                var holder = Ui.Rect(i == 0 ? "Captain" : "Chaos", heroRoot, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(560, 720));
                holder.pivot = new Vector2(0.5f, 0f);
                holder.anchoredPosition = Vector2.zero;
                heroes[i] = holder.gameObject.AddComponent<CanvasGroup>();
                if (!tex) continue;
                float height = 620f, width = height * tex.width / tex.height;
                // Reflection on the polished floor, then a neon rim glow behind the body, then the body.
                var refl = Ui.RawImage(holder, "Reflection", new Vector2(0.5f, 0f), new Vector2(0, -6), new Vector2(width, height * 0.3f), tex, new Color(0.6f, 0.9f, 1f, 0.16f));
                refl.rectTransform.pivot = new Vector2(0.5f, 1f);
                refl.uvRect = new Rect(0f, 0.55f, 1f, -0.55f);
                glows[i] = Ui.RawImage(holder, "Rim", new Vector2(0.5f, 0f), new Vector2(0, -8), new Vector2(width * 1.05f, height * 1.03f), tex, new Color(Cyan.r, Cyan.g, Cyan.b, 0.4f));
                glows[i].rectTransform.pivot = new Vector2(0.5f, 0f);
                var body = Ui.RawImage(holder, "Body", new Vector2(0.5f, 0f), Vector2.zero, new Vector2(width, height), tex);
                body.rectTransform.pivot = new Vector2(0.5f, 0f);
                body.raycastTarget = true;
                body.gameObject.AddComponent<Button>().onClick.AddListener(() => { lastInteraction = Time.unscaledTime; ShowMood(mood == Mood.Captain ? Mood.Chaos : Mood.Captain, true); });
            }
            if (!heroes[0].GetComponentInChildren<RawImage>())
                Ui.Label(heroRoot, "SUBJECT 731", Ui.Title, 40, Cyan, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0, 300), new Vector2(560, 80));

            // Speech bubble next to the helmet.
            var b = Ui.Panel(stage, c, new Vector2(240, 290), new Vector2(330, 64), new Color(0.03f, 0.08f, 0.12f, 0.95f), "Bubble");
            Ui.Border(b, Cyan, 2f);
            Ui.Panel(b.transform, new Vector2(0, 0), new Vector2(26, -9), new Vector2(18, 18), new Color(0.03f, 0.08f, 0.12f, 0.95f), "Tail").rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            bubbleText = Ui.Label(b.transform, "", Ui.Bold, 26, Color.white, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(320, 60));
            bubble = b.rectTransform;
            bubble.localScale = Vector3.zero;

            // Nameplate + mood switch under the platform.
            Ui.Label(stage, "SUBJECT 731", Ui.Title, 30, Color.white, TextAnchor.MiddleCenter, c, new Vector2(0, floorY - 96), new Vector2(600, 40));
            codename = Ui.Label(stage, "", Ui.Bold, 22, Cyan, TextAnchor.MiddleCenter, c, new Vector2(0, floorY - 130), new Vector2(600, 30));
            var sw = Ui.Panel(stage, c, new Vector2(0, floorY - 180), new Vector2(330, 54), new Color(0.03f, 0.07f, 0.1f, 0.9f), "Switch");
            Ui.Border(sw, new Color(Cyan.r, Cyan.g, Cyan.b, 0.6f), 1.5f);
            thumb = Ui.Panel(sw.transform, new Vector2(0.5f, 0.5f), new Vector2(-82, 0), new Vector2(158, 44), Cyan, "Thumb").rectTransform;
            MoodButton(sw.transform, "CAPTAIN", -82, Mood.Captain);
            MoodButton(sw.transform, "CHAOS", 82, Mood.Chaos);
        }

        void MoodButton(Transform parent, string label, float x, Mood m)
        {
            var img = Ui.Panel(parent, new Vector2(0.5f, 0.5f), new Vector2(x, 0), new Vector2(158, 44), Color.clear, "Mood " + label);
            img.raycastTarget = true;
            img.gameObject.AddComponent<Button>().onClick.AddListener(() => { lastInteraction = Time.unscaledTime; ShowMood(m, true); });
            Ui.Label(img.transform, label, Ui.Bold, 22, Color.white, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(158, 44), "MoodLabel");
        }

        void BuildCorners()
        {
            var c = new Color(Cyan.r, Cyan.g, Cyan.b, 0.7f);
            foreach (var (anchor, sx, sy) in new[] { (new Vector2(0, 1), 1, -1), (new Vector2(1, 1), -1, -1), (new Vector2(0, 0), 1, 1), (new Vector2(1, 0), -1, 1) })
            {
                var o = new Vector2(24 * sx, 24 * sy);
                Ui.Panel(root, anchor, o, new Vector2(46, 3), c, "Corner");
                Ui.Panel(root, anchor, o, new Vector2(3, 46), c, "Corner");
            }
        }

        void BuildHowTo()
        {
            var layer = Ui.Stretch("HowTo", root);
            howto = layer.gameObject.AddComponent<CanvasGroup>();
            var shade = layer.gameObject.AddComponent<Image>();
            shade.color = new Color(0.01f, 0.02f, 0.04f, 0.88f);
            layer.gameObject.AddComponent<Button>().onClick.AddListener(() => ToggleHowTo(false));

            var card = Ui.Panel(layer, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400, 640), new Color(0.03f, 0.07f, 0.11f, 0.97f), "Card");
            Ui.Border(card, Cyan, 2f);
            Ui.Panel(card.transform, new Vector2(0.5f, 1), Vector2.zero, new Vector2(1400, 5), Cyan, "Top");
            Ui.Label(card.transform, "HOW TO PLAY", Ui.Title, 54, Color.white, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, -60), new Vector2(1200, 70));

            var steps = new[]
            {
                ("1", "HOLD  V", "Hold V — or the round mic button — while you talk. Release to send.", Cyan),
                ("2", "GIVE AN ORDER", "Plain English works: \"walk forward\", \"jump over the laser\", \"crouch\".", Pink),
                ("3", "FOLLOW THE BANNER", "The banner at the top always shows exactly what to say next. Follow the arrows on the floor.", Mats.Green),
            };
            for (int i = 0; i < steps.Length; i++)
            {
                var (num, title, text, color) = steps[i];
                var s = Ui.Panel(card.transform, new Vector2(0.5f, 0.5f), new Vector2(-440 + i * 440, 10), new Vector2(410, 300), new Color(color.r * 0.08f, color.g * 0.08f, color.b * 0.08f, 1f), "Step " + num);
                Ui.Border(s, color, 1.5f);
                Ui.Label(s.transform, num, Ui.Title, 80, color, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, -60), new Vector2(400, 100));
                Ui.Label(s.transform, title, Ui.Bold, 32, Color.white, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, -135), new Vector2(400, 44));
                Ui.Label(s.transform, text, Ui.Body, 24, Soft, TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0, -190), new Vector2(370, 100));
            }
            string v = Ui.Hex(Mats.Violet), am = Ui.Hex(Mats.Amber), bl = Ui.Hex(Mats.Blue);
            Ui.Label(card.transform, $"<color=#{v}>SHOUT</color> breaks sonic glass   ·   <color=#{am}>WHISPER</color> near sentinels   ·   <color=#{bl}>HUM</color> opens the vault   ·   T to type instead",
                Ui.Bold, 24, Soft, TextAnchor.MiddleCenter, new Vector2(0.5f, 0), new Vector2(0, 90), new Vector2(1300, 36));
            Ui.Label(card.transform, "CLICK ANYWHERE TO CLOSE", Ui.Bold, 18, Dim, TextAnchor.MiddleCenter, new Vector2(0.5f, 0), new Vector2(0, 36), new Vector2(1300, 30));
            howto.alpha = 0f;
            howto.blocksRaycasts = false;
        }

        // ── Behaviour ────────────────────────────────────────────────────────

        void ShowMood(Mood next, bool animate)
        {
            bool changed = next != mood;
            mood = next;
            int on = (int)mood;
            var accent = mood == Mood.Captain ? Cyan : Pink;
            codename.text = mood == Mood.Captain ? "CODENAME · CAPTAIN ECHO" : "CODENAME · CAPTAIN CHEESE";
            codename.color = accent;
            thumb.GetComponent<Image>().color = accent;
            foreach (var l in thumb.parent.GetComponentsInChildren<Text>())
                l.color = (l.transform.parent.name == "Mood " + (mood == Mood.Captain ? "CAPTAIN" : "CHAOS")) ? Ink : Color.white;
            var fromX = thumb.anchoredPosition.x;
            var toX = mood == Mood.Captain ? -82f : 82f;
            if (animate) Tween.Run(this, 0.22f, k => thumb.anchoredPosition = new Vector2(Mathf.Lerp(fromX, toX, Tween.OutCubic(k)), 0));
            else thumb.anchoredPosition = new Vector2(toX, 0);
            for (int i = 0; i < 2; i++) heroes[i].alpha = i == on ? 1f : 0f;
            if (glows[on]) glows[on].color = new Color(accent.r, accent.g, accent.b, 0.4f);
            rings[0].color = new Color(accent.r, accent.g, accent.b, 0.95f);
            if (!animate || !changed) return;
            glitch = 0.35f;
            Sfx.Play(mood == Mood.Chaos ? "blip" : "accept", 0.5f);
            var lines = mood == Mood.Captain ? CaptainLines : ChaosLines;
            Say(lines[UnityEngine.Random.Range(0, lines.Length)]);
        }

        void Say(string line, float seconds = 2.6f)
        {
            bubbleText.text = line;
            bubble.sizeDelta = new Vector2(Mathf.Max(220f, bubbleText.preferredWidth + 48f), 64f);
            bubbleText.rectTransform.sizeDelta = bubble.sizeDelta;
            bubbleTimer = seconds;
        }

        void ToggleHowTo(bool open)
        {
            lastInteraction = Time.unscaledTime;
            howto.blocksRaycasts = open;
            float from = howto.alpha;
            Tween.Run(this, 0.2f, k => howto.alpha = Mathf.Lerp(from, open ? 1f : 0f, k));
            if (open) Sfx.Play("blip", 0.4f);
        }

        public void SetStatus(string text)
        {
            if (status) status.text = text;
        }

        void Update()
        {
            float t = Time.unscaledTime, dt = Time.unscaledDeltaTime;

            if (!starting)
            {
                if (Input.GetKeyDown(KeyCode.Escape) && howto.blocksRaycasts) ToggleHowTo(false);
                else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || (Input.GetKeyDown(KeyCode.Space) && !howto.blocksRaycasts)) RequestStart();
                if (Input.GetKeyDown(KeyCode.H)) ToggleHowTo(!howto.blocksRaycasts);
            }

            // Mouse parallax: backdrop drifts one way, hero stage the other.
            var mouse = new Vector2(Input.mousePosition.x / Mathf.Max(1, Screen.width) - 0.5f, Input.mousePosition.y / Mathf.Max(1, Screen.height) - 0.5f);
            parallax = Vector2.Lerp(parallax, mouse, dt * 3f);
            background.anchoredPosition = -parallax * 36f;

            // Hero idle: gentle hover + breathing, glitch jitter on mood switch.
            glitch = Mathf.Max(0f, glitch - dt);
            float jitter = glitch > 0f ? UnityEngine.Random.Range(-14f, 14f) * glitch / 0.35f : 0f;
            heroRoot.anchoredPosition = new Vector2(jitter + parallax.x * 14f, FloorY + 8f + Mathf.Sin(t * 1.6f) * 6f);
            if (heroRoot.localScale.y > 0.99f) heroRoot.localScale = new Vector3(1f, 1f + Mathf.Sin(t * 2.1f) * 0.006f, 1f);
            int on = (int)mood;
            if (glows[on])
            {
                var c = glows[on].color;
                c.a = 0.3f + 0.15f * Mathf.Sin(t * 3f) + (glitch > 0f ? 0.4f : 0f);
                glows[on].color = c;
            }
            for (int i = 0; i < rings.Length; i++)
            {
                float s = 1f + 0.04f * Mathf.Sin(t * (1.4f + i * 0.5f) + i);
                rings[i].rectTransform.localScale = new Vector3(s, s, 1f);
            }

            // Floating motes.
            foreach (var m in motes)
            {
                var p = m.rt.anchoredPosition;
                p.y += m.speed * dt;
                p.x += Mathf.Sin(t * 0.6f + m.phase) * 6f * dt;
                if (p.y > 560f) p.y = -560f;
                m.rt.anchoredPosition = p;
                var col = m.img.color;
                col.a = 0.25f + 0.35f * (0.5f + 0.5f * Mathf.Sin(t * 2f + m.phase));
                m.img.color = col;
            }

            // Logo chromatic glitch.
            nextLogoGlitch -= dt;
            float spread = nextLogoGlitch < 0.12f ? UnityEngine.Random.Range(6f, 16f) : 4f;
            if (nextLogoGlitch < 0f) nextLogoGlitch = UnityEngine.Random.Range(2.5f, 5f);
            logoA.rectTransform.anchoredPosition = new Vector2(-spread, LogoY);
            logoB.rectTransform.anchoredPosition = new Vector2(spread, LogoY);

            // Speech bubble pop.
            bubbleTimer -= dt;
            float target = bubbleTimer > 0f ? 1f : 0f;
            float sc = Mathf.MoveTowards(bubble.localScale.x, target, dt * 7f);
            bubble.localScale = Vector3.one * (target > 0f ? Tween.OutBack(sc) : sc);
            if (bubble.localScale.x > 1.15f) bubble.localScale = Vector3.one * 1.15f;

            // Idle tease: the captain glitches into his funny self for a second.
            if (!starting && t - lastInteraction > nextTease)
            {
                lastInteraction = t;
                nextTease = UnityEngine.Random.Range(10f, 15f);
                if (mood == Mood.Captain)
                {
                    ShowMood(Mood.Chaos, true);
                    Tween.Delay(this, 1.8f, () => { if (!starting) ShowMood(Mood.Captain, true); });
                }
                else Say(ChaosLines[UnityEngine.Random.Range(0, ChaosLines.Length)]);
            }
        }

        void RequestStart()
        {
            if (starting) return;
            starting = true;
            Sfx.Play("accept");
            if (mood == Mood.Captain) Say("MISSION ACCEPTED.", 1f);
            else Say("LET'S GOOO!", 1f);
            StartRequested?.Invoke();
            Tween.Run(this, 0.7f, k =>
            {
                group.alpha = 1f - k;
                root.localScale = Vector3.one * (1f + 0.06f * k);
            }, () => Destroy(gameObject));
        }
    }
}

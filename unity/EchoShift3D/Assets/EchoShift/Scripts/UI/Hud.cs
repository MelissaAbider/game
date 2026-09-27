using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EchoShift
{
    public enum VoiceState { Idle, Listening, Thinking, Acting }

    /// <summary>Screen-space interface, built entirely from code.</summary>
    public sealed class Hud : MonoBehaviour
    {
        public static readonly Color Info = new Color(0.62f, 0.93f, 1f);
        public static readonly Color Warning = new Color(1f, 0.84f, 0.48f);
        public static readonly Color Danger = new Color(1f, 0.56f, 0.64f);
        public static readonly Color Success = new Color(0.51f, 1f, 0.72f);
        public static readonly Color Build = new Color(1f, 0.72f, 0.45f);
        static readonly Color PanelColor = Ui.PanelBg;
        static readonly Color Ink = new Color(0.02f, 0.05f, 0.08f);
        List<Image> roomFrame, bannerFrame, voiceFrame;
        RawImage micRing;
        Text announceA, announceB;
        static readonly Color Dim = new Color(0.56f, 0.7f, 0.76f);

        Canvas canvas;
        RectTransform root;
        Text roomLetter, roomName, roomVerb, stepBadge, stepTitle, stepSay, stepHint, echoLine, transcript, prompt, announceTitle, announceSub;
        Image roomAccent, stepAccent, micOrb, flash, stepBadgeBg;
        RectTransform levelFill, whisperMark, shoutMark, routeMarker, chipsRow;
        Image levelFillImg;
        readonly List<Image> pips = new List<Image>();
        readonly List<Image> energyCells = new List<Image>();
        readonly List<Image> routeSegments = new List<Image>();
        readonly List<Image> waveBars = new List<Image>();
        readonly List<GameObject> chips = new List<GameObject>();
        GameObject energyGroup, echoGroup, timeGroup, tunerGroup, resonanceGroup, typedGroup;
        Text echoCount, timeLabel, tunerLabel, resonanceLabel, levelLabel;
        RectTransform timeFill, tunerNeedle, resonanceFill;
        Image timeFillImg, resonanceFillImg, tunerNeedleImg;
        readonly Image[] tunerBands = new Image[3];
        InputField typed;
        CanvasGroup announceGroup, voiceGroup;
        Image voiceDot, voiceCardBg;
        Text voiceState, voiceText;
        VoiceState vstate;
        float vtimer;
        float announceTimer, flashAlpha;
        Color flashColor;
        Sprite circle;

        public bool Typing => typedGroup && typedGroup.activeSelf;
        public event Action<string> TypedCommand;
        public event Action MicDown, MicUp;

        public static Hud Create()
        {
            var hud = new GameObject("HUD").AddComponent<Hud>();
            hud.Construct();
            return hud;
        }

        void Construct()
        {
            canvas = Ui.CreateCanvas("HUD Canvas", 10);
            canvas.transform.SetParent(transform, false);
            root = Ui.Stretch("Root", canvas.transform);
            circle = MakeCircle();

            // Same readability shading and neon screen corners as the home page.
            Ui.Fill(root, "ShadeBottom", Ui.Ramp(false, 2.4f), new Color(0.01f, 0.02f, 0.05f, 0.7f));
            Ui.Fill(root, "ShadeTop", Ui.Ramp(false, 3f), new Color(0.01f, 0.02f, 0.05f, 0.65f)).rectTransform.localScale = new Vector3(1f, -1f, 1f);
            var cornerColor = new Color(Mats.Cyan.r, Mats.Cyan.g, Mats.Cyan.b, 0.55f);
            foreach (var (anchor, sx, sy) in new[] { (new Vector2(0, 1), 1, -1), (new Vector2(1, 1), -1, -1), (new Vector2(0, 0), 1, 1), (new Vector2(1, 0), -1, 1) })
            {
                Ui.Panel(root, anchor, new Vector2(10 * sx, 10 * sy), new Vector2(40, 3), cornerColor, "ScreenCorner");
                Ui.Panel(root, anchor, new Vector2(10 * sx, 10 * sy), new Vector2(3, 40), cornerColor, "ScreenCorner");
            }

            // ── Room card (top-left)
            var card = Ui.Panel(root, new Vector2(0, 1), new Vector2(36, -30), new Vector2(420, 116), PanelColor, "RoomCard");
            roomFrame = Ui.Frame(card, Mats.Cyan);
            roomAccent = Ui.Panel(card.transform, new Vector2(0, 0.5f), Vector2.zero, new Vector2(6, 116), Mats.Cyan, "Accent");
            roomLetter = Ui.Label(card.transform, "A", Ui.Title, 70, Mats.Cyan, TextAnchor.MiddleCenter, new Vector2(0, 0.5f), new Vector2(20, 0), new Vector2(100, 110));
            roomName = Ui.Label(card.transform, "AIRLOCK", Ui.Bold, 34, Color.white, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(128, -10), new Vector2(285, 50));
            roomName.resizeTextForBestFit = true;
            roomName.resizeTextMinSize = 22;
            roomName.resizeTextMaxSize = 34;
            roomVerb = Ui.Label(card.transform, "SPEAK", Ui.Bold, 24, Mats.Cyan, TextAnchor.MiddleLeft, new Vector2(0, 0), new Vector2(128, 14), new Vector2(285, 40));

            // ── Objective banner (top-center)
            var banner = Ui.Panel(root, new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(980, 116), PanelColor, "Objective");
            bannerFrame = Ui.Frame(banner, Mats.Cyan);
            stepAccent = Ui.Panel(banner.transform, new Vector2(0.5f, 0), Vector2.zero, new Vector2(980, 3), Mats.Cyan, "Line");
            stepBadgeBg = Ui.Panel(banner.transform, new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(130, 44), Mats.Cyan, "Badge");
            stepBadge = Ui.Label(stepBadgeBg.transform, "STEP 1/10", Ui.Title, 18, Ink, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(130, 44));
            stepTitle = Ui.Label(banner.transform, "", Ui.Bold, 22, Mats.Cyan, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(166, -8), new Vector2(790, 30));
            stepSay = Ui.Label(banner.transform, "", Ui.Bold, 34, Color.white, TextAnchor.MiddleLeft, new Vector2(0, 0.5f), new Vector2(166, -2), new Vector2(790, 44));
            Ui.Glow(stepSay, new Color(0.2f, 0.8f, 1f, 0.35f), 3f);
            stepHint = Ui.Label(banner.transform, "", Ui.Body, 19, Dim, TextAnchor.MiddleLeft, new Vector2(0, 0), new Vector2(166, 6), new Vector2(790, 28));

            // Route strip under the banner.
            var route = Ui.Rect("Route", root, new Vector2(0.5f, 1), new Vector2(0, -156), new Vector2(980, 30));
            Ui.Label(route, "START", Ui.Bold, 16, Info, TextAnchor.MiddleRight, new Vector2(0, 0.5f), new Vector2(-72, 0), new Vector2(64, 24));
            Ui.Label(route, "EXIT", Ui.Bold, 16, Success, TextAnchor.MiddleLeft, new Vector2(1, 0.5f), new Vector2(72, 0), new Vector2(64, 24));
            routeMarker = Ui.Panel(route, new Vector2(0, 0.5f), new Vector2(0, 16), new Vector2(14, 14), Color.white, "Marker").rectTransform;

            // ── Status (top-right)
            var status = Ui.Panel(root, new Vector2(1, 1), new Vector2(-36, -30), new Vector2(360, 116), PanelColor, "Status");
            Ui.Frame(status, Danger);
            Ui.Label(status.transform, "INTEGRITY", Ui.Bold, 18, Danger, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(18, -8), new Vector2(200, 26));
            for (int i = 0; i < 3; i++)
                pips.Add(Ui.Panel(status.transform, new Vector2(0, 1), new Vector2(18 + i * 58, -42), new Vector2(50, 12), Mats.Red, "Pip"));
            energyGroup = Ui.Rect("Energy", status.transform, new Vector2(0, 0), new Vector2(18, 10), new Vector2(330, 50)).gameObject;
            Ui.Label(energyGroup.transform, "ENERGY", Ui.Bold, 16, Build, TextAnchor.MiddleLeft, new Vector2(0, 1), Vector2.zero, new Vector2(120, 22));
            for (int i = 0; i < WordForge.MaxEnergy; i++)
                energyCells.Add(Ui.Panel(energyGroup.transform, new Vector2(0, 0), new Vector2(i * 32, 4), new Vector2(26, 16), Mats.Orange, "Cell"));
            echoGroup = Ui.Rect("Echoes", status.transform, new Vector2(1, 1), new Vector2(-16, -8), new Vector2(150, 60)).gameObject;
            echoCount = Ui.Label(echoGroup.transform, "ECHOES 0", Ui.Bold, 20, new Color(1f, 0.6f, 0.9f), TextAnchor.MiddleRight, new Vector2(1, 1), Vector2.zero, new Vector2(150, 30));

            // ── Voice panel (bottom-left)
            var voice = Ui.Panel(root, new Vector2(0, 0), new Vector2(36, 36), new Vector2(520, 250), PanelColor, "Voice");
            Ui.Frame(voice, Mats.Cyan);
            micRing = Ui.RawImage(voice.transform, "MicRing", new Vector2(0, 1), new Vector2(4, -4), new Vector2(128, 128), Ui.Radial(0.9f, 1.6f), Mats.Cyan);
            var orbBg = Ui.Panel(voice.transform, new Vector2(0, 1), new Vector2(20, -20), new Vector2(96, 96), new Color(0.1f, 0.2f, 0.28f), "Orb");
            orbBg.sprite = circle;
            orbBg.raycastTarget = true;
            var trigger = orbBg.gameObject.AddComponent<EventTrigger>();
            AddTrigger(trigger, EventTriggerType.PointerDown, () => MicDown?.Invoke());
            AddTrigger(trigger, EventTriggerType.PointerUp, () => MicUp?.Invoke());
            micOrb = orbBg;
            Ui.Label(orbBg.transform, "V", Ui.Title, 40, Color.white, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 6), new Vector2(96, 60));
            Ui.Label(orbBg.transform, "HOLD", Ui.Bold, 15, Info, TextAnchor.MiddleCenter, new Vector2(0.5f, 0), new Vector2(0, 8), new Vector2(96, 20));

            Ui.Label(voice.transform, "VOICE LEVEL", Ui.Bold, 16, Dim, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(134, -18), new Vector2(200, 22));
            levelLabel = Ui.Label(voice.transform, "", Ui.Bold, 16, Info, TextAnchor.MiddleRight, new Vector2(0, 1), new Vector2(134, -18), new Vector2(360, 22));
            levelFill = Ui.Bar(voice.transform, new Vector2(0, 1), new Vector2(134, -46), new Vector2(360, 18), new Color(0.05f, 0.1f, 0.15f), Mats.Cyan, out levelFillImg);
            whisperMark = Ui.Panel(voice.transform, new Vector2(0, 1), new Vector2(134, -42), new Vector2(3, 26), Mats.Amber, "Whisper").rectTransform;
            shoutMark = Ui.Panel(voice.transform, new Vector2(0, 1), new Vector2(134, -42), new Vector2(3, 26), Mats.Violet, "Shout").rectTransform;
            Ui.Label(voice.transform, "WHISPER", Ui.Bold, 14, Mats.Amber, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(134, -72), new Vector2(120, 20));
            Ui.Label(voice.transform, "SHOUT", Ui.Bold, 14, Mats.Violet, TextAnchor.MiddleRight, new Vector2(0, 1), new Vector2(374, -72), new Vector2(120, 20));

            timeGroup = Ui.Rect("Time", voice.transform, new Vector2(0, 0), new Vector2(20, 96), new Vector2(480, 50)).gameObject;
            timeLabel = Ui.Label(timeGroup.transform, "TIME FLOW", Ui.Bold, 17, Mats.Amber, TextAnchor.MiddleLeft, new Vector2(0, 1), Vector2.zero, new Vector2(480, 22));
            timeFill = Ui.Bar(timeGroup.transform, new Vector2(0, 0), new Vector2(0, 4), new Vector2(474, 16), new Color(0.12f, 0.1f, 0.05f), Mats.Amber, out timeFillImg);

            tunerGroup = Ui.Rect("Tuner", voice.transform, new Vector2(0, 0), new Vector2(20, 96), new Vector2(480, 50)).gameObject;
            tunerLabel = Ui.Label(tunerGroup.transform, "RESONANCE TUNER", Ui.Bold, 17, Mats.Blue, TextAnchor.MiddleLeft, new Vector2(0, 1), Vector2.zero, new Vector2(480, 22));
            var tunerBar = Ui.Panel(tunerGroup.transform, new Vector2(0, 0), new Vector2(0, 4), new Vector2(474, 16), new Color(0.05f, 0.08f, 0.14f), "TunerBar");
            for (int i = 0; i < 3; i++)
            {
                float center = (i == 0 ? -5f : i == 1 ? 0f : 5f);
                tunerBands[i] = Ui.Panel(tunerBar.transform, new Vector2(0, 0.5f), new Vector2(SemitoneX(center - 1.8f, 474f), 0), new Vector2(474f * 3.6f / 16f, 16), ResonanceVault.NoteColors[i] * 0.55f, "Band");
            }
            tunerNeedle = Ui.Panel(tunerBar.transform, new Vector2(0, 0.5f), Vector2.zero, new Vector2(4, 26), Color.white, "Needle").rectTransform;
            tunerNeedleImg = tunerNeedle.GetComponent<Image>();

            resonanceGroup = Ui.Rect("Resonance", voice.transform, new Vector2(0, 0), new Vector2(20, 26), new Vector2(480, 50)).gameObject;
            resonanceLabel = Ui.Label(resonanceGroup.transform, "", Ui.Bold, 17, Mats.Violet, TextAnchor.MiddleLeft, new Vector2(0, 1), Vector2.zero, new Vector2(480, 22));
            resonanceFill = Ui.Bar(resonanceGroup.transform, new Vector2(0, 0), new Vector2(0, 4), new Vector2(474, 16), new Color(0.09f, 0.06f, 0.14f), Mats.Violet, out resonanceFillImg);

            // ── ECHO line + console (bottom-center)
            var echoPanel = Ui.Panel(root, new Vector2(0.5f, 0), new Vector2(250, 226), new Vector2(1120, 56), PanelColor, "EchoLine");
            Ui.Frame(echoPanel, Mats.Cyan, 14f, 2f);
            for (int i = 0; i < 12; i++)
                waveBars.Add(Ui.Panel(echoPanel.transform, new Vector2(0, 0.5f), new Vector2(18 + i * 9, 0), new Vector2(5, 6), Info, "Wave"));
            echoLine = Ui.Label(echoPanel.transform, "ECHO: …", Ui.Bold, 26, Color.white, TextAnchor.MiddleLeft, new Vector2(0, 0.5f), new Vector2(140, 0), new Vector2(960, 50));

            var console = Ui.Panel(root, new Vector2(0.5f, 0), new Vector2(250, 36), new Vector2(1120, 180), PanelColor, "Console");
            Ui.Frame(console, Ui.Pink);
            transcript = Ui.Label(console.transform, "", Ui.Bold, 28, new Color(0.65f, 0.93f, 1f), TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(24, -12), new Vector2(1070, 50));
            chipsRow = Ui.Rect("Chips", console.transform, new Vector2(0, 0.5f), new Vector2(24, -6), new Vector2(1070, 40));
            prompt = Ui.Label(console.transform, "HOLD V TO SPEAK · T TO TYPE · F TO FLOW TIME", Ui.Bold, 17, Dim, TextAnchor.MiddleRight, new Vector2(1, 0), new Vector2(-20, 12), new Vector2(900, 26));

            // Typed command box.
            var typedBg = Ui.Panel(root, new Vector2(0.5f, 0), new Vector2(250, 300), new Vector2(1000, 70), new Color(0.02f, 0.1f, 0.15f, 0.95f), "Typed");
            typedBg.raycastTarget = true;
            typed = typedBg.gameObject.AddComponent<InputField>();
            var typedText = Ui.Label(typedBg.transform, "", Ui.Bold, 30, Color.white, TextAnchor.MiddleLeft, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960, 60));
            typedText.supportRichText = false;
            var placeholder = Ui.Label(typedBg.transform, "Type a command (typed commands are silent)… Enter to send, Esc to cancel", Ui.Body, 24, Dim, TextAnchor.MiddleLeft, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960, 60));
            typed.textComponent = typedText;
            typed.placeholder = placeholder;
            typed.lineType = InputField.LineType.SingleLine;
            typed.onEndEdit.AddListener(OnTypedEnd);
            typedGroup = typedBg.gameObject;
            typedGroup.SetActive(false);

            // ── Voice status card: what the game is doing with your voice right now.
            voiceCardBg = Ui.Panel(root, new Vector2(0.5f, 1), new Vector2(0, -206), new Vector2(900, 76), new Color(0.02f, 0.05f, 0.09f, 0.94f), "VoiceCard");
            voiceFrame = Ui.Frame(voiceCardBg, Info, 22f, 3f);
            voiceGroup = voiceCardBg.gameObject.AddComponent<CanvasGroup>();
            voiceGroup.alpha = 0f;
            voiceDot = Ui.Panel(voiceCardBg.transform, new Vector2(0, 0.5f), new Vector2(26, 0), new Vector2(34, 34), Danger, "Dot");
            voiceDot.sprite = circle;
            voiceState = Ui.Label(voiceCardBg.transform, "", Ui.Title, 26, Danger, TextAnchor.MiddleLeft, new Vector2(0, 0.5f), new Vector2(78, 0), new Vector2(260, 60));
            voiceText = Ui.Label(voiceCardBg.transform, "", Ui.Bold, 32, Color.white, TextAnchor.MiddleLeft, new Vector2(0, 0.5f), new Vector2(340, 0), new Vector2(540, 70));
            voiceText.horizontalOverflow = HorizontalWrapMode.Wrap;
            voiceText.resizeTextForBestFit = true;
            voiceText.resizeTextMinSize = 18;
            voiceText.resizeTextMaxSize = 32;

            // ── Announcements & flashes
            var announce = Ui.Rect("Announce", root, new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(1400, 220));
            announceGroup = announce.gameObject.AddComponent<CanvasGroup>();
            announceGroup.alpha = 0f;
            // Chromatic split title, like the home-page logo.
            announceA = Ui.Label(announce, "", Ui.Title, 84, new Color(1f, 0.25f, 0.7f, 0.55f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(-5, 30), new Vector2(1400, 120));
            announceB = Ui.Label(announce, "", Ui.Title, 84, new Color(0.2f, 0.95f, 1f, 0.55f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(5, 30), new Vector2(1400, 120));
            announceTitle = Ui.Glow(Ui.Label(announce, "", Ui.Title, 84, Color.white, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(1400, 120)), Color.black, 4f);
            announceSub = Ui.Label(announce, "", Ui.Bold, 34, Info, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, -50), new Vector2(1400, 50));

            flash = Ui.Stretch("Flash", root).gameObject.AddComponent<Image>();
            flash.raycastTarget = false;
            flash.color = new Color(0, 0, 0, 0);
        }

        static void AddTrigger(EventTrigger trigger, EventTriggerType type, Action action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
        }

        static Sprite MakeCircle()
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - size / 2f, dy = y + 0.5f - size / 2f;
                float a = Mathf.Clamp01(size / 2f - Mathf.Sqrt(dx * dx + dy * dy));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        static float SemitoneX(float semitones, float width) => (semitones + 8f) / 16f * width;

        public void SetVisible(bool visible) => root.gameObject.SetActive(visible);

        public void SetRoom(Room r)
        {
            roomLetter.text = r.Letter;
            roomLetter.color = r.Color;
            roomName.text = r.Name.ToUpperInvariant();
            roomVerb.text = r.Verb;
            roomVerb.color = r.Color;
            roomAccent.color = r.Color;
            Ui.Tint(roomFrame, r.Color);
        }

        public void SetStep(int index, int total, string title, string say, string hint, Color color)
        {
            stepBadge.text = index > total ? "COMPLETE" : $"STEP {index}/{total}";
            stepBadgeBg.color = color;
            stepAccent.color = color;
            stepTitle.text = title.ToUpperInvariant();
            stepTitle.color = color;
            Ui.Tint(bannerFrame, color);
            stepSay.text = say;
            stepHint.text = hint;
        }

        public void SetEcho(string text, Color color)
        {
            echoLine.text = "ECHO: " + text;
            echoLine.color = color;
        }

        public void SetTranscript(string text, bool live)
        {
            transcript.text = string.IsNullOrEmpty(text) ? "" : (live ? "» " : "“") + text + (live ? "…" : "”");
            if (live && vstate == VoiceState.Listening && !string.IsNullOrEmpty(text)) voiceText.text = "“" + text + "…”";
        }

        /// <summary>LISTENING (red) while the mic is held, UNDERSTANDING (amber) while the order is parsed, GO (green) when it runs.</summary>
        public void SetVoiceState(VoiceState state, string text = null)
        {
            vstate = state;
            vtimer = state == VoiceState.Acting ? 1.8f : 0f;
            if (state == VoiceState.Idle) return;
            Color c;
            string label;
            switch (state)
            {
                case VoiceState.Listening: c = Danger; label = "LISTENING"; break;
                case VoiceState.Thinking: c = Warning; label = "THINKING"; break;
                default: c = Success; label = "GO"; break;
            }
            voiceState.text = label;
            voiceState.color = c;
            voiceDot.color = c;
            Ui.Tint(voiceFrame, c);
            voiceText.text = !string.IsNullOrEmpty(text) ? text : state == VoiceState.Listening ? "speak now — release V to send" : "";
        }

        public void SetPrompt(string text) => prompt.text = text;

        public void SetMic(bool listening)
        {
            micOrb.color = listening ? new Color(1f, 0.2f, 0.32f) : new Color(0.1f, 0.2f, 0.28f);
        }

        public void SetLevel(float level, float whisperMax, float shoutMin, bool suppressed)
        {
            float max = shoutMin * 1.45f;
            Ui.SetFill(levelFill, level / max);
            levelFillImg.color = level >= shoutMin ? Mats.Violet : level <= whisperMax ? Mats.Amber : Mats.Cyan;
            whisperMark.anchoredPosition = new Vector2(134 + 360f * Mathf.Clamp01(whisperMax / max), -42);
            shoutMark.anchoredPosition = new Vector2(134 + 360f * Mathf.Clamp01(shoutMin / max), -42);
            levelLabel.text = suppressed ? "ECHO SPEAKING" : level >= shoutMin ? "SHOUT" : level <= whisperMax ? "WHISPER" : "VOICE";
        }

        public void SetIntegrity(int value)
        {
            for (int i = 0; i < pips.Count; i++) pips[i].color = i < value ? Mats.Red : new Color(0.2f, 0.08f, 0.1f);
        }

        public void SetEnergy(int energy, bool visible)
        {
            energyGroup.SetActive(visible);
            for (int i = 0; i < energyCells.Count; i++) energyCells[i].color = i < energy ? Mats.Orange : new Color(0.18f, 0.12f, 0.08f);
        }

        public void SetEchoes(int count, bool visible, bool recording, float seconds)
        {
            echoGroup.SetActive(visible);
            echoCount.text = recording ? $"● REC {seconds:0.0}s\nECHOES {count}" : $"ECHOES {count}";
        }

        public void SetTimeFlow(bool visible, float flow)
        {
            timeGroup.SetActive(visible);
            if (!visible) return;
            Ui.SetFill(timeFill, flow);
            timeLabel.text = flow < 0.2f ? "TIME FROZEN · make a sound to move" : $"TIME FLOW {Mathf.RoundToInt(flow * 100)}% · silence freezes";
        }

        public void SetTuner(bool visible, ResonanceVault vault)
        {
            tunerGroup.SetActive(visible);
            if (!visible || vault == null) return;
            if (!vault.Calibrated)
            {
                tunerLabel.text = $"TUNING… hum any comfortable note ({Mathf.RoundToInt(vault.CalibrationProgress * 100)}%)";
                tunerNeedle.gameObject.SetActive(false);
                return;
            }
            var target = vault.Target;
            tunerLabel.text = target.HasValue ? $"HUM {target.Value.ToString().ToUpperInvariant()} · melody {vault.MelodyText} · {vault.Progress}/3" : "HARMONY ACCEPTED";
            bool has = !float.IsNaN(vault.CurrentSemitones);
            tunerNeedle.gameObject.SetActive(has);
            if (has)
            {
                tunerNeedle.anchoredPosition = new Vector2(SemitoneX(Mathf.Clamp(vault.CurrentSemitones, -8f, 8f), 474f), 0);
                var note = ResonanceVault.Classify(vault.CurrentSemitones);
                tunerNeedleImg.color = note.HasValue ? ResonanceVault.NoteColors[(int)note.Value] : Color.white;
            }
            for (int i = 0; i < 3; i++)
            {
                bool isTarget = target.HasValue && (int)target.Value == i;
                tunerBands[i].color = ResonanceVault.NoteColors[i] * (isTarget ? 1f : 0.35f);
            }
        }

        public void SetResonance(bool visible, string label, float value, Color color)
        {
            resonanceGroup.SetActive(visible);
            if (!visible) return;
            resonanceLabel.text = label;
            resonanceLabel.color = color;
            Ui.SetFill(resonanceFill, value);
            resonanceFillImg.color = color;
        }

        public void SetRoute(float progress, IList<Room> rooms, float endZ, HashSet<int> cleared)
        {
            if (routeSegments.Count == 0)
            {
                var route = routeMarker.parent;
                foreach (var r in rooms)
                {
                    float x = r.Z0 / endZ * 980f, w = (r.Z1 - r.Z0) / endZ * 980f;
                    var seg = Ui.Panel(route, new Vector2(0, 0.5f), new Vector2(x + 1, 0), new Vector2(w - 2, 10), r.Color, "Seg " + r.Letter);
                    routeSegments.Add(seg);
                    Ui.Label(seg.transform, r.Letter, Ui.Bold, 15, r.Color, TextAnchor.MiddleCenter, new Vector2(0.5f, 0), new Vector2(0, -22), new Vector2(40, 20));
                }
            }
            for (int i = 0; i < routeSegments.Count; i++)
            {
                var c = rooms[i].Color;
                c.a = cleared.Contains(i) ? 1f : 0.3f;
                routeSegments[i].color = c;
            }
            routeMarker.anchoredPosition = new Vector2(Mathf.Clamp01(progress) * 980f - 7f, 16);
        }

        public void SetActions(IReadOnlyList<GameAction> queue, Func<GameAction, string> describe)
        {
            foreach (var c in chips) Destroy(c);
            chips.Clear();
            float x = 0f;
            for (int i = 0; i < queue.Count && i < 7; i++)
            {
                var a = queue[i];
                // Outlined pills like the verb chips on the home page; the running step is filled.
                Color accent, fg;
                switch (a.Status)
                {
                    case ActionStatus.Running: accent = Mats.Cyan; fg = Ink; break;
                    case ActionStatus.Success: accent = Success; fg = Success; break;
                    case ActionStatus.Failed: accent = Danger; fg = Danger; break;
                    case ActionStatus.Cancelled: accent = new Color(0.35f, 0.42f, 0.46f); fg = accent; break;
                    default: accent = Info; fg = Info; break;
                }
                var bg = a.Status == ActionStatus.Running ? accent : new Color(accent.r * 0.12f, accent.g * 0.12f, accent.b * 0.12f, 0.9f);
                var label = $"{i + 1} {describe(a)}";
                var chip = Ui.Panel(chipsRow, new Vector2(0, 0.5f), new Vector2(x, 0), new Vector2(10, 36), bg, "Chip");
                var edge = Ui.Panel(chip.transform, new Vector2(0.5f, 0), Vector2.zero, new Vector2(10, 2), accent, "Edge");
                var t = Ui.Label(chip.transform, label, Ui.Bold, 20, fg, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400, 36));
                float w = t.preferredWidth + 26f;
                chip.rectTransform.sizeDelta = new Vector2(w, 36);
                edge.rectTransform.sizeDelta = new Vector2(w, 2);
                t.rectTransform.sizeDelta = new Vector2(w, 36);
                chips.Add(chip.gameObject);
                x += w + 10f;
            }
        }

        public void Announce(string title, string subtitle, Color color, float seconds = 2.6f)
        {
            announceTitle.text = announceA.text = announceB.text = title;
            announceTitle.color = color;
            announceSub.text = subtitle;
            announceTimer = seconds;
        }

        public void Flash(Color color, float alpha = 0.45f)
        {
            flashColor = color;
            flashAlpha = alpha;
        }

        public void OpenTyped()
        {
            typedGroup.SetActive(true);
            typed.text = "";
            EventSystem.current?.SetSelectedGameObject(typed.gameObject);
            typed.ActivateInputField();
        }

        void OnTypedEnd(string text)
        {
            typedGroup.SetActive(false);
            if (!Input.GetKey(KeyCode.Escape) && !string.IsNullOrWhiteSpace(text)) TypedCommand?.Invoke(text.Trim());
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            announceTimer -= dt;
            announceGroup.alpha = Mathf.MoveTowards(announceGroup.alpha, announceTimer > 0f ? 1f : 0f, dt * 3f);
            flashAlpha = Mathf.MoveTowards(flashAlpha, 0f, dt * 1.2f);
            flash.color = new Color(flashColor.r, flashColor.g, flashColor.b, flashAlpha);

            if (vstate == VoiceState.Acting && (vtimer -= dt) <= 0f) vstate = VoiceState.Idle;
            bool showVoice = vstate != VoiceState.Idle && !Typing;
            voiceGroup.alpha = Mathf.MoveTowards(voiceGroup.alpha, showVoice ? 1f : 0f, dt * (showVoice ? 10f : 3f));
            float pulse = vstate == VoiceState.Listening || vstate == VoiceState.Thinking ? 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 10f) : 1f;
            voiceDot.rectTransform.localScale = Vector3.one * pulse;
            micOrb.rectTransform.localScale = Vector3.one * (vstate == VoiceState.Listening ? 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 10f) : 1f);
            float t = Time.unscaledTime;
            var ringColor = vstate == VoiceState.Listening ? Danger : Mats.Cyan;
            micRing.color = new Color(ringColor.r, ringColor.g, ringColor.b, vstate == VoiceState.Listening ? 1f : 0.45f + 0.25f * Mathf.Sin(t * 2.5f));
            micRing.rectTransform.localScale = Vector3.one * (1f + 0.05f * Mathf.Sin(t * (vstate == VoiceState.Listening ? 12f : 2.5f)));
            // Occasional chromatic glitch on announcements.
            float spread = Mathf.Repeat(t, 3.2f) < 0.12f ? 12f : 5f;
            announceA.rectTransform.anchoredPosition = new Vector2(-spread, 30);
            announceB.rectTransform.anchoredPosition = new Vector2(spread, 30);

            float amp = EchoSpeaker.I ? EchoSpeaker.I.Amplitude : 0f;
            for (int i = 0; i < waveBars.Count; i++)
            {
                float h = 6f + amp * 40f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 18f + i * 1.3f));
                waveBars[i].rectTransform.sizeDelta = new Vector2(5, h);
            }
        }
    }
}

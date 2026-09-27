using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace EchoShift
{
    /// <summary>
    /// Orchestrates the whole experience: title, intro flythrough, voice pipeline (mic → STT → Gemini →
    /// executor), chamber objectives, hazards, damage, wayfinding and the final ranking.
    /// </summary>
    public sealed class GameDirector : MonoBehaviour
    {
        public static GameDirector I { get; private set; }

        public Subject Subject { get; private set; }
        public Facility Facility { get; private set; }
        public WordForge Forge { get; private set; }
        public EchoRecorder Echoes { get; private set; }
        public CommandExecutor Executor { get; private set; }
        public Hud Hud { get; private set; }
        public ThirdPersonCamera Cam { get; private set; }
        public bool InCinematic { get; private set; } = true;
        public bool Playing { get; private set; }

        const string DefaultPrompt = "HOLD THE MIC (V) AND SPEAK";

        static readonly string[] AllowedActions =
        {
            "MOVE_FORWARD", "MOVE_BACK", "MOVE_LEFT", "MOVE_RIGHT", "TURN_LEFT", "TURN_RIGHT", "TURN_AROUND", "MOVE_TO",
            "JUMP", "JUMP_OVER", "CROUCH", "STAND", "WAIT", "STOP", "INTERACT", "USE", "BUILD", "ECHO", "RECYCLE",
        };

        sealed class Step
        {
            public int Room;
            public string Title, Say, Hint, Intro;
            public Color Color;
            public Func<bool> Done;
            public Func<Vector3> Target;
        }

        readonly List<Step> steps = new List<Step>();
        readonly HashSet<int> clearedRooms = new HashSet<int>();
        readonly List<Transform> chevrons = new List<Transform>();
        Material chevronMat;
        Transform marker;
        WorldLabel markerLabel;
        int stepIndex;
        int integrity = 3;
        int commands, hits;
        float startTime, invulnerableUntil, lastWarnTime, titleAngle;
        string lastWarnId;
        bool talking, keyPending, chronoAnnounced, manualMove;
        SttSession stt;
        AudioClip lastUtterance;
        Action<byte[]> pcmHandler;
        Room currentRoom;
        TitleScreen title;
        VoiceInput voice;
        VoiceTime voiceTime;
        EchoSpeaker echo;
        string[] args;

        void Awake()
        {
            if (I && I != this) { Destroy(gameObject); return; }
            I = this;
            Barrier.All.Clear();
            Time.timeScale = 1f;
            MainThread.Ensure();
            Sfx.Init();
            Application.targetFrameRate = 120;
            args = Environment.GetCommandLineArgs();
        }

        void Start()
        {
            voice = gameObject.AddComponent<VoiceInput>();
            echo = gameObject.AddComponent<EchoSpeaker>();
            voiceTime = gameObject.AddComponent<VoiceTime>();
            Forge = gameObject.AddComponent<WordForge>();
            Echoes = gameObject.AddComponent<EchoRecorder>();

            var camera = Camera.main;
            if (!camera)
            {
                var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
                camera = camGo.GetComponent<Camera>();
            }
            Cam = camera.GetComponent<ThirdPersonCamera>();
            if (!Cam) Cam = camera.gameObject.AddComponent<ThirdPersonCamera>();
            Cam.Manual = true;
            PostFx.Setup(camera);

            Facility = Facility.Build();
            Forge.Init(Facility);
            var echoRoom = Facility.Rooms[5];
            Echoes.ZStart = echoRoom.Z0 + 0.3f;
            Echoes.ZEnd = 124f;
            Echoes.Entry = echoRoom.Checkpoint;
            voiceTime.InChamber = () => Playing && Subject && Facility.Rooms[3].Contains(Subject.transform.position.z);

            Subject = new GameObject("Subject 731").AddComponent<Subject>();
            Subject.Teleport(Facility.Spawn, 0f);
            Subject.Rig.gameObject.SetActive(false);
            Cam.Target = Subject.transform;
            if (SideView.Enabled)
            {
                SideView.Setup(Facility, Subject);
                Cam.EnableSide(-2f, Facility.EndZ + 2f);
            }

            Hud = Hud.Create();
            if (Application.isMobilePlatform || Input.touchSupported || HasArg("-touch")) TouchControls.Create();
            Hud.SetVisible(false);
            Hud.TypedCommand += text => HandleTranscript(text, true);
            Hud.MicDown += BeginTalk;
            Hud.MicUp += EndTalk;
            Executor = new CommandExecutor(Subject, this);
            Executor.Changed += () => Hud.SetActions(Executor.Queue, Describe);

            BuildSteps();
            BuildGuide();
            currentRoom = Facility.Rooms[0];
            Hud.SetRoom(currentRoom);
            Sfx.Loop("ambience", gameObject, 0.3f, false);
            StartCoroutine(LoadArt());

            if (HasArg("-autotest") && !HasArg("-title")) StartCoroutine(AutoTest());
            else
            {
                title = TitleScreen.Create();
                title.StartRequested += StartExperiment;
                if (HasArg("-title")) StartCoroutine(ShotAndQuit());
            }
        }

        bool HasArg(string name) => Array.IndexOf(args, name) >= 0;

        string ArgValue(string name, string fallback)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
        }

        IEnumerator LoadArt()
        {
            yield return null;
            Facility.RefreshReflections();
            yield return Backend.LoadGeneratedArt(this, null);
            yield return null;
            Facility.RefreshReflections();
            if (title) title.SetStatus(TitleScreen.DefaultStatus);
        }

        // ── Flow ────────────────────────────────────────────────────────────

        void StartExperiment()
        {
            title = null; // the title screen fades itself out; stop orbiting the camera now
            voice.StartMic();
            StartCoroutine(Intro());
        }

        IEnumerator Intro()
        {
            InCinematic = true;
            Cam.Manual = true;
            Hud.SetVisible(true);
            Hud.SetEcho("Scanning Sector 01…", Hud.Info);
            Hud.SetPrompt("SPACE / CLICK TO SKIP");
            echo.Say("Scanning sector one. Eight chambers between you and the exit. Each one listens differently.");

            var camT = Cam.transform;
            var from = new Vector3(0f, 7.5f, Facility.EndZ - 18f);
            var to = new Vector3(1.4f, 3f, Facility.Spawn.z - 7f);
            int announced = -1;
            float t = 0f, age = 0f;
            while (t < 1f)
            {
                age += Time.unscaledDeltaTime;
                t += Time.unscaledDeltaTime / 6f;
                float k = Tween.InOutSine(Mathf.Clamp01(t));
                Room room;
                if (Cam.Side)
                {
                    // Side view: a single sweep along the painted chambers, from the core back to the airlock.
                    float z = Mathf.Lerp(Facility.EndZ - 6f, Facility.Spawn.z + 2.5f, k);
                    var target = Cam.SidePosition(z, 0f);
                    target.y = Mathf.Lerp(camT.position.y, Cam.SidePosition(z, Facility.RoomAt(z).FloorY).y, age < 0.05f ? 1f : 0.1f);
                    camT.SetPositionAndRotation(target, ThirdPersonCamera.SideRotation);
                    room = Facility.RoomAt(z);
                }
                else
                {
                    var pos = Vector3.Lerp(from, to, k) + new Vector3(Mathf.Sin(k * 8f) * 2.4f, Mathf.Sin(k * 5f) * 0.5f, 0f);
                    pos.y = Mathf.Max(pos.y, Facility.RoomAt(pos.z).FloorY + 1.2f);
                    camT.position = pos;
                    camT.rotation = Quaternion.Slerp(camT.rotation, Quaternion.LookRotation(new Vector3(0f, Facility.RoomAt(pos.z + 10f).FloorY + 1.4f, pos.z + 12f) - pos), 0.2f);
                    room = Facility.RoomAt(pos.z + 9f);
                }
                if (room.Index != announced)
                {
                    announced = room.Index;
                    Hud.Announce($"{room.Letter} · {room.Name.ToUpperInvariant()}", room.Verb, room.Color, 1.3f);
                    Sfx.Play("blip", 0.5f);
                }
                if (age > 0.5f && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0))) break;
                yield return null;
            }
            yield return Materialize();
        }

        IEnumerator Materialize()
        {
            Cam.Manual = false;
            Cam.SnapBehind();
            var pos = Facility.Spawn;
            Sfx.Play("teleport", 0.8f);
            var column = Prims.Make("TeleportBeam", PrimitiveType.Cylinder, null, pos + Vector3.up * 2.3f, new Vector3(1.3f, 2.3f, 1.3f), Mats.Hologram(Mats.Cyan, 2.5f, 0.7f), false, false);
            Burst(pos + Vector3.up * 1f, Mats.Cyan, 60);
            yield return new WaitForSecondsRealtime(0.35f);
            Subject.Rig.gameObject.SetActive(true);
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / 0.8f;
                float k = Mathf.Clamp01(t);
                Subject.Rig.transform.localScale = new Vector3(1f, Tween.OutBack(k), 1f);
                column.transform.localScale = new Vector3(1.3f * (1f - k), 2.3f, 1.3f * (1f - k));
                yield return null;
            }
            Destroy(column);
            Subject.Rig.transform.localScale = Vector3.one;
            InCinematic = false;
            Playing = true;
            startTime = Time.time;
            ShowStep();
            Say("Subject online. Hold V and tell me to walk forward.", Hud.Info);
            Hud.SetPrompt(DefaultPrompt);
        }

        /// <summary>
        /// Headless check / demo capture:
        /// -autotest [-title | -room C [-say "walk forward|BREAK THROUGH|~walk forward"] [-voice]] [-victory]
        ///           [-shot a.png,b.png] [-shotDelay 3,5.5] [-quit]
        /// -voice plays each order as simulated speech (LISTENING card, live transcript, voice meter): UPPERCASE = shouted,
        /// a leading ~ = whispered. -shot/-shotDelay take comma-separated lists (delays in seconds from the start).
        /// </summary>
        IEnumerator AutoTest()
        {
            voice.StartMic();
            Hud.SetVisible(true);
            Cam.Manual = false;
            Subject.Rig.gameObject.SetActive(true);
            InCinematic = false;
            Playing = true;
            startTime = Time.time;
            var letter = ArgValue("-room", "A");
            var room = Facility.Rooms.Find(r => r.Letter == letter) ?? Facility.Rooms[0];
            Subject.Teleport(room.Checkpoint + (room.Index == 0 ? new Vector3(0, 0, 2.2f) : Vector3.zero), 0f);
            Cam.SnapBehind();
            int first = steps.FindIndex(s => s.Room == room.Index);
            stepIndex = first < 0 ? 0 : first;
            ShowStep();
            Say(steps[stepIndex].Intro, Hud.Info);
            var command = ArgValue("-say", null);
            if (command != null) StartCoroutine(RunCommands(command, HasArg("-voice")));
            if (HasArg("-victory")) Tween.Delay(this, 0.5f, Victory);
            yield return ShotAndQuit();
        }

        int demoShots;

        /// <summary>-demo prefix: event-driven screenshots (listening, go, in the air, result) for README demos.</summary>
        void Snap(string tag)
        {
            var prefix = ArgValue("-demo", null);
            if (prefix != null) ScreenCapture.CaptureScreenshot($"{prefix}_{++demoShots:00}_{tag}.png");
        }

        IEnumerator RunCommands(string command, bool spoken)
        {
            yield return new WaitForSecondsRealtime(1.5f);
            Snap("start");
            foreach (var part in command.Split('|'))
            {
                yield return new WaitForSecondsRealtime(1.2f);
                while (Executor.Busy || reacting) yield return null;
                if (spoken) yield return SimulateSpeech(part.Trim());
                else HandleTranscript(part.Trim(), true);
                yield return new WaitForSecondsRealtime(0.35f);
                Snap("go");
                bool air = false;
                while (Executor.Busy)
                {
                    if (!air && !Subject.Grounded && Subject.VerticalVelocity < 1.5f) { air = true; Snap("air"); }
                    yield return null;
                }
                yield return new WaitForSecondsRealtime(0.6f);
                Snap("result");
            }
            if (ArgValue("-demo", null) != null && HasArg("-quit"))
            {
                yield return new WaitForSecondsRealtime(1.5f);
                Application.Quit();
            }
        }

        /// <summary>Plays an order exactly like push-to-talk would, with a fake microphone.</summary>
        IEnumerator SimulateSpeech(string text)
        {
            bool whisper = text.StartsWith("~");
            text = text.TrimStart('~').Trim();
            bool shout = text.Any(char.IsLetter) && text == text.ToUpperInvariant();
            float level = shout ? voice.ShoutMin * 1.4f : whisper ? voice.WhisperMax * 0.55f : Mathf.Max(voice.SpeechBaseline * 0.8f, voice.WhisperMax * 1.6f);

            echo.Stop();
            Hud.SetMic(true);
            Hud.SetVoiceState(VoiceState.Listening);
            Hud.SetTranscript("", true);
            Subject.Rig.Listening = true;
            Sfx.Play("micOn", 0.45f);
            voice.Simulate(0f, true);
            string said = "";
            var words = text.Split(' ');
            for (int w = 0; w < words.Length; w++)
            {
                said = said.Length == 0 ? words[w] : said + " " + words[w];
                Hud.SetTranscript(said, true);
                for (float t = 0f; t < 0.38f; t += Mathf.Min(Time.unscaledDeltaTime, 0.05f))
                {
                    voice.Simulate(level * (0.75f + 0.25f * Mathf.Abs(Mathf.Sin(t * 20f))));
                    yield return null;
                }
                // Capture mid-sentence (live transcript still growing) for long orders, at the end for short ones.
                if (w == Mathf.Max(0, words.Length - (words.Length > 2 ? 2 : 1))) Snap("listening");
            }
            voice.Simulate(0f);
            Hud.SetMic(false);
            Subject.Rig.Listening = false;
            Sfx.Play("micOff", 0.45f);
            JudgeShout();
            Hud.SetVoiceState(VoiceState.Thinking, "…");
            yield return new WaitForSecondsRealtime(0.45f);
            HandleTranscript(text, false);
        }

        IEnumerator ShotAndQuit()
        {
            var shot = ArgValue("-shot", null);
            if (shot == null) yield break;
            var files = shot.Split(',');
            var delays = ArgValue("-shotDelay", "10").Split(',');
            float start = Time.realtimeSinceStartup;
            for (int i = 0; i < files.Length; i++)
            {
                float at = float.Parse(delays[Mathf.Min(i, delays.Length - 1)], System.Globalization.CultureInfo.InvariantCulture);
                while (Time.realtimeSinceStartup - start < at) yield return null;
                ScreenCapture.CaptureScreenshot(files[i]);
                yield return null;
            }
            yield return new WaitForSecondsRealtime(1.5f);
            if (HasArg("-quit")) Application.Quit();
        }

        void Update()
        {
            PostFx.Tick(Time.unscaledDeltaTime);
            if (title) OrbitTitleCamera();
            if (!Playing) return;

            HandleInput();
            Executor.Tick(Time.deltaTime);
            CheckHazards();
            UpdateRoom();
            CheckProgress();
            UpdateHud();
            UpdateGuide();
        }

        void OrbitTitleCamera()
        {
            titleAngle += Time.unscaledDeltaTime * 7f;
            float a = titleAngle * Mathf.Deg2Rad;
            var center = new Vector3(0f, 1.6f, 13f);
            Cam.transform.position = center + new Vector3(Mathf.Sin(a) * 5.5f, 2.6f, -Mathf.Cos(a) * 8f);
            Cam.transform.LookAt(center + Vector3.forward * 6f);
        }

        // ── Input & voice ───────────────────────────────────────────────────

        void HandleInput()
        {
            if (Hud.Typing || reacting) return;
            if (Input.GetKeyDown(KeyCode.V)) BeginTalk();
            if (Input.GetKeyUp(KeyCode.V)) EndTalk();
            if (Input.GetKeyDown(KeyCode.T)) Tween.Delay(this, 0.05f, Hud.OpenTyped);

            if (Executor.Busy) return;
            // Crouch gesture right in front of a floor laser triggers the same "what are you doing?" moment.
            bool crouchDown = Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.LeftControl) || TouchControls.ConsumeCrouchDown();
            if (crouchDown)
            {
                var ahead = NearestFloorLaserAhead();
                if (ahead != null && WrongMove(ahead, true)) return;
            }
            // Keyboard / touch movement is a fallback; the game is meant to be played by voice.
            var input = new Vector3(TouchControls.Move.x, 0f, TouchControls.Move.y);
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) input.z += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) input.z -= 1f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) input.x -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) input.x += 1f;
            if (input.sqrMagnitude > 0.01f)
            {
                var forward = Cam.transform.forward;
                forward.y = 0f;
                forward.Normalize();
                var right = Vector3.Cross(Vector3.up, forward);
                // Side view: left/right (and up/down) all walk along the corridor.
                var move = Cam.Side ? new Vector3(0f, 0f, Mathf.Clamp(input.x + input.z, -1f, 1f)) : forward * input.z + right * input.x;
                float speed = Subject.Crouching ? Subject.SneakSpeed : Input.GetKey(KeyCode.LeftShift) ? Subject.RunSpeed : Subject.WalkSpeed;
                Subject.Move(move, speed);
                manualMove = true;
            }
            else if (manualMove)
            {
                Subject.Stop();
                manualMove = false;
            }
            if (Input.GetKeyDown(KeyCode.Space) || TouchControls.ConsumeJump()) Subject.Jump();
            if (Input.GetKey(KeyCode.C) || Input.GetKey(KeyCode.LeftControl) || TouchControls.CrouchHeld) Subject.Crouch();
            else if (Subject.Crouching && !UnderOverhead()) Subject.Stand();
        }

        Barrier NearestFloorLaserAhead()
        {
            float z = Subject.transform.position.z;
            Barrier best = null;
            float bestAhead = 3.6f;
            foreach (var b in Barrier.All)
            {
                if (b.Kind != BarrierKind.Jump || !b.Stops) continue;
                float ahead = b.Z - z;
                if (ahead > -0.3f && ahead < bestAhead) { best = b; bestAhead = ahead; }
            }
            return best;
        }

        void BeginTalk()
        {
            if (!Playing || talking || Hud.Typing || reacting) return;
            if (!voice.Available && !voice.StartMic())
            {
                Say("I can't hear you. Allow the microphone, or use the on-screen controls.", Hud.Warning);
                return;
            }
            talking = true;
            echo.Stop(); // barge-in: never make the player wait for ECHO to finish (and never mute their shout)
            voice.BeginCapture();
            var session = new SttSession();
            stt = session;
            session.Partial += p => { if (session == stt) Hud.SetTranscript(p, true); };
            session.Final += text => OnFinal(session, text);
            session.Error += e => { Hud.SetEcho(e, Hud.Danger); Hud.SetVoiceState(VoiceState.Idle); };
            pcmHandler = session.Push;
            voice.PcmChunk += pcmHandler;
            session.Start();
            Hud.SetMic(true);
            Hud.SetVoiceState(VoiceState.Listening);
            Hud.SetTranscript("", true);
            Hud.SetPrompt("LISTENING… RELEASE TO SEND");
            Sfx.Play("micOn", 0.45f);
            Subject.Rig.Listening = true;
        }

        void EndTalk()
        {
            if (!talking) return;
            talking = false;
            voice.PcmChunk -= pcmHandler;
            lastUtterance = voice.EndCapture(!InLoudnessChallenge());
            JudgeShout();
            stt?.End();
            Hud.SetMic(false);
            Hud.SetVoiceState(VoiceState.Thinking, "…");
            Hud.SetPrompt("UNDERSTANDING…");
            Sfx.Play("micOff", 0.45f);
            Subject.Rig.Listening = false;
        }

        void OnFinal(SttSession session, string text)
        {
            if (session != stt) return;
            if (string.IsNullOrWhiteSpace(text))
            {
                Hud.SetEcho("I didn't hear anything. Hold V while you speak.", Hud.Warning);
                Hud.SetVoiceState(VoiceState.Idle);
                Hud.SetPrompt(DefaultPrompt);
                return;
            }
            Echoes.RecordVoice(lastUtterance);
            HandleTranscript(text, false);
        }

        float lastShout = -1f; // loudness of the last utterance near the glass, as a fraction of a SHOUT

        bool NearGlass() => Facility.Glass.Intact && Mathf.Abs(Subject.transform.position.z - Facility.Glass.transform.position.z) < 9f;

        /// <summary>
        /// The glass judges the whole push-to-talk utterance: if its loudest moment reached (almost) the SHOUT mark,
        /// it shatters — whatever the words were. No need to hold a shout, and no speech recognition involved.
        /// </summary>
        void JudgeShout()
        {
            lastShout = -1f;
            if (!NearGlass()) return;
            lastShout = voice.CapturePeak / voice.ShoutMin;
            if (lastShout >= 0.8f) Facility.Glass.Shatter();
        }

        bool InLoudnessChallenge()
        {
            float z = Subject.transform.position.z;
            return (Facility.Glass.Intact && Mathf.Abs(z - 46f) < 10f) || Facility.Sentinel.Listening;
        }

        void HandleTranscript(string text, bool typed)
        {
            if (!Playing) return;
            commands++;
            Hud.SetTranscript(text, false);
            var lower = text.Trim().ToLowerInvariant();

            if (Regex.IsMatch(lower, @"^(stop|wait|cancel|freeze|hold on|halt)[\s.!?]*$"))
            {
                Executor.CancelAll();
                Hud.SetVoiceState(VoiceState.Acting, "STOP");
                Say("Holding position.", Hud.Info);
                Hud.SetPrompt(DefaultPrompt);
                return;
            }
            if (currentRoom.Index == 7 && !Facility.Terminal.Activated && Facility.Terminal.Matches(text))
            {
                Hud.SetVoiceState(VoiceState.Acting, "VOICE KEY ACCEPTED");
                if (Vector3.Distance(Subject.transform.position, Facility.Terminal.transform.position) < 3.2f) ActivateTerminal();
                else
                {
                    keyPending = true;
                    Executor.Enqueue(new List<GameAction> { GameAction.Of("INTERACT", "core_terminal") });
                    Say("Voice key recognised. Walking to the terminal.", Hud.Success);
                }
                return;
            }
            if (Regex.IsMatch(lower, @"^\W*(echo|rewind|loop)\W*$"))
            {
                RunNow(new List<GameAction> { GameAction.Of("ECHO") });
                return;
            }
            if (Regex.IsMatch(lower, @"^\W*recycle\W*$"))
            {
                RunNow(new List<GameAction> { GameAction.Of("RECYCLE") });
                return;
            }
            if (Regex.IsMatch(lower, @"^[\s\W]*((h+m+|m+h*|la+|oo+h*|ah+|na+|da+|hum+)[\s\W]*)+$")) // humming only
            {
                Hud.SetVoiceState(VoiceState.Idle);
                Hud.SetPrompt(DefaultPrompt);
                return;
            }

            // Still facing intact glass: explain exactly what's missing instead of "I didn't understand".
            bool quickOk = QuickIntent.TryParse(text, out var quick);
            float toGlass = Facility.Glass.transform.position.z - Subject.transform.position.z;
            bool atGlass = Facility.Glass.Intact && toGlass > 0f && toGlass < 3.5f;
            if (atGlass && (QuickIntent.IsShout(text) || (quickOk && quick[0].Type is "MOVE_FORWARD" or "MOVE_RIGHT" or "JUMP_OVER")))
            {
                Hud.SetVoiceState(VoiceState.Idle);
                Hud.SetPrompt("HOLD V AND SHOUT");
                Sfx.Play("deny", 0.5f);
                if (typed) Say("Typed commands are silent. Hold V and SHOUT at the glass!", Mats.Violet);
                else if (lastShout >= 0f)
                    Say($"Louder! That was {Mathf.RoundToInt(Mathf.Clamp01(lastShout) * 100f)}% of a shout. Fill the meter to SHOUT!", Mats.Violet);
                else Say("Hold V and SHOUT at the glass!", Mats.Violet);
                Hud.Announce("LOUDER!", "fill the voice meter past SHOUT", Mats.Violet, 1.6f);
                return;
            }

            // Everyday orders run instantly on-device; only complex ones wait for Gemini.
            if (quickOk)
            {
                RunNow(quick);
                return;
            }

            Hud.SetVoiceState(VoiceState.Thinking, "“" + text + "”");
            Hud.SetPrompt("UNDERSTANDING…");
            StartCoroutine(IntentClient.Interpret(text, WorldStateJson(), OnIntent, e =>
            {
                Hud.SetVoiceState(VoiceState.Idle);
                Say("Signal lost. Say it again.", Hud.Danger);
                Hud.SetPrompt(DefaultPrompt);
            }));
        }

        void RunNow(List<GameAction> actions)
        {
            Sfx.Play("accept", 0.55f);
            Executor.Enqueue(actions);
            var parts = new List<string>();
            foreach (var a in actions) parts.Add(Describe(a));
            Hud.SetVoiceState(VoiceState.Acting, string.Join("  ›  ", parts));
            Hud.SetPrompt(DefaultPrompt);
        }

        void OnIntent(Intent intent)
        {
            Hud.SetPrompt(DefaultPrompt);
            if (intent.Actions.Count == 0 || intent.NeedsClarification || intent.Confidence < 0.7f)
            {
                Hud.SetVoiceState(VoiceState.Idle);
                if (intent.Actions.Count == 0 && !intent.NeedsClarification && intent.Confidence >= 0.95f) return;
                Sfx.Play("deny", 0.6f);
                Say(string.IsNullOrEmpty(intent.Clarification) ? "I didn't understand that." : intent.Clarification, Hud.Warning);
                return;
            }
            RunNow(intent.Actions);
            if (!string.IsNullOrEmpty(intent.Echo)) Say(intent.Echo, Hud.Info);
        }

        string WorldStateJson()
        {
            var s = Subject;
            var p = s.transform.position;
            var room = currentRoom;
            var w = new JsonWriter();
            w.BeginObject()
                .Prop("levelId", "facility_sector_01")
                .Prop("roomId", room.Id)
                .Prop("roomName", room.Name)
                .Key("player").BeginObject()
                .Prop("x", p.x).Prop("y", p.y).Prop("z", p.z).Prop("facing_yaw", s.Yaw)
                .Prop("grounded", s.Grounded).Prop("crouching", s.Crouching)
                .EndObject();

            w.Key("visibleObjects").BeginArray();
            foreach (var e in WorldEntity.All)
                if (e && Mathf.Abs(e.Room - room.Index) <= 1) WriteEntity(w, e);
            w.EndArray();
            w.Key("activeHazards").BeginArray();
            foreach (var e in WorldEntity.All)
                if (e && e.Room == room.Index && e.Type == "hazard" && e.IsActive()) WriteEntity(w, e);
            w.EndArray();
            w.Key("interactables").BeginArray();
            foreach (var e in WorldEntity.All)
                if (e && Mathf.Abs(e.Room - room.Index) <= 1 && e.Interact != null) WriteEntity(w, e);
            w.EndArray();

            var step = stepIndex < steps.Count ? steps[stepIndex] : null;
            w.Prop("objective", step != null ? $"{step.Title}: {step.Hint}" : "Escape.");
            w.Key("flags").BeginObject()
                .Prop("glass_intact", Facility.Glass.Intact)
                .Prop("sentinel_bypassed", Facility.Sentinel.Bypassed)
                .Prop("vault_open", Facility.Vault.Open)
                .Prop("terminal_activated", Facility.Terminal.Activated)
                .Prop("exit_open", Facility.Portal.Active)
                .Prop("energy", Forge.Energy)
                .Prop("echo_clones", Echoes.Clones.Count)
                .EndObject();
            w.Key("buildCosts").BeginObject();
            foreach (var kv in WordForge.Costs) w.Prop(kv.Key, kv.Value);
            w.EndObject();
            w.Key("allowedActions").BeginArray();
            foreach (var a in AllowedActions) w.Value(a);
            w.EndArray();
            w.EndObject();
            return w.ToString();
        }

        void WriteEntity(JsonWriter w, WorldEntity e)
        {
            var local = Subject.transform.InverseTransformPoint(e.Position);
            var flat = e.Position - Subject.transform.position;
            flat.y = 0f;
            float dist = flat.magnitude;
            string rel = Mathf.Abs(local.x) > Mathf.Abs(local.z) ? (local.x > 0 ? "right" : "left") : (local.z > 0 ? "ahead" : "behind");
            w.BeginObject()
                .Prop("id", e.Id).Prop("type", e.Type).Prop("label", e.Label).Prop("color", e.ColorName)
                .Key("tags").BeginArray();
            foreach (var t in e.Tags) w.Value(t);
            w.EndArray()
                .Prop("relative_position", rel)
                .Prop("distance_m", Mathf.Round(dist * 10f) / 10f)
                .Prop("distance", dist < 4f ? "near" : dist < 12f ? "mid" : "far")
                .Prop("active", e.IsActive())
                .EndObject();
        }

        string Describe(GameAction a)
        {
            string Label(string id)
            {
                var e = WorldEntity.Find(id);
                return (e ? e.Label : id ?? "").ToUpperInvariant();
            }
            switch (a.Type)
            {
                case "MOVE_FORWARD": return "FORWARD ↑";
                case "MOVE_BACK": return "BACK ↓";
                case "MOVE_LEFT": return "← LEFT";
                case "MOVE_RIGHT": return "RIGHT →";
                case "TURN_LEFT": return "TURN ←";
                case "TURN_RIGHT": return "TURN →";
                case "TURN_AROUND": return "TURN AROUND";
                case "MOVE_TO": return "GO TO " + Label(a.Target);
                case "JUMP": return "JUMP";
                case "JUMP_OVER": return "JUMP " + Label(a.Target);
                case "CROUCH": return string.IsNullOrEmpty(a.Target) ? "CROUCH" : "CROUCH UNDER";
                case "INTERACT":
                case "USE": return "USE " + Label(a.Target);
                case "BUILD": return "BUILD " + (a.BuildKind ?? "").ToUpperInvariant() + (string.IsNullOrEmpty(a.Material) ? "" : " · " + a.Material.ToUpperInvariant());
                default: return a.Type;
            }
        }

        // ── Services for gameplay objects ──────────────────────────────────

        public void Say(string text, Color color)
        {
            Hud.SetEcho(text, color);
            echo.Say(text);
        }

        public bool UnderOverhead()
        {
            if (!Subject) return false;
            float z = Subject.transform.position.z;
            foreach (var l in Facility.Lasers)
                if (l.Overhead && Mathf.Abs(z - l.Z) < Subject.Radius + 0.2f) return true;
            return false;
        }

        public WorldEntity NearestInteractable()
        {
            WorldEntity best = null;
            float bestDist = 8f;
            foreach (var e in WorldEntity.All)
            {
                if (!e || e.Interact == null) continue;
                float d = Vector3.Distance(e.Position, Subject.transform.position);
                if (d < bestDist) { best = e; bestDist = d; }
            }
            return best;
        }

        /// <summary>First barrier that stops a move from 'from' toward 'to', if the subject is right in front of it.</summary>
        public Barrier BarrierAhead(Vector3 from, Vector3 to)
        {
            float dz = to.z - from.z;
            if (Mathf.Abs(dz) < 0.05f) return null;
            float dir = Mathf.Sign(dz);
            Barrier best = null;
            float bestAhead = float.MaxValue;
            foreach (var b in Barrier.All)
            {
                if (!b.Stops) continue;
                float edge = dir > 0 ? b.Z : b.Z + b.Depth;
                float ahead = (edge - from.z) * dir;
                float stopDistance = b.Kind == BarrierKind.Bridge || b.Kind == BarrierKind.Climb ? 0.9f : 1.25f;
                if (ahead <= 0f || ahead > stopDistance) continue;
                if ((to.z - edge) * dir <= 0f) continue;
                if (ahead < bestAhead) { best = b; bestAhead = ahead; }
            }
            return best;
        }

        /// <summary>Keeps the subject on a forged bridge / stairs while crossing.</summary>
        public Vector3 Steer(Vector3 pos, Vector3 target)
        {
            float dz = target.z - pos.z;
            if (Mathf.Abs(dz) < 0.3f) return target;
            float dir = Mathf.Sign(dz);
            foreach (var b in Barrier.All)
            {
                float lane = b.Lane();
                if (float.IsNaN(lane)) continue;
                float start = dir > 0 ? b.Z : b.Z + b.Depth;
                float end = dir > 0 ? b.Z + b.Depth : b.Z;
                float ahead = (start - pos.z) * dir;
                float remaining = (end - pos.z) * dir;
                if (remaining < -0.5f || ahead > 7f) continue;
                if ((target.z - start) * dir <= 0f) continue;
                if (Mathf.Abs(pos.x - lane) > 0.25f && ahead > 0.4f) return new Vector3(lane, pos.y, start - dir * 0.6f);
                return new Vector3(lane, pos.y, end + dir * 1.2f);
            }
            return target;
        }

        public void Warn(Barrier b)
        {
            bool repeat = lastWarnId == b.Id && Time.unscaledTime - lastWarnTime < 6f;
            lastWarnId = b.Id;
            lastWarnTime = Time.unscaledTime;
            if (b.AnnounceOnly) b.Acknowledged = true;
            var color = ColorOf(b.Kind);
            Hud.SetEcho(b.Hint, color);
            if (repeat) return;
            Sfx.Play("deny", 0.5f);
            echo.Say(b.Hint);
            Hud.Announce(VerbOf(b.Kind), "", color, 1.6f);
        }

        static Color ColorOf(BarrierKind k)
        {
            switch (k)
            {
                case BarrierKind.Jump:
                case BarrierKind.Crouch: return Hud.Danger;
                case BarrierKind.Shout: return Mats.Violet;
                case BarrierKind.Whisper: return Mats.Amber;
                case BarrierKind.Hum: return Mats.Blue;
                case BarrierKind.Echo: return new Color(1f, 0.55f, 0.9f);
                case BarrierKind.Bridge:
                case BarrierKind.Climb: return Hud.Build;
                default: return Hud.Success;
            }
        }

        static string VerbOf(BarrierKind k)
        {
            switch (k)
            {
                case BarrierKind.Jump: return "JUMP";
                case BarrierKind.Crouch: return "CROUCH";
                case BarrierKind.Shout: return "SHOUT";
                case BarrierKind.Whisper: return "WHISPER";
                case BarrierKind.Hum: return "HUM";
                case BarrierKind.Echo: return "ECHO";
                case BarrierKind.Bridge:
                case BarrierKind.Climb: return "BUILD";
                default: return "READ";
            }
        }

        // ── "What are you doing?" reaction ─────────────────────────────────

        static readonly string[] WrongCrouchLines =
        {
            "You're ducking... at a FLOOR laser?",
            "The laser is down there. Why are YOU down there too?",
            "We talked about this. Legs go UP. Over the laser.",
        };

        static readonly string[] WrongJumpLines =
        {
            "Jumping into a chest-high beam. Bold strategy.",
            "My helmet and that beam are about to become friends.",
            "Down, operator. DOWN. This is not negotiable.",
        };

        bool reacting;
        int mistakeStreak;
        float lastMistakeTime = -100f;

        /// <summary>
        /// A clearly wrong order (crouching at a floor laser, jumping into a chest beam) doesn't just fail:
        /// Subject 731 stops, turns to the camera, plants his hands on his knees and asks what you're doing.
        /// </summary>
        public bool WrongMove(Barrier barrier, bool triedCrouch)
        {
            if (!Playing) return false;
            if (reacting) return true;
            float distance = Mathf.Abs(barrier.Z - Subject.transform.position.z);
            if (distance > 3.6f) return false;
            if (Time.unscaledTime - lastMistakeTime > 15f) mistakeStreak = 0;
            lastMistakeTime = Time.unscaledTime;
            var lines = triedCrouch ? WrongCrouchLines : WrongJumpLines;
            string line = lines[Mathf.Min(mistakeStreak, lines.Length - 1)];
            mistakeStreak++;
            StartCoroutine(ConfusedRoutine(line, triedCrouch ? "jump over the laser" : "crouch under the beam"));
            return true;
        }

        IEnumerator ConfusedRoutine(string line, string suggestion)
        {
            reacting = true;
            Executor.CancelAll(false);
            var toCamera = Cam.transform.position - Subject.transform.position;
            Subject.SetConfused(true, toCamera);
            Sfx.Play("deny", 0.7f);
            Cam.Shake(0.2f);
#if UNITY_ANDROID || UNITY_IOS
            Handheld.Vibrate();
#endif
            var marks = new List<WorldLabel>();
            for (int i = -1; i <= 1; i++)
            {
                var color = i == 0 ? Hud.Warning : Hud.Danger;
                marks.Add(WorldLabel.Create(Subject.transform, new Vector3(i * 0.32f, 2.25f + (i == 0 ? 0.12f : 0f), 0f), "?", i == 0 ? 0.55f : 0.38f, color, 2.6f, true, true));
            }
            var bubble = WorldLabel.Create(Subject.transform, new Vector3(0f, 2.95f, 0f), line, 0.2f, Color.white, 2f, false, true);
            Hud.Announce("?!", "Try again: \"" + suggestion + "\"", Hud.Warning, 3.2f);
            Say(line + " Try again.", Hud.Warning);
            Hud.SetPrompt("TRY AGAIN · SAY \"" + suggestion.ToUpperInvariant() + "\"");

            float t = 0f;
            while (t < 3.4f)
            {
                t += Time.unscaledDeltaTime;
                for (int i = 0; i < marks.Count; i++)
                {
                    float pop = Tween.OutBack(Mathf.Clamp01((t - i * 0.12f) / 0.35f));
                    float bob = Mathf.Sin(t * 6f + i * 1.7f) * 0.06f;
                    marks[i].transform.localScale = Vector3.one * pop;
                    marks[i].transform.localPosition = new Vector3((i - 1) * 0.32f, 2.25f + (i == 1 ? 0.12f : 0f) + bob, 0f);
                }
                bubble.transform.localScale = Vector3.one * Tween.OutBack(Mathf.Clamp01(t / 0.3f));
                yield return null;
            }

            foreach (var m in marks) if (m) Destroy(m.gameObject);
            if (bubble) Destroy(bubble.gameObject);
            Subject.SetConfused(false, Vector3.zero);
            Subject.FaceYaw(0f); // back to facing the route
            reacting = false;
            Hud.SetPrompt("HOLD THE MIC · SAY \"" + suggestion.ToUpperInvariant() + "\"");
        }

        public void Damage(string reason, Vector3? respawn, bool force = false)
        {
            if (!Playing) return;
            if (!force && Time.unscaledTime < invulnerableUntil) return;
            invulnerableUntil = Time.unscaledTime + 1.3f;
            if (Subject.Shielded)
            {
                Subject.SetShield(false);
                Forge.ConsumeShield();
                Hud.Flash(Mats.Green, 0.3f);
                Say("Shield absorbed the hit.", Hud.Success);
                return;
            }
            integrity--;
            hits++;
            Sfx.Play("hit", 0.9f);
            Hud.Flash(Mats.Red, 0.5f);
            PostFx.Pulse(Mats.Red, 1f);
            Cam.Shake(0.7f);
            Executor.CancelAll();
            string line = reason;
            if (integrity <= 0)
            {
                integrity = 3;
                line += " Integrity restored.";
            }
            Say(line, Hud.Danger);
            StartCoroutine(Respawn(respawn ?? currentRoom.Checkpoint));
        }

        IEnumerator Respawn(Vector3 target)
        {
            Burst(Subject.transform.position + Vector3.up, Mats.Red, 40);
            Subject.Rig.gameObject.SetActive(false);
            yield return new WaitForSecondsRealtime(0.4f);
            Subject.Teleport(target, 0f);
            Subject.Rig.gameObject.SetActive(true);
            Burst(target + Vector3.up, Mats.Cyan, 30);
            Sfx.Play("teleport", 0.35f);
        }

        public void RewindSubject(Vector3 entry)
        {
            Executor.CancelAll(false);
            var pink = new Color(1f, 0.5f, 0.9f);
            Sfx.Play("rewind", 0.8f);
            Hud.Flash(pink, 0.4f);
            PostFx.Pulse(pink, 1f);
            Burst(Subject.transform.position + Vector3.up, pink, 50);
            Subject.Teleport(entry, 0f);
            Cam.SnapBehind();
            Hud.Announce("ECHO", $"{Echoes.Clones.Count} clone{(Echoes.Clones.Count > 1 ? "s" : "")} replaying your past", pink, 2.2f);
            Say("Echo recorded. Your past self replays your last run, with your voice.", pink);
        }

        public void OnGlassShattered(Vector3 position)
        {
            Cam.Shake(1.2f);
            Hud.Flash(Mats.Violet, 0.35f);
            PostFx.Pulse(Mats.Violet, 0.9f);
            Hud.Announce("SHATTERED", "your voice broke the glass", Mats.Violet, 2f);
            Say("Resonance critical. Glass shattered!", Mats.Violet);
        }

        public void OnSentinelBypassed() => Say("Sentinel bypassed. You can speak normally again.", Hud.Success);

        public void OnChronoEntered()
        {
            if (chronoAnnounced) return;
            chronoAnnounced = true;
            Hud.Announce("TIME = VOICE", "silence freezes the world · sound moves it", Mats.Amber, 3.2f);
            Say("Chrono hall. Time only moves while you make sound. Go silent to freeze.", Mats.Amber);
        }

        public void OnVaultCalibrated(ResonanceVault vault)
        {
            Say($"Vault tuned to your voice. Now hum {vault.MelodyText}, one note at a time.", Mats.Blue);
            StartCoroutine(PlayMelody(vault));
        }

        IEnumerator PlayMelody(ResonanceVault vault)
        {
            yield return new WaitForSecondsRealtime(3.6f);
            voice.SuppressedUntil = Time.unscaledTime + 2.6f; // don't let the demo notes unlock the vault
            foreach (var n in vault.Melody)
            {
                Sfx.Note(vault.FrequencyOf(n), 0.55f, 0.6f);
                yield return new WaitForSecondsRealtime(0.7f);
            }
        }

        public void OnVaultOpened()
        {
            Hud.Announce("HARMONY", "the vault opens", Mats.Blue, 2f);
            Say("Harmony accepted. Vault open.", Mats.Blue);
        }

        public void OnTerminalUsed(CoreTerminal terminal)
        {
            if (terminal.Activated) { Say("The terminal is already unlocked.", Hud.Info); return; }
            if (keyPending) { ActivateTerminal(); return; }
            terminal.Deny();
            Sfx.Play("deny", 0.6f);
            Say($"Voice key required. Read the hologram aloud: \"{terminal.VoiceKey}\".", Hud.Info);
        }

        void ActivateTerminal()
        {
            keyPending = false;
            Facility.Terminal.Activate();
            Facility.Portal.Activate();
            Sfx.Play("unlock", 0.9f);
            Hud.Flash(Mats.Green, 0.3f);
            Hud.Announce("ACCESS GRANTED", "exit portal online", Mats.Green, 2.2f);
            Say("Voice key accepted. Exit portal online.", Hud.Success);
        }

        // ── Per-frame systems ───────────────────────────────────────────────

        void CheckHazards()
        {
            var p = Subject.transform.position;
            if (p.y < currentRoom.FloorY - 3f)
            {
                Damage("You fell into the chasm. Build a bridge first.", null, true);
                return;
            }
            if (Time.unscaledTime >= invulnerableUntil)
            {
                var body = Subject.HitBounds;
                foreach (var l in Facility.Lasers)
                    if (l.Hits(body)) { Damage(l.Overhead ? "Beam contact. Crouch under it next time." : "Laser contact. Jump over it next time.", null); return; }
                foreach (var sw in Facility.Sweepers)
                    if (sw.Hits(Subject)) { Damage("Sweeper contact. Go silent to freeze time when it comes.", null); return; }
            }
            if (Facility.Portal.Contains(p)) Victory();
        }

        void UpdateRoom()
        {
            var room = Facility.RoomAt(Subject.transform.position.z);
            if (room == currentRoom) return;
            if (room.Index > currentRoom.Index) clearedRooms.Add(currentRoom.Index);
            currentRoom = room;
            Hud.SetRoom(room);
            Hud.Announce($"{room.Letter} · {room.Name.ToUpperInvariant()}", room.Verb, room.Color, 2.2f);
            Sfx.Play("objective", 0.4f);
        }

        void CheckProgress()
        {
            while (stepIndex < steps.Count && steps[stepIndex].Done())
            {
                stepIndex++;
                if (stepIndex >= steps.Count) return;
                Sfx.Play("objective", 0.5f);
                ShowStep();
                Say(steps[stepIndex].Intro, Hud.Info);
            }
        }

        void ShowStep()
        {
            if (stepIndex >= steps.Count) return;
            var s = steps[stepIndex];
            Hud.SetStep(stepIndex + 1, steps.Count, s.Title, s.Say, s.Hint, s.Color);
            if (chevronMat) chevronMat.SetColor("_BaseColor", new Color(s.Color.r, s.Color.g, s.Color.b, 0.8f) * 2f);
            if (markerLabel) markerLabel.SetColor(s.Color, 2.2f);
        }

        void UpdateHud()
        {
            Hud.SetLevel(voice.EffectiveLevel, voice.WhisperMax, voice.ShoutMin, voice.Suppressed);
            Hud.SetIntegrity(integrity);
            Hud.SetEnergy(Forge.Energy, currentRoom.Index >= 6 || Forge.Spent > 0);
            Hud.SetEchoes(Echoes.Clones.Count, currentRoom.Index == 5, Echoes.Recording, Echoes.RecordedSeconds);
            Hud.SetTimeFlow(VoiceTime.I && VoiceTime.I.Active, VoiceTime.I ? VoiceTime.I.Flow : 1f);
            Hud.SetTuner(currentRoom.Index == 4 && !Facility.Vault.Open, Facility.Vault);

            float z = Subject.transform.position.z;
            if (Facility.Glass.Intact && Mathf.Abs(z - 46f) < 9f)
                Hud.SetResonance(true, "GLASS RESONANCE · SHOUT to shatter", Facility.Glass.Resonance, Mats.Violet);
            else if (Facility.Sentinel.Listening)
                Hud.SetResonance(true, "SENTINEL LISTENING · stay under WHISPER", Facility.Sentinel.Loudness, Color.Lerp(Mats.Amber, Mats.Red, Facility.Sentinel.Loudness));
            else
                Hud.SetResonance(false, "", 0f, Color.white);
            Hud.SetRoute(z / Facility.EndZ, Facility.Rooms, Facility.EndZ, clearedRooms);
        }

        // ── Wayfinding ──────────────────────────────────────────────────────

        void BuildGuide()
        {
            chevronMat = Mats.Hologram(Mats.Cyan, 2f, 0.8f);
            for (int i = 0; i < 12; i++)
            {
                var c = new GameObject("Chevron").transform;
                foreach (var side in new[] { -1f, 1f })
                {
                    var arm = Prims.Make("Arm", PrimitiveType.Cube, c, new Vector3(side * 0.16f, 0f, -0.1f), new Vector3(0.07f, 0.015f, 0.42f), chevronMat, false, false);
                    arm.transform.localRotation = Quaternion.Euler(0f, -side * 38f, 0f);
                }
                c.gameObject.SetActive(false);
                chevrons.Add(c);
            }
            marker = new GameObject("ObjectiveMarker").transform;
            var gem = Prims.Make("Gem", PrimitiveType.Cube, marker, Vector3.zero, Vector3.one * 0.32f, chevronMat, false, false);
            gem.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            markerLabel = WorldLabel.Create(marker, new Vector3(0f, 0.55f, 0f), "", 0.24f, Mats.Cyan, 2.2f, false, true);
        }

        void UpdateGuide()
        {
            if (stepIndex >= steps.Count || InCinematic)
            {
                foreach (var c in chevrons) c.gameObject.SetActive(false);
                marker.gameObject.SetActive(false);
                return;
            }
            var target = steps[stepIndex].Target();
            var from = Subject.transform.position;
            var flat = target - from;
            flat.y = 0f;
            float dist = flat.magnitude;
            var dir = dist > 0.01f ? flat / dist : Vector3.forward;
            float phase = (Time.unscaledTime * 1.6f) % 1.4f;
            for (int i = 0; i < chevrons.Count; i++)
            {
                float d = 1.4f + i * 1.4f + phase;
                bool on = !Cam.Side && d < Mathf.Min(dist - 0.6f, 16f); // floor chevrons are edge-on in the side view
                chevrons[i].gameObject.SetActive(on);
                if (!on) continue;
                var pos = from + dir * d;
                pos.y = from.y + 0.04f;
                chevrons[i].SetPositionAndRotation(pos, Quaternion.LookRotation(dir));
                float fade = Mathf.Sin(Mathf.Clamp01(d / Mathf.Min(dist, 16f)) * Mathf.PI);
                chevrons[i].localScale = Vector3.one * (0.6f + 0.4f * fade);
            }
            marker.gameObject.SetActive(true);
            marker.position = target + Vector3.up * (2.7f + Mathf.Sin(Time.unscaledTime * 2f) * 0.12f);
            marker.GetChild(0).Rotate(0f, 90f * Time.unscaledDeltaTime, 0f, Space.World);
            markerLabel.Text = $"{steps[stepIndex].Title.ToUpperInvariant()} · {Mathf.RoundToInt(dist)} m";
        }

        void BuildSteps()
        {
            var f = Facility;
            Vector3 At(float x, float z) => new Vector3(x, f.RoomAt(z).FloorY, z);
            steps.Add(new Step
            {
                Room = 0, Title = "Voice link", Color = Mats.Cyan,
                Say = "Hold V and say: \"walk forward\"",
                Hint = "Speak normally: ECHO learns your voice level. WASD is only a debug fallback.",
                Intro = "Hold V and tell me to walk forward.",
                Done = () => Subject.transform.position.z > 16.6f, Target = () => At(0, 17f),
            });
            steps.Add(new Step
            {
                Room = 1, Title = "Laser corridor", Color = Mats.Red,
                Say = "\"Jump over the laser\"",
                Hint = "The subject halts at every hazard and waits for your order.",
                Intro = "Security hall. Tell the subject when to jump.",
                Done = () => Subject.transform.position.z > 30f, Target = () => At(0, 30.5f),
            });
            steps.Add(new Step
            {
                Room = 1, Title = "Chest beam", Color = Mats.Red,
                Say = "\"Crouch under the beam\"",
                Hint = "Too high to jump: the subject has to stay low until it's past.",
                Intro = "A chest-height beam. Keep the subject low.",
                Done = () => Subject.transform.position.z > 40.6f, Target = () => At(0, 41f),
            });
            steps.Add(new Step
            {
                Room = 2, Title = "Sonic glass", Color = Mats.Violet,
                Say = "SHOUT: \"BREAK THROUGH!\"",
                Hint = "Only a loud voice resonates the glass: push the meter past SHOUT.",
                Intro = "Sonic glass. Only a shout can break it.",
                Done = () => !f.Glass.Intact, Target = () => At(0, 46f),
            });
            steps.Add(new Step
            {
                Room = 2, Title = "Acoustic sentinel", Color = Mats.Amber,
                Say = "Whisper: \"walk forward\"",
                Hint = "Inside its cone, stay under the WHISPER mark. Typed commands (T) are silent.",
                Intro = "An acoustic sentinel. From here on, whisper.",
                Done = () => f.Sentinel.Bypassed, Target = () => At(0, 61f),
            });
            steps.Add(new Step
            {
                Room = 3, Title = "Chrono hall", Color = Mats.Amber,
                Say = "\"Walk to the far door\", then HUM to make time flow",
                Hint = "Silence freezes everything. Sound moves it. Freeze when a sweeper comes.",
                Intro = "Next: the chrono hall. Your voice is its clock.",
                Done = () => Subject.transform.position.z > 90.6f, Target = () => At(0, 91f),
            });
            steps.Add(new Step
            {
                Room = 4, Title = "Resonance vault", Color = Mats.Blue,
                Say = "Hum any note to tune the vault, then hum its melody",
                Hint = "One note at a time with a pause between. The tuner shows your pitch.",
                Intro = "Resonance vault. Hum any comfortable note to tune it.",
                Done = () => f.Vault.Open, Target = () => At(0, 104f),
            });
            steps.Add(new Step
            {
                Room = 5, Title = "Echo chamber", Color = new Color(1f, 0.5f, 0.9f),
                Say = "Walk onto the plate, then say \"echo\"",
                Hint = "Your past self replays the run with your own voice, and holds the plate for you.",
                Intro = "Echo chamber. One body is not enough.",
                Done = () => Subject.transform.position.z > 124.8f,
                Target = () => f.EchoDoor.IsOpen ? At(0, 125f) : f.Plate.transform.position,
            });
            steps.Add(new Step
            {
                Room = 6, Title = "Architect forge", Color = Mats.Orange,
                Say = "\"Build a bridge of ice over the gap\", then \"build stairs\"",
                Hint = "Energy is limited (bridge 4, stairs 3). Describe any material you like.",
                Intro = "The forge. Speak objects into existence.",
                Done = () => Subject.transform.position.z > 150f && Subject.transform.position.y > 2f,
                Target = () => float.IsNaN(Forge.BridgeLane) ? At(0, f.ChasmZ0 - 0.5f) : float.IsNaN(Forge.ClimbLane) ? At(0, f.LedgeZ - 4f) : new Vector3(0, f.LedgeHeight, f.LedgeZ + 2f),
            });
            steps.Add(new Step
            {
                Room = 7, Title = "Core terminal", Color = Mats.Green,
                Say = "Read the voice key on the hologram aloud",
                Hint = "Walk to the terminal and speak clearly.",
                Intro = "The core. Read the voice key on the terminal.",
                Done = () => f.Terminal.Activated, Target = () => f.Terminal.transform.position,
            });
            steps.Add(new Step
            {
                Room = 7, Title = "Escape", Color = Mats.Green,
                Say = "\"Go through the portal\"",
                Hint = "The exit is open.",
                Intro = "Portal online. Get out.",
                Done = () => !Playing, Target = () => f.Portal.transform.position,
            });
        }

        void Victory()
        {
            if (!Playing) return;
            Playing = false;
            Executor.CancelAll();
            Subject.Stop();
            clearedRooms.Add(7);
            Time.timeScale = 1f;
            float seconds = Time.time - startTime;
            string rank = hits == 0 && commands <= 20 && seconds < 420f ? "S" : hits <= 2 && commands <= 30 ? "A" : hits <= 5 ? "B" : "C";
            var rankColor = rank == "S" ? new Color(1f, 0.82f, 0.4f) : rank == "A" ? Mats.Green : rank == "B" ? Mats.Cyan : new Color(0.8f, 0.86f, 0.9f);
            Sfx.Play("victory", 0.8f);
            Hud.SetStep(steps.Count + 1, steps.Count, "Sector cleared", "Rank " + rank, "", Mats.Green);
            echo.Say($"Sector cleared. Rank {rank}.");
            Burst(Subject.transform.position + Vector3.up, Mats.Green, 90);
            Hud.SetVisible(false);
            VictoryScreen.Show(rank, rankColor, seconds, commands, hits, Forge.Spent, Echoes.EchoCount, Bootstrap.Restart);
        }

        public static void Burst(Vector3 position, Color color, int count)
        {
            var go = new GameObject("Burst");
            go.transform.position = position;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
            main.startColor = color;
            main.useUnscaledTime = true;
            main.gravityModifier = 0.3f;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.4f;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = Mats.Particles(color * 2f);
            ps.Emit(count);
            Destroy(go, 2f);
        }
    }
}

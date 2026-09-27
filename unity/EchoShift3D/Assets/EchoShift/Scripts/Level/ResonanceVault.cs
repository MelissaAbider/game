using UnityEngine;

namespace EchoShift
{
    /// <summary>
    /// Musical lock. The vault first tunes itself to the note you hum naturally (so any voice works),
    /// then the door opens when you hum its melody: LOW / MID / HIGH are a fourth apart.
    /// </summary>
    public sealed class ResonanceVault : MonoBehaviour
    {
        public enum Note { Low, Mid, High }

        public Note[] Melody { get; private set; }
        public int Progress { get; private set; }
        public bool Calibrated { get; private set; }
        public bool Open { get; private set; }
        public float ReferenceHz { get; private set; }
        public float CurrentSemitones { get; private set; } = float.NaN;
        public float CalibrationProgress => Mathf.Clamp01(stableTime / 0.7f);

        float z0, z1;
        SlidingDoor door;
        readonly Material[] crystalMats = new Material[3];
        readonly Transform[] crystals = new Transform[3];
        float stableTime, stableSum, holdTime, t;
        int stableCount;
        Note? holding;
        bool awaitSilence;

        public static readonly Color[] NoteColors = { new Color(0.35f, 0.55f, 1f), new Color(0.3f, 0.95f, 1f), new Color(0.85f, 0.55f, 1f) };

        public static ResonanceVault Build(Transform parent, float roomZ0, float doorZ, float floorY, int room)
        {
            var root = new GameObject("ResonanceVault").transform;
            root.SetParent(parent, false);
            root.position = new Vector3(0, floorY, doorZ);
            var v = root.gameObject.AddComponent<ResonanceVault>();
            v.z0 = roomZ0;
            v.z1 = doorZ;
            v.Melody = Shuffle();

            v.door = SlidingDoor.Build(root, doorZ, floorY, 3f, 3.2f, Mats.Blue);
            v.door.Locked = () => !v.Open;
            var pedestal = Mats.Solid(new Color(0.08f, 0.1f, 0.13f), 0.8f, 0.8f);
            for (int i = 0; i < 3; i++)
            {
                var note = v.Melody[i];
                float x = -3.6f + i * 3.6f;
                Prims.Make("Pedestal", PrimitiveType.Cylinder, root, new Vector3(x, 0.5f, -2.2f), new Vector3(0.9f, 0.5f, 0.9f), pedestal, true);
                v.crystalMats[i] = Mats.Emissive(NoteColors[(int)note], 0.6f, new Color(0.05f, 0.07f, 0.1f));
                var crystal = Prims.Make("Crystal", PrimitiveType.Cube, root, new Vector3(x, 1.75f, -2.2f), new Vector3(0.45f, 0.45f, 0.45f), v.crystalMats[i], false);
                crystal.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
                crystal.transform.localScale = new Vector3(0.42f, 0.42f, 0.42f);
                v.crystals[i] = crystal.transform;
                WorldLabel.Create(root, new Vector3(x, 2.65f, -2.2f), $"{i + 1} · {note.ToString().ToUpper()}", 0.32f, NoteColors[(int)note], 2f, false, true);
            }
            WorldLabel.Create(root, new Vector3(0, 4.3f, -0.3f), "HUM THE MELODY", 0.5f, Mats.Blue, 2.2f, true);

            var e = WorldEntity.Register(root.gameObject, "resonance_door", "door", "resonance door", room, "door", "vault", "melody", "hum", "music");
            e.ColorName = "blue";
            e.IsActive = () => !v.Open;
            var barrier = Barrier.Add("resonance_door", doorZ, BarrierKind.Hum, "The vault door answers to music. HUM the melody on the crystals.");
            barrier.Blocking = () => !v.Open;
            return v;
        }

        static Note[] Shuffle()
        {
            var notes = new[] { Note.Low, Note.Mid, Note.High };
            for (int i = notes.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (notes[i], notes[j]) = (notes[j], notes[i]);
            }
            return notes;
        }

        public string MelodyText => $"{Melody[0]} · {Melody[1]} · {Melody[2]}".ToUpper();

        public float FrequencyOf(Note n) => ReferenceHz * Mathf.Pow(2f, n == Note.Low ? -5f / 12f : n == Note.High ? 5f / 12f : 0f);

        public static Note? Classify(float semitones)
        {
            if (semitones > -6.8f && semitones < -3.2f) return Note.Low;
            if (semitones > -1.8f && semitones < 1.8f) return Note.Mid;
            if (semitones > 3.2f && semitones < 6.8f) return Note.High;
            return null;
        }

        public bool SubjectInside
        {
            get
            {
                var s = GameDirector.I ? GameDirector.I.Subject : null;
                if (!s) return false;
                float z = s.transform.position.z;
                return z > z0 && z < z1 + 0.5f;
            }
        }

        void Update()
        {
            t += Time.unscaledDeltaTime;
            for (int i = 0; i < 3; i++) crystals[i].localRotation = Quaternion.Euler(45f, t * (i < Progress ? 90f : 25f), 45f);
            if (Open || !SubjectInside) return;
            var voice = VoiceInput.I;
            if (!voice) return;

            if (!voice.HasPitch)
            {
                holdTime = 0f;
                holding = null;
                awaitSilence = false;
                CurrentSemitones = float.NaN;
                if (!Calibrated) { stableTime = 0f; stableSum = 0f; stableCount = 0; }
                return;
            }

            float hz = voice.PitchHz;
            if (!Calibrated)
            {
                float mean = stableCount > 0 ? stableSum / stableCount : hz;
                if (stableCount > 0 && Mathf.Abs(PitchDetector.Semitones(hz, mean)) > 1.2f) { stableTime = 0f; stableSum = 0f; stableCount = 0; }
                stableSum += hz;
                stableCount++;
                stableTime += Time.unscaledDeltaTime;
                if (stableTime >= 0.7f)
                {
                    ReferenceHz = stableSum / stableCount;
                    Calibrated = true;
                    awaitSilence = true;
                    Sfx.Note(ReferenceHz, 0.5f);
                    GameDirector.I?.OnVaultCalibrated(this);
                }
                return;
            }

            CurrentSemitones = PitchDetector.Semitones(hz, ReferenceHz);
            if (awaitSilence) return;
            var note = Classify(CurrentSemitones);
            if (note != holding) { holding = note; holdTime = 0f; }
            if (!holding.HasValue) return;
            holdTime += Time.unscaledDeltaTime;
            if (holdTime < 0.55f) return;

            if (holding.Value == Melody[Progress])
            {
                Mats.SetEmission(crystalMats[Progress], NoteColors[(int)Melody[Progress]], 7f);
                Sfx.Note(FrequencyOf(Melody[Progress]), 0.55f);
                Progress++;
                awaitSilence = true;
                if (Progress >= Melody.Length)
                {
                    Open = true;
                    Sfx.Play("unlock", 0.9f);
                    GameDirector.I?.OnVaultOpened();
                }
            }
            else
            {
                Progress = 0;
                for (int i = 0; i < 3; i++) Mats.SetEmission(crystalMats[i], NoteColors[(int)Melody[i]], 0.6f);
                Sfx.Play("deny", 0.7f);
                awaitSilence = true;
                GameDirector.I?.Say($"Wrong note. Start again: {MelodyText}.", Hud.Warning);
            }
            holding = null;
            holdTime = 0f;
        }

        public Note? Target => Open || Progress >= Melody.Length ? (Note?)null : Melody[Progress];
    }
}

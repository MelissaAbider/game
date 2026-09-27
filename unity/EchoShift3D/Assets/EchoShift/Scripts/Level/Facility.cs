using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace EchoShift
{
    public sealed class Room
    {
        public int Index;
        public string Letter, Name, Verb, Id;
        public Color Color;
        public float Z0, Z1, FloorY, Height;
        public Vector3 Checkpoint => new Vector3(0f, FloorY + 0.05f, Z0 + 1.8f);
        public bool Contains(float z) => z >= Z0 && z < Z1;
        public float Center => (Z0 + Z1) * 0.5f;
    }

    /// <summary>
    /// Sector 01, built procedurally: eight chambers along +Z, each teaching one way to use your voice.
    /// A  Airlock · B Security Hall · C Acoustic Wing · D Chrono Hall · E Resonance Vault ·
    /// F Echo Chamber · G Architect Forge · H Core.
    /// </summary>
    public sealed class Facility : MonoBehaviour
    {
        public float HalfWidth => 7f;
        public readonly List<Room> Rooms = new List<Room>();
        public readonly List<LaserGate> Lasers = new List<LaserGate>();
        public readonly List<ChronoSweeper> Sweepers = new List<ChronoSweeper>();
        public readonly List<ReflectionProbe> Probes = new List<ReflectionProbe>();
        public SonicGlass Glass;
        public Sentinel Sentinel;
        public ResonanceVault Vault;
        public PressurePlate Plate;
        public SlidingDoor EchoDoor;
        public CoreTerminal Terminal;
        public ExitPortal Portal;
        public Transform StartPod;
        public readonly float ChasmZ0 = 136f, ChasmZ1 = 143f, LedgeZ = 148f, LedgeHeight = 2.4f;
        public Vector3 Spawn => new Vector3(0f, 0.05f, 4f);
        public float EndZ => Rooms[Rooms.Count - 1].Z1;

        Material wallMat, floorMat, ceilingMat, trimDark;

        public static Facility Build()
        {
            var f = new GameObject("Facility").AddComponent<Facility>();
            f.Construct();
            return f;
        }

        public Room RoomAt(float z)
        {
            foreach (var r in Rooms) if (r.Contains(z)) return r;
            return z < 0 ? Rooms[0] : Rooms[Rooms.Count - 1];
        }

        void Construct()
        {
            AddRoom("A", "Airlock", "SPEAK", Mats.Cyan, 0, 16, 0, 6);
            AddRoom("B", "Security Hall", "JUMP · CROUCH", Mats.Red, 16, 40, 0, 6);
            AddRoom("C", "Acoustic Wing", "SHOUT · WHISPER", Mats.Violet, 40, 64, 0, 6.5f);
            AddRoom("D", "Chrono Hall", "TIME = VOICE", Mats.Amber, 64, 90, 0, 7);
            AddRoom("E", "Resonance Vault", "HUM", Mats.Blue, 90, 108, 0, 7);
            AddRoom("F", "Echo Chamber", "ECHO", new Color(1f, 0.45f, 0.85f), 108, 130, 0, 6);
            AddRoom("G", "Architect Forge", "BUILD WITH WORDS", Mats.Orange, 130, 156, 0, 9);
            AddRoom("H", "Core", "READ THE KEY", Mats.Green, 156, 176, LedgeHeight, 6.5f);

            wallMat = Mats.Environment("tex_wall", new Color(0.55f, 0.6f, 0.68f), new Vector2(6f, 2f), 0.55f, 0.6f);
            floorMat = Mats.Environment("tex_floor", new Color(0.5f, 0.55f, 0.6f), new Vector2(3f, 3f), 0.85f, 0.7f);
            ceilingMat = Mats.Solid(new Color(0.05f, 0.06f, 0.08f), 0.3f, 0.5f);
            trimDark = Mats.Solid(new Color(0.07f, 0.08f, 0.1f), 0.7f, 0.9f);

            foreach (var r in Rooms) BuildShell(r);
            BuildEndWalls();
            BuildAirlock(Rooms[0]);
            BuildSecurity(Rooms[1]);
            BuildAcoustic(Rooms[2]);
            BuildChrono(Rooms[3]);
            BuildVault(Rooms[4]);
            BuildEcho(Rooms[5]);
            BuildForge(Rooms[6]);
            BuildCore(Rooms[7]);
            BuildLighting();
        }

        void AddRoom(string letter, string name, string verb, Color color, float z0, float z1, float floorY, float height)
        {
            Rooms.Add(new Room
            {
                Index = Rooms.Count, Letter = letter, Name = name, Verb = verb, Color = color,
                Z0 = z0, Z1 = z1, FloorY = floorY, Height = height, Id = "room_" + letter.ToLowerInvariant(),
            });
        }

        // ── Shell ───────────────────────────────────────────────────────────

        void BuildShell(Room r)
        {
            var root = new GameObject($"Room {r.Letter} {r.Name}").transform;
            root.SetParent(transform, false);
            float len = r.Z1 - r.Z0, mid = r.Center, top = r.FloorY + r.Height;
            var floorPer = Mats.Environment("tex_floor", new Color(0.5f, 0.55f, 0.6f), new Vector2(3.5f, len / 4f), 0.85f, 0.7f);
            var wallPer = Mats.Environment("tex_wall", new Color(0.55f, 0.6f, 0.68f), new Vector2(len / 4f, r.Height / 4f), 0.55f, 0.6f);

            if (r.Letter != "G")
                Prims.Box("Floor", root, new Vector3(0, r.FloorY - 0.2f - (r.FloorY > 0 ? r.FloorY * 0.5f : 0f), mid),
                    new Vector3(HalfWidth * 2f, 0.4f + r.FloorY, len), floorPer);
            float wallTop = r.Letter == "G" ? r.FloorY + r.Height : top;
            foreach (var side in new[] { -1f, 1f })
            {
                Prims.Box("Wall", root, new Vector3(side * (HalfWidth + 0.25f), wallTop * 0.5f, mid), new Vector3(0.5f, wallTop + 0.4f, len), wallPer);
                var trim = Mats.Emissive(r.Color, 2.6f);
                Prims.Box("TrimLow", root, new Vector3(side * (HalfWidth - 0.02f), r.FloorY + 0.08f, mid), new Vector3(0.04f, 0.05f, len), trim, false, false);
                Prims.Box("TrimHigh", root, new Vector3(side * (HalfWidth - 0.02f), top - 0.3f, mid), new Vector3(0.04f, 0.04f, len), trim, false, false);
                var panelGlow = Mats.Emissive(Color.Lerp(r.Color, Color.white, 0.35f), 1.8f);
                for (float z = r.Z0 + 2f; z < r.Z1 - 1f; z += 4f)
                {
                    Prims.Box("Rib", root, new Vector3(side * (HalfWidth - 0.15f), r.FloorY + r.Height * 0.5f, z), new Vector3(0.3f, r.Height, 0.35f), trimDark);
                    Prims.Box("RibLight", root, new Vector3(side * (HalfWidth - 0.31f), r.FloorY + 2.1f, z), new Vector3(0.02f, 1.6f, 0.08f), panelGlow, false, false);
                }
            }
            var ceiling = Prims.Box("Ceiling", root, new Vector3(0, top + 0.2f, mid), new Vector3(HalfWidth * 2f + 1f, 0.4f, len), ceilingMat, true, false);
            ceiling.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            // Ceiling light bars + real lights.
            var bar = Mats.Emissive(Color.Lerp(Color.white, r.Color, 0.25f), 2.4f);
            for (float z = r.Z0 + 3f; z < r.Z1 - 1f; z += 6f)
            {
                Prims.Box("LightBar", root, new Vector3(0, top - 0.05f, z), new Vector3(6f, 0.06f, 0.3f), bar, false, false);
                AddLight(root, new Vector3(0, top - 0.6f, z), Color.Lerp(Color.white, r.Color, 0.35f), 5.5f, 13f);
            }

            // Floor route line.
            var route = Mats.Emissive(r.Color, 0.9f);
            for (float z = r.Z0 + 0.5f; z < r.Z1 - 0.5f; z += 1.4f)
                Prims.Box("Route", root, new Vector3(0, r.FloorY + 0.012f, z), new Vector3(0.12f, 0.01f, 0.7f), route, false, false);

            // Chamber sign.
            var sign = new GameObject("Sign").transform;
            sign.SetParent(root, false);
            sign.position = new Vector3(0, top - 1.2f, r.Z0 + Mathf.Min(9f, len * 0.45f));
            Prims.Box("Plate", sign, new Vector3(0, 0, 0.05f), new Vector3(7.2f, 1.5f, 0.05f), Mats.Hologram(r.Color, 0.25f, 0.2f), false, false);
            WorldLabel.Create(sign, new Vector3(0, 0.28f, 0), $"{r.Letter} · {r.Name.ToUpperInvariant()}", 0.55f, r.Color, 2.4f, true);
            WorldLabel.Create(sign, new Vector3(0, -0.35f, 0), r.Verb, 0.36f, Color.white, 1.6f);

            // Voice-addressable waypoint for the whole chamber.
            var anchor = new GameObject("Anchor " + r.Letter);
            anchor.transform.SetParent(root, false);
            anchor.transform.position = new Vector3(0, r.FloorY, mid);
            var e = WorldEntity.Register(anchor, r.Id, "zone", r.Name.ToLowerInvariant(), r.Index, "zone", "room", "chamber", r.Name.ToLowerInvariant(), "zone " + r.Letter.ToLowerInvariant());
            e.ApproachDistance = 0.5f;

            var probe = new GameObject("Probe").AddComponent<ReflectionProbe>();
            probe.transform.SetParent(root, false);
            probe.transform.position = new Vector3(0, r.FloorY + 2.5f, mid);
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.size = new Vector3(HalfWidth * 2f, r.Height + 1f, len);
            probe.boxProjection = true;
            probe.resolution = 128;
            Probes.Add(probe);

            // Doorway into this room (the airlock's back wall is solid).
            if (r.Index > 0)
            {
                var prev = Rooms[r.Index - 1];
                float doorFloor = Mathf.Max(prev.FloorY, r.FloorY);
                float wallHeight = Mathf.Max(prev.FloorY + prev.Height, top);
                DoorWall(root, r.Z0, doorFloor, wallHeight, r.Color);
                var door = SlidingDoor.Build(root, r.Z0, doorFloor, 3f, 3.2f, r.Color);
                door.name = "Door " + r.Letter;
            }
        }

        void DoorWall(Transform root, float z, float doorFloor, float wallHeight, Color color, float doorWidth = 3f, float doorHeight = 3.2f)
        {
            float half = doorWidth * 0.5f;
            float sideWidth = HalfWidth - half;
            foreach (var side in new[] { -1f, 1f })
                Prims.Box("DoorWall", root, new Vector3(side * (half + sideWidth * 0.5f), wallHeight * 0.5f, z), new Vector3(sideWidth, wallHeight + 0.4f, 0.4f), wallMat);
            float lintelBottom = doorFloor + doorHeight;
            Prims.Box("Lintel", root, new Vector3(0, (lintelBottom + wallHeight) * 0.5f, z), new Vector3(doorWidth, wallHeight - lintelBottom + 0.4f, 0.4f), wallMat);
            if (doorFloor > 0.01f)
                Prims.Box("Sill", root, new Vector3(0, doorFloor * 0.5f, z), new Vector3(doorWidth, doorFloor, 0.4f), wallMat);
            var frame = Mats.Emissive(color, 3f);
            foreach (var side in new[] { -1f, 1f })
                Prims.Box("Frame", root, new Vector3(side * (half + 0.05f), doorFloor + doorHeight * 0.5f, z - 0.22f), new Vector3(0.08f, doorHeight, 0.06f), frame, false, false);
            Prims.Box("FrameTop", root, new Vector3(0, doorFloor + doorHeight + 0.05f, z - 0.22f), new Vector3(doorWidth + 0.2f, 0.08f, 0.06f), frame, false, false);
        }

        void BuildEndWalls()
        {
            var first = Rooms[0];
            Prims.Box("BackWall", transform, new Vector3(0, first.Height * 0.5f, first.Z0 - 0.2f), new Vector3(HalfWidth * 2f + 1f, first.Height + 0.4f, 0.4f), wallMat);
            var last = Rooms[Rooms.Count - 1];
            Prims.Box("EndWall", transform, new Vector3(0, (last.FloorY + last.Height) * 0.5f, last.Z1 + 0.2f), new Vector3(HalfWidth * 2f + 1f, last.FloorY + last.Height + 0.4f, 0.4f), wallMat);
        }

        static Light AddLight(Transform parent, Vector3 position, Color color, float intensity, float range, LightType type = LightType.Point)
        {
            var go = new GameObject(type + " Light");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var l = go.AddComponent<Light>();
            l.type = type;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            return l;
        }

        void BuildLighting()
        {
            var sun = new GameObject("Key Light").AddComponent<Light>();
            sun.transform.SetParent(transform, false);
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(62f, 25f, 0f);
            sun.color = new Color(0.72f, 0.82f, 1f);
            sun.intensity = 0.55f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.75f;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.1f, 0.13f, 0.2f);
            RenderSettings.ambientEquatorColor = new Color(0.06f, 0.08f, 0.12f);
            RenderSettings.ambientGroundColor = new Color(0.03f, 0.03f, 0.05f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.018f;
            RenderSettings.fogColor = new Color(0.03f, 0.05f, 0.08f);
            RenderSettings.skybox = null;
        }

        public void RefreshReflections()
        {
            foreach (var p in Probes) if (p) p.RenderProbe();
        }

        void Dust(Transform parent, Vector3 center, Vector3 size, Color color)
        {
            var go = new GameObject("Dust");
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 9f;
            main.startSpeed = 0.05f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
            main.startColor = color;
            main.maxParticles = 250;
            var emission = ps.emission;
            emission.rateOverTime = 18f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = size;
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.08f;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = Mats.Particles(color);
        }

        // ── Chambers ────────────────────────────────────────────────────────

        void BuildAirlock(Room r)
        {
            var root = transform.Find($"Room {r.Letter} {r.Name}");
            StartPod = new GameObject("StartPod").transform;
            StartPod.SetParent(root, false);
            StartPod.position = new Vector3(0, 0, Spawn.z);
            Prims.Make("PodBase", PrimitiveType.Cylinder, StartPod, new Vector3(0, 0.05f, 0), new Vector3(2.4f, 0.05f, 2.4f), trimDark, true);
            Prims.Make("PodRing", PrimitiveType.Cylinder, StartPod, new Vector3(0, 0.11f, 0), new Vector3(2f, 0.01f, 2f), Mats.Emissive(Mats.Cyan, 4f), false, false);
            Prims.Make("PodCap", PrimitiveType.Cylinder, StartPod, new Vector3(0, 4.6f, 0), new Vector3(2.4f, 0.12f, 2.4f), trimDark, false);
            Prims.Make("PodCapRing", PrimitiveType.Cylinder, StartPod, new Vector3(0, 4.46f, 0), new Vector3(2f, 0.01f, 2f), Mats.Emissive(Mats.Cyan, 4f), false, false);
            WorldLabel.Create(StartPod, new Vector3(0, 5.1f, 0), "START", 0.5f, Mats.Cyan, 2.4f, true);
            AddLight(StartPod, new Vector3(0, 3.5f, 0), Mats.Cyan, 6f, 8f);
            ScreenPanel(root, new Vector3(-6.9f, 2.4f, 9f), 90f);
            ScreenPanel(root, new Vector3(6.9f, 2.4f, 9f), -90f);
            Dust(root, new Vector3(0, 3f, r.Center), new Vector3(13f, 5f, 15f), new Color(0.6f, 0.9f, 1f, 0.5f));
        }

        void ScreenPanel(Transform root, Vector3 position, float yaw)
        {
            var panel = Prims.Box("Screen", root, position, new Vector3(3.2f, 1.8f, 0.05f), Mats.Environment("tex_holo", Color.white, Vector2.one, 0.9f, 0f), false, false);
            panel.transform.rotation = Quaternion.Euler(0, yaw, 0);
            var mat = panel.GetComponent<MeshRenderer>().sharedMaterial;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Mats.Cyan * 0.6f);
        }

        void BuildSecurity(Room r)
        {
            var root = transform.Find($"Room {r.Letter} {r.Name}");
            Lasers.Add(LaserGate.Build(root, "laser_1", "first laser", 23f, r.FloorY, false, r.Index, HalfWidth));
            Lasers.Add(LaserGate.Build(root, "laser_2", "second laser", 29f, r.FloorY, false, r.Index, HalfWidth));
            Lasers.Add(LaserGate.Build(root, "chest_beam", "chest beam", 35f, r.FloorY, true, r.Index, HalfWidth));
            Dust(root, new Vector3(0, 3f, r.Center), new Vector3(13f, 5f, 23f), new Color(1f, 0.6f, 0.7f, 0.4f));
        }

        void BuildAcoustic(Room r)
        {
            var root = transform.Find($"Room {r.Letter} {r.Name}");
            Glass = SonicGlass.Build(root, 46f, r.FloorY, HalfWidth, r.Height, r.Index);
            Sentinel = Sentinel.Build(root, 51f, 60f, r.FloorY, r.FloorY + r.Height, HalfWidth, r.Index);
            Dust(root, new Vector3(0, 3f, r.Center), new Vector3(13f, 5f, 23f), new Color(0.8f, 0.6f, 1f, 0.4f));
        }

        void BuildChrono(Room r)
        {
            var root = transform.Find($"Room {r.Letter} {r.Name}");
            Sweepers.Add(ChronoSweeper.Build(root, new Vector3(-3.4f, r.FloorY, 71f), 75f, 90f, r.Index, "sweeper_1"));
            Sweepers.Add(ChronoSweeper.Build(root, new Vector3(3.4f, r.FloorY, 81f), -95f, 250f, r.Index, "sweeper_2"));
            // Giant clock ring on the far wall.
            var clock = new GameObject("ClockRing").transform;
            clock.SetParent(root, false);
            clock.position = new Vector3(0, 4f, r.Z1 - 0.6f);
            var gold = Mats.Emissive(Mats.Amber, 3f);
            for (int i = 0; i < 12; i++)
            {
                float a = i / 12f * Mathf.PI * 2f;
                Prims.Box("Tick", clock, new Vector3(Mathf.Cos(a) * 2.2f, Mathf.Sin(a) * 2.2f, 0), new Vector3(0.12f, 0.45f, 0.05f), gold, false, false)
                    .transform.localRotation = Quaternion.Euler(0, 0, a * Mathf.Rad2Deg + 90f);
            }
            Dust(root, new Vector3(0, 3f, r.Center), new Vector3(13f, 5f, 25f), new Color(1f, 0.85f, 0.5f, 0.45f));
        }

        void BuildVault(Room r)
        {
            var root = transform.Find($"Room {r.Letter} {r.Name}");
            DoorWall(root, 104f, r.FloorY, r.FloorY + r.Height, r.Color);
            Vault = ResonanceVault.Build(root, r.Z0, 104f, r.FloorY, r.Index);
            Dust(root, new Vector3(0, 3f, r.Center), new Vector3(13f, 5f, 17f), new Color(0.5f, 0.7f, 1f, 0.45f));
        }

        void BuildEcho(Room r)
        {
            var root = transform.Find($"Room {r.Letter} {r.Name}");
            // In the side view there is no depth to walk into, so the plate sits on the walkway itself.
            Plate = PressurePlate.Build(root, new Vector3(SideView.Enabled ? 0f : -4.3f, r.FloorY, 115f), new Vector2(2f, 2f), r.Index);
            DoorWall(root, 124f, r.FloorY, r.FloorY + r.Height, r.Color);
            EchoDoor = SlidingDoor.Build(root, 124f, r.FloorY, 3f, 3.2f, r.Color);
            EchoDoor.OpenWhenUnlocked = true;
            var plate = Plate;
            EchoDoor.Locked = () => !plate.Pressed;
            var door = EchoDoor;
            var barrier = Barrier.Add("echo_door", 124f, BarrierKind.Echo,
                "This door only stays open while the plate is pressed. Stand on the plate, then say \"echo\".");
            barrier.Blocking = () => !door.IsOpen;
            var e = WorldEntity.Register(door.gameObject, "echo_door", "door", "echo door", r.Index, "door", "gate", "exit");
            e.IsActive = () => !door.IsOpen;
            WorldLabel.Create(root, new Vector3(0, 4.2f, 123.7f), "ONE BODY IS NOT ENOUGH", 0.36f, r.Color, 2f);
            Dust(root, new Vector3(0, 3f, r.Center), new Vector3(13f, 5f, 21f), new Color(1f, 0.6f, 0.9f, 0.45f));
        }

        void BuildForge(Room r)
        {
            var root = transform.Find($"Room {r.Letter} {r.Name}");
            var floor = Mats.Environment("tex_floor", new Color(0.5f, 0.55f, 0.6f), new Vector2(3.5f, 2f), 0.85f, 0.7f);
            Prims.Box("FloorNear", root, new Vector3(0, -0.2f, (r.Z0 + ChasmZ0) * 0.5f), new Vector3(HalfWidth * 2f, 0.4f, ChasmZ0 - r.Z0), floor);
            Prims.Box("FloorFar", root, new Vector3(0, -0.2f, (ChasmZ1 + LedgeZ) * 0.5f), new Vector3(HalfWidth * 2f, 0.4f, LedgeZ - ChasmZ1), floor);
            Prims.Box("Ledge", root, new Vector3(0, LedgeHeight * 0.5f, (LedgeZ + r.Z1) * 0.5f), new Vector3(HalfWidth * 2f, LedgeHeight, r.Z1 - LedgeZ), floor);
            var edge = Mats.Emissive(r.Color, 3f);
            Prims.Box("LedgeEdge", root, new Vector3(0, LedgeHeight - 0.05f, LedgeZ - 0.02f), new Vector3(HalfWidth * 2f, 0.06f, 0.06f), edge, false, false);
            foreach (var z in new[] { ChasmZ0, ChasmZ1 })
                Prims.Box("ChasmEdge", root, new Vector3(0, 0.01f, z + (z == ChasmZ0 ? -0.05f : 0.05f)), new Vector3(HalfWidth * 2f, 0.03f, 0.08f), edge, false, false);

            // The pit: dark walls falling away to a deep red glow.
            var pit = Mats.Solid(new Color(0.03f, 0.03f, 0.04f), 0.2f, 0.3f);
            Prims.Box("PitNear", root, new Vector3(0, -6f, ChasmZ0 + 0.2f), new Vector3(HalfWidth * 2f, 12f, 0.4f), pit);
            Prims.Box("PitFar", root, new Vector3(0, -6f, ChasmZ1 - 0.2f), new Vector3(HalfWidth * 2f, 12f, 0.4f), pit);
            Prims.Box("PitFloor", root, new Vector3(0, -12f, (ChasmZ0 + ChasmZ1) * 0.5f), new Vector3(HalfWidth * 2f, 0.4f, ChasmZ1 - ChasmZ0), Mats.Emissive(Mats.Red, 1.2f));
            AddLight(root, new Vector3(0, -8f, (ChasmZ0 + ChasmZ1) * 0.5f), Mats.Red, 8f, 14f);

            var chasm = new GameObject("Chasm");
            chasm.transform.SetParent(root, false);
            chasm.transform.position = new Vector3(0, 0, (ChasmZ0 + ChasmZ1) * 0.5f);
            WorldEntity.Register(chasm, "chasm", "gap", "chasm", r.Index, "gap", "chasm", "pit", "hole", "void", "abyss");
            var ledge = new GameObject("LedgeAnchor");
            ledge.transform.SetParent(root, false);
            ledge.transform.position = new Vector3(0, LedgeHeight, LedgeZ + 1f);
            WorldEntity.Register(ledge, "ledge", "ledge", "high ledge", r.Index, "ledge", "upper level", "up there", "high platform", "exit ledge");

            var forge = GameDirector.I.Forge;
            var bridge = Barrier.Add("chasm", ChasmZ0, BarrierKind.Bridge, "A chasm. Build a bridge with words: \"build a bridge over the gap\".");
            bridge.Depth = ChasmZ1 - ChasmZ0;
            bridge.Blocking = () => float.IsNaN(forge.BridgeLane);
            bridge.Lane = () => forge.BridgeLane;
            var climb = Barrier.Add("ledge", LedgeZ - 3.8f, BarrierKind.Climb, "Too high to climb. Say \"build stairs to the ledge\".");
            climb.Depth = 4.2f;
            climb.Blocking = () => float.IsNaN(forge.ClimbLane);
            climb.Lane = () => forge.ClimbLane;

            WorldLabel.Create(root, new Vector3(0, 5.2f, ChasmZ0 - 1f), "SPEAK IT INTO EXISTENCE", 0.5f, r.Color, 2.2f, true);
            Dust(root, new Vector3(0, 3f, r.Center), new Vector3(13f, 7f, 25f), new Color(1f, 0.7f, 0.4f, 0.45f));
        }

        void BuildCore(Room r)
        {
            var root = transform.Find($"Room {r.Letter} {r.Name}");
            Terminal = CoreTerminal.Build(root, new Vector3(3f, r.FloorY, 164f), r.Index);
            Portal = ExitPortal.Build(root, new Vector3(0, r.FloorY, 172f), r.Index);
            Dust(root, new Vector3(0, r.FloorY + 3f, r.Center), new Vector3(13f, 5f, 19f), new Color(0.6f, 1f, 0.8f, 0.45f));
        }
    }
}

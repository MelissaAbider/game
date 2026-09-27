using System;
using System.Collections.Generic;
using UnityEngine;

namespace EchoShift
{
    /// <summary>
    /// Side-scrolling presentation of the facility. The 3D level keeps running underneath (physics, hazards,
    /// voice logic), but what you see is Nano Banana art: one painted backdrop per chamber, painted obstacle
    /// props that follow the state of the real objects, and the hero render as the subject.
    /// Launch with -3d to get the old third-person view back.
    /// </summary>
    public sealed class SideView : MonoBehaviour
    {
        static bool? enabled;

        /// <summary>On unless -3d is passed or the generated art is missing from Resources/Game.</summary>
        public static bool Enabled => enabled ??= Array.IndexOf(Environment.GetCommandLineArgs(), "-3d") < 0 && Resources.Load<TextAsset>("Game/robot_idle");

        /// <summary>Where the painted floor meets the walking line in the room backdrops (fraction of image height).</summary>
        const float PaintedFloor = 0.2f;
        const float BackdropHeight = 14f;
        const float BackdropX = -8f, PropX = -0.6f, BehindX = -1.4f, FrontX = 1.2f;

        readonly List<Action> ticks = new List<Action>();

        public static SideView Setup(Facility f, Subject subject)
        {
            var view = new GameObject("SideView").AddComponent<SideView>();
            view.Build(f, subject);
            return view;
        }

        void Build(Facility f, Subject subject)
        {
            RenderSettings.fog = false;
            // Hide the procedural 3D look; keep colliders, logic and the floating dust.
            foreach (var r in f.GetComponentsInChildren<Renderer>(true))
                if (!(r is ParticleSystemRenderer)) r.enabled = false;

            for (int i = 0; i < f.Rooms.Count; i++) Backdrop(f, f.Rooms[i], i);
            // Soft dark bulkhead between two paintings so chambers blend instead of cutting hard.
            for (int i = 1; i < f.Rooms.Count; i++)
            {
                var r = f.Rooms[i];
                Quad("Seam " + r.Letter, Ui.Radial(), BackdropX + 0.3f, r.Z0, r.FloorY + 3f, 3.2f, 26f, new Color(0.01f, 0.012f, 0.02f, 1f), out _);
                Quad("SeamCore " + r.Letter, Ui.Radial(), BackdropX + 0.31f, r.Z0, r.FloorY + 3f, 1.4f, 26f, new Color(0.005f, 0.006f, 0.01f, 1f), out _);
            }
            ForgeGround(f);
            Props(f);
            RobotSprite.Attach(subject.transform, subject.Rig.transform, Color.white,
                () => (subject.PlanarSpeed, subject.Grounded, subject.Crouching, subject.Reacting));
        }

        void LateUpdate()
        {
            foreach (var t in ticks) t();
        }

        // ── Building blocks ─────────────────────────────────────────────────

        /// <summary>A camera-facing (+X) textured quad; x is only draw depth, z/y are world position of its center.</summary>
        Material Quad(string name, Texture tex, float x, float z, float y, float width, float height, Color tint, out Transform t, bool additive = false)
        {
            var mat = Mats.Hologram(Color.white, 1f, tint.a, additive);
            mat.SetColor("_BaseColor", tint);
            if (tex) mat.SetTexture("_BaseMap", tex);
            var go = Prims.Make(name, PrimitiveType.Quad, transform, new Vector3(x, y, z), new Vector3(width, height, 1f), mat, false, false);
            go.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
            t = go.transform;
            return mat;
        }

        /// <summary>Chroma-keyed prop standing on the floor at z (bottom-centered), sized by height and the art's aspect.</summary>
        Material Prop(string id, float x, float z, float floorY, float height, out Transform t, Color? tint = null, float widthScale = 1f)
        {
            t = null;
            var tex = HeroArt.Cutout("Game/" + id);
            if (!tex) return null;
            float width = height * tex.width / tex.height * widthScale;
            return Quad(id, tex, x, z, floorY + height * 0.5f, width, height, tint ?? Color.white, out t);
        }

        void Backdrop(Facility f, Room r, int index)
        {
            var tex = HeroArt.Plain("Game/room_" + r.Letter.ToLowerInvariant());
            if (!tex) return;
            // First and last chambers extend past the facility ends so the camera never sees a void.
            float z0 = index == 0 ? r.Z0 - 12f : r.Z0;
            float z1 = index == f.Rooms.Count - 1 ? r.Z1 + 12f : r.Z1;
            float len = z1 - z0 + 0.05f, h = BackdropHeight;
            float bottom = r.FloorY - PaintedFloor * h;
            var mat = Quad("Backdrop " + r.Letter, tex, BackdropX + index * 0.01f, (z0 + z1) * 0.5f, bottom + h * 0.5f, len, h, new Color(0.9f, 0.9f, 0.95f), out _);
            // Crop (never stretch) the painting to the chamber's proportions, keeping its floor line in place.
            float imageAspect = tex.width / (float)tex.height, quadAspect = len / h;
            var scale = Vector2.one;
            var offset = Vector2.zero;
            if (quadAspect < imageAspect) { scale.x = quadAspect / imageAspect; offset.x = (1f - scale.x) * 0.5f; }
            else { scale.y = imageAspect / quadAspect; offset.y = PaintedFloor * (1f - scale.y); }
            mat.SetTextureScale("_BaseMap", scale);
            mat.SetTextureOffset("_BaseMap", offset);
        }

        /// <summary>The forge's chasm and upper ledge are gameplay geometry the backdrop can't know about.</summary>
        void ForgeGround(Facility f)
        {
            var forge = f.Rooms[6];
            // Pit: darkness falling to a molten glow, framed by two glowing cliff edges.
            float pitZ = (f.ChasmZ0 + f.ChasmZ1) * 0.5f, pitW = f.ChasmZ1 - f.ChasmZ0, pitTop = forge.FloorY + 0.02f, pitH = 4f;
            Quad("PitShade", null, BackdropX + 0.4f, pitZ, pitTop - pitH * 0.5f, pitW, pitH, new Color(0.01f, 0.01f, 0.02f, 1f), out _);
            Quad("PitGlow", Ui.Ramp(false, 2.2f), BackdropX + 0.5f, pitZ, pitTop - pitH * 0.5f, pitW, pitH, new Color(1f, 0.32f, 0.08f, 0.8f), out _);
            foreach (var z in new[] { f.ChasmZ0, f.ChasmZ1 })
            {
                Quad("CliffEdge", null, FrontX, z, pitTop - pitH * 0.5f, 0.07f, pitH, Glow(forge.Color, 2f), out _, true);
                Quad("CliffLip", null, FrontX, z + (z == f.ChasmZ0 ? -0.6f : 0.6f), pitTop - 0.02f, 1.2f, 0.06f, Glow(forge.Color, 2.5f), out _, true);
            }
            // Upper ledge: a solid platform block rising to the core level.
            float lz0 = f.LedgeZ, lz1 = forge.Z1, top = f.LedgeHeight;
            Quad("Ledge", null, FrontX, (lz0 + lz1) * 0.5f, (top - 3f) * 0.5f, lz1 - lz0, top + 3f, new Color(0.07f, 0.08f, 0.11f, 1f), out _);
            Quad("LedgeEdge", null, FrontX + 0.01f, (lz0 + lz1) * 0.5f, top - 0.03f, lz1 - lz0, 0.06f, Glow(forge.Color, 2.5f), out _, true);
            Quad("LedgeFace", null, FrontX + 0.01f, lz0 + 0.03f, (top - 3f) * 0.5f, 0.06f, top + 3f, Glow(forge.Color, 1.5f), out _, true);
        }

        static Color Glow(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, 1f);

        void Props(Facility f)
        {
            float floor0 = f.Rooms[0].FloorY;

            // Start pod behind the spawn point.
            if (f.StartPod) Prop("prop_pod", BehindX, f.StartPod.position.z, floor0, 3.4f, out _);

            // Laser gates: knee-high curtains to jump, chest-high beams to crouch under.
            foreach (var gate in f.Lasers)
            {
                var g = gate;
                var mat = g.Overhead
                    ? Prop("prop_laser_high", PropX, g.Z, g.transform.position.y, 2.4f, out _)
                    : Prop("prop_laser_low", PropX, g.Z, g.transform.position.y, 1.1f, out _);
                // Red-biased tint removes the yellow-green halo left by the green-screen key, plus a laser flicker.
                if (mat) ticks.Add(() => mat.SetColor("_BaseColor", Glow(new Color(1f, 0.62f, 0.66f), 1f + 0.12f * Mathf.Sin(Time.time * 37f))));
            }

            // Sonic glass: resonates with your voice, gone once shattered.
            if (f.Glass)
            {
                var glass = f.Glass;
                var mat = Prop("prop_glass", PropX, glass.transform.position.z, glass.transform.position.y, 4.4f, out var t);
                if (mat) ticks.Add(() =>
                {
                    t.gameObject.SetActive(glass.Intact);
                    float jitter = glass.Resonance * 0.03f * Mathf.Sin(Time.time * 60f);
                    t.localPosition = new Vector3(PropX, t.localPosition.y, glass.transform.position.z + jitter);
                    mat.SetColor("_BaseColor", Color.Lerp(Color.white, new Color(1.4f, 1.1f, 1.6f), glass.Resonance));
                });
            }

            // Acoustic sentinel hanging over its listening zone; it glows red as you get too loud.
            if (f.Sentinel)
            {
                var s = f.Sentinel;
                var room = f.RoomAt(s.transform.position.z);
                var mat = Prop("prop_sentinel", BehindX, s.transform.position.z, room.FloorY + 2.5f, 1.6f, out _);
                if (mat) ticks.Add(() =>
                {
                    var c = s.Bypassed ? new Color(0.45f, 0.5f, 0.55f) : Color.Lerp(Color.white, new Color(1.6f, 0.35f, 0.35f), s.Loudness);
                    mat.SetColor("_BaseColor", c);
                });
            }

            // Chrono sweepers: turret + the projection of its rotating golden beam.
            foreach (var sweeper in f.Sweepers)
            {
                var sw = sweeper;
                var p = sw.transform.position;
                Prop("prop_sweeper", BehindX, p.z, p.y, 1.8f, out _);
                var beam = Quad("SweeperBeam", null, PropX + 0.05f, p.z, p.y + sw.BeamHeight, 1f, 0.09f, Glow(Mats.Amber, 3f), out var bt, true);
                ticks.Add(() =>
                {
                    var d = sw.ArmDirection;
                    float along = d.z * sw.Radius;
                    bt.localScale = new Vector3(Mathf.Max(0.05f, Mathf.Abs(along)), 0.09f, 1f);
                    bt.localPosition = new Vector3(PropX + 0.05f, p.y + sw.BeamHeight, p.z + along * 0.5f);
                    beam.SetColor("_BaseColor", Glow(Mats.Amber, 1.5f + 2.5f * Mathf.Abs(d.z)));
                });
            }

            // Every door (chamber doors, echo door, vault gate) slides up out of the way when open.
            foreach (var door in f.GetComponentsInChildren<SlidingDoor>(true))
            {
                var d = door;
                bool vault = d.GetComponentInParent<ResonanceVault>();
                var pos = d.transform.position;
                float height = vault ? 3.6f : 3.3f;
                var mat = Prop(vault ? "prop_vault" : "prop_door", PropX + 0.1f, pos.z, pos.y, height, out var t, null, 0.9f);
                if (!mat) continue;
                float baseY = pos.y + height * 0.5f;
                ticks.Add(() =>
                {
                    // Opens upward and dissolves, so it never hangs at the top of the screen.
                    float o = d.Openness;
                    t.localPosition = new Vector3(t.localPosition.x, baseY + o * 1.6f, pos.z);
                    mat.SetColor("_BaseColor", new Color(1f, 1f, 1f, 1f - o));
                    t.gameObject.SetActive(o < 0.99f);
                });
            }

            // Pressure plate on the walkway, lighting up when something stands on it.
            if (f.Plate)
            {
                var plate = f.Plate;
                var p = plate.transform.position;
                var mat = Prop("prop_plate", PropX, p.z, p.y - 0.2f, 0.7f, out _, null, 0.7f);
                if (mat) ticks.Add(() => mat.SetColor("_BaseColor", plate.Pressed ? new Color(1.6f, 1.2f, 1.5f) : new Color(0.75f, 0.75f, 0.8f)));
            }

            if (f.Terminal)
            {
                var term = f.Terminal;
                var p = term.transform.position;
                var mat = Prop("prop_terminal", BehindX, p.z, p.y, 2f, out _);
                if (mat) ticks.Add(() => mat.SetColor("_BaseColor", term.Activated ? new Color(1.3f, 1.5f, 1.3f) : Color.white));
            }

            if (f.Portal)
            {
                var portal = f.Portal;
                var p = portal.transform.position;
                var mat = Prop("prop_portal", BehindX, p.z, p.y, 3.2f, out _);
                if (mat) ticks.Add(() => mat.SetColor("_BaseColor",
                    portal.Active ? Glow(Color.white, 1.1f + 0.15f * Mathf.Sin(Time.unscaledTime * 4f)) : new Color(0.35f, 0.4f, 0.4f, 1f)));
            }
        }
    }
}

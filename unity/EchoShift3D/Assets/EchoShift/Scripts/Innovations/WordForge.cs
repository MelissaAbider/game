using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EchoShift
{
    /// <summary>
    /// Build with words: "build a bridge of ice over the gap". Gemini picks the kind and material,
    /// the forge validates the energy cost, materializes the object as a hologram, solidifies it,
    /// and asks Gemini's image model to paint the described material onto it.
    /// </summary>
    public sealed class WordForge : MonoBehaviour
    {
        public const int MaxEnergy = 10;
        public int Energy { get; private set; } = MaxEnergy;
        public int Spent { get; private set; }

        public static readonly Dictionary<string, int> Costs = new Dictionary<string, int>
        {
            { "bridge", 4 }, { "stairs", 3 }, { "ramp", 3 }, { "platform", 2 }, { "crate", 1 }, { "shield", 2 }, { "light", 1 },
        };

        sealed class Built
        {
            public string Kind;
            public int Cost;
            public GameObject Root;
            public float LaneX;
        }

        readonly List<Built> builds = new List<Built>();
        Facility facility;

        public void Init(Facility f) => facility = f;

        public float BridgeLane => LaneOf("bridge");
        public float ClimbLane
        {
            get
            {
                float stairs = LaneOf("stairs");
                return float.IsNaN(stairs) ? LaneOf("ramp") : stairs;
            }
        }

        float LaneOf(string kind)
        {
            foreach (var b in builds) if (b.Kind == kind && b.Root) return b.LaneX;
            return float.NaN;
        }

        public bool Build(GameAction action)
        {
            var director = GameDirector.I;
            var s = director.Subject;
            string kind = (action.BuildKind ?? "").ToLowerInvariant();
            if (!Costs.TryGetValue(kind, out int cost))
            {
                director.Say("The forge can make bridges, stairs, ramps, platforms, crates, shields and lights.", Hud.Warning);
                return false;
            }
            if (Energy < cost)
            {
                director.Say($"Not enough energy: a {kind} costs {cost}, you have {Energy}. Say \"recycle\" to reclaim it.", Hud.Warning);
                return false;
            }

            var pos = s.transform.position;
            float lane = Mathf.Clamp(pos.x, -facility.HalfWidth + 1.6f, facility.HalfWidth - 1.6f);
            GameObject root;
            Vector3 size;
            switch (kind)
            {
                case "bridge":
                    if (!Near(pos, facility.ChasmZ0 - 12f, facility.ChasmZ1 + 1f)) return Refuse("There is no gap to bridge here.");
                    root = MakeRoot(kind, new Vector3(lane, -0.15f, (facility.ChasmZ0 + facility.ChasmZ1) * 0.5f));
                    size = new Vector3(2.6f, 0.3f, facility.ChasmZ1 - facility.ChasmZ0 + 1.2f);
                    Piece(root, Vector3.zero, size);
                    for (float z = -size.z * 0.5f; z <= size.z * 0.5f; z += 1.1f)
                        foreach (var side in new[] { -1f, 1f })
                            Piece(root, new Vector3(side * 1.25f, 0.55f, z), new Vector3(0.06f, 0.9f, 0.06f), false);
                    break;
                case "stairs":
                    if (!Near(pos, facility.ChasmZ0 - 2f, facility.LedgeZ + 1f) || pos.y > 1f) return Refuse("There is no ledge to climb here.");
                    root = MakeRoot(kind, new Vector3(lane, 0f, facility.LedgeZ));
                    const int steps = 8;
                    float rise = facility.LedgeHeight / steps, depth = 0.45f;
                    for (int i = 0; i < steps; i++)
                    {
                        float h = rise * (i + 1);
                        Piece(root, new Vector3(0, h * 0.5f, -(steps - i) * depth + depth * 0.5f), new Vector3(2.4f, h, depth));
                    }
                    size = new Vector3(2.4f, facility.LedgeHeight, steps * depth);
                    break;
                case "ramp":
                    if (!Near(pos, facility.ChasmZ0 - 2f, facility.LedgeZ + 1f) || pos.y > 1f) return Refuse("There is no ledge to climb here.");
                    float run = facility.LedgeZ - (facility.ChasmZ1 + 0.4f);
                    float angle = Mathf.Atan2(facility.LedgeHeight, run);
                    float length = Mathf.Sqrt(run * run + facility.LedgeHeight * facility.LedgeHeight);
                    var mid = new Vector3(lane, facility.LedgeHeight * 0.5f, facility.LedgeZ - run * 0.5f);
                    mid += new Vector3(0, -Mathf.Cos(angle), Mathf.Sin(angle)) * 0.15f;
                    root = MakeRoot(kind, mid);
                    root.transform.rotation = Quaternion.Euler(-angle * Mathf.Rad2Deg, 0, 0);
                    size = new Vector3(2.4f, 0.3f, length);
                    Piece(root, Vector3.zero, size);
                    break;
                case "platform":
                    root = MakeRoot(kind, pos + s.transform.forward * 2.3f + Vector3.up * 1.0f);
                    size = new Vector3(2.2f, 0.3f, 2.2f);
                    Piece(root, Vector3.zero, size);
                    break;
                case "crate":
                    root = MakeRoot(kind, pos + s.transform.forward * 1.9f + Vector3.up * 0.5f);
                    size = Vector3.one;
                    Piece(root, Vector3.zero, size);
                    break;
                case "shield":
                    s.SetShield(true);
                    root = MakeRoot(kind, pos);
                    root.transform.SetParent(s.transform, true);
                    size = Vector3.one;
                    break;
                default: // light
                    root = MakeRoot(kind, s.transform.position + new Vector3(0.7f, 2.3f, 0));
                    root.transform.SetParent(s.transform, true);
                    Prims.Make("Orb", PrimitiveType.Sphere, root.transform, Vector3.zero, Vector3.one * 0.25f, Mats.Emissive(Color.white, 5f), false, false);
                    var l = root.AddComponent<Light>();
                    l.type = LightType.Point;
                    l.range = 10f;
                    l.intensity = 4f;
                    l.color = new Color(0.9f, 0.95f, 1f);
                    size = Vector3.one * 0.25f;
                    break;
            }

            Energy -= cost;
            Spent += cost;
            builds.Add(new Built { Kind = kind, Cost = cost, Root = root, LaneX = lane });
            string material = (action.Material ?? "").Trim();
            string title = kind.ToUpperInvariant() + (material.Length > 0 ? " · " + material.ToUpperInvariant() : "");
            var label = WorldLabel.Create(root.transform, new Vector3(0, size.y * 0.5f + 1.1f, 0), title, 0.3f, Mats.Orange, 2.2f, false, true);
            label.transform.rotation = Quaternion.identity;
            Destroy(label.gameObject, 6f);
            StartCoroutine(Materialize(root, material));
            Sfx.Play("build", 0.8f);
            director.Say($"{Title(kind)} forged{(material.Length > 0 ? " from " + material : "")}. Energy {Energy} of {MaxEnergy}.", Hud.Build);
            return true;
        }

        static bool Near(Vector3 p, float z0, float z1) => p.z > z0 && p.z < z1;

        bool Refuse(string line)
        {
            GameDirector.I.Say(line, Hud.Warning);
            return false;
        }

        static string Title(string kind) => char.ToUpperInvariant(kind[0]) + kind.Substring(1);

        GameObject MakeRoot(string kind, Vector3 position)
        {
            var go = new GameObject("Forged " + kind);
            go.transform.position = position;
            return go;
        }

        static void Piece(GameObject root, Vector3 localPos, Vector3 size, bool collider = true)
        {
            var go = Prims.Make("Piece", PrimitiveType.Cube, root.transform, localPos, size, Mats.Hologram(Mats.Orange, 1.6f, 0.45f), collider, false);
            if (go.TryGetComponent<Collider>(out var c)) c.enabled = false;
        }

        IEnumerator Materialize(GameObject root, string material)
        {
            var renderers = root.GetComponentsInChildren<MeshRenderer>();
            var colliders = root.GetComponentsInChildren<Collider>(true);
            var finalScale = root.transform.localScale;

            // Grow like a 3D printer, bottom to top.
            float t = 0f;
            while (t < 1f && root)
            {
                t += Time.deltaTime / 0.9f;
                root.transform.localScale = new Vector3(finalScale.x, finalScale.y * Tween.OutBack(Mathf.Clamp01(t)), finalScale.z);
                yield return null;
            }
            if (!root) yield break;
            root.transform.localScale = finalScale;

            var solid = Mats.Solid(new Color(0.75f, 0.8f, 0.86f), 0.7f, 0.3f);
            var edge = Mats.Emissive(Mats.Orange, 2.5f, new Color(0.25f, 0.2f, 0.18f));
            foreach (var r in renderers)
            {
                if (!r || r.name == "Orb") continue;
                bool thin = r.transform.localScale.x < 0.1f;
                r.sharedMaterial = thin ? edge : solid;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            foreach (var c in colliders) if (c) c.enabled = true;

            if (material.Length == 0) yield break;
            string url = null;
            yield return Backend.PostJson("/api/design/texture", "{\"material\":" + Json.Quote(material) + "}",
                text => url = Json.ParseObject(text).Str("url", null), null, 120);
            if (string.IsNullOrEmpty(url) || !root) yield break;
            Texture2D tex = null;
            yield return Backend.Texture(url, x => tex = x);
            if (!tex || !root) yield break;
            tex.wrapMode = TextureWrapMode.Repeat;
            var painted = Mats.Solid(Color.white, 0.6f, 0.1f);
            painted.SetTexture("_BaseMap", tex);
            painted.SetTextureScale("_BaseMap", new Vector2(1f, 3f));
            foreach (var r in renderers)
                if (r && r.sharedMaterial == solid) r.sharedMaterial = painted;
            Sfx.Play("objective", 0.5f);
            GameDirector.I?.Say($"Material synthesized: {material}.", Hud.Build);
        }

        public void Recycle()
        {
            int refund = 0;
            foreach (var b in builds)
            {
                refund += b.Cost;
                if (b.Root) Destroy(b.Root);
            }
            builds.Clear();
            GameDirector.I.Subject.SetShield(false);
            Energy = Mathf.Min(MaxEnergy, Energy + refund);
            Sfx.Play("rewind", 0.5f);
            GameDirector.I.Say($"Recycled. Energy {Energy} of {MaxEnergy}.", Hud.Build);
        }

        public void ConsumeShield()
        {
            for (int i = builds.Count - 1; i >= 0; i--)
            {
                if (builds[i].Kind != "shield") continue;
                if (builds[i].Root) Destroy(builds[i].Root);
                builds.RemoveAt(i);
                break;
            }
        }
    }
}

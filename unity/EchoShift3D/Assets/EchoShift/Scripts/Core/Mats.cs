using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace EchoShift
{
    /// <summary>
    /// URP material factory. Template materials in Resources/EchoMaterials (created by the editor setup)
    /// keep the right shader variants in player builds; Shader.Find is the fallback in the editor.
    /// </summary>
    public static class Mats
    {
        public static readonly Color Cyan = new Color(0.25f, 0.9f, 1f);
        public static readonly Color Red = new Color(1f, 0.2f, 0.37f);
        public static readonly Color Violet = new Color(0.71f, 0.48f, 1f);
        public static readonly Color Amber = new Color(0.97f, 0.78f, 0.42f);
        public static readonly Color Orange = new Color(1f, 0.54f, 0.24f);
        public static readonly Color Blue = new Color(0.18f, 0.6f, 1f);
        public static readonly Color Green = new Color(0.51f, 1f, 0.72f);
        public static readonly Color Graphite = new Color(0.07f, 0.09f, 0.12f);

        static Shader lit, unlit, particles;
        static Shader Lit => lit ? lit : (lit = Shader.Find("Universal Render Pipeline/Lit"));
        static Shader Unlit => unlit ? unlit : (unlit = Shader.Find("Universal Render Pipeline/Unlit"));
        static Shader ParticleShader => particles ? particles : (particles = Shader.Find("Universal Render Pipeline/Particles/Unlit"));

        static Material FromTemplate(string name, Shader fallback)
        {
            var template = Resources.Load<Material>("EchoMaterials/" + name);
            return template ? new Material(template) : new Material(fallback);
        }

        public static Material Solid(Color color, float smoothness = 0.5f, float metallic = 0f)
        {
            var m = FromTemplate("Lit", Lit);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            return m;
        }

        /// <summary>HDR emissive surface: values above 1 bloom into neon.</summary>
        public static Material Emissive(Color glow, float intensity = 3f, Color? baseColor = null)
        {
            var m = FromTemplate("LitEmissive", Lit);
            m.SetColor("_BaseColor", baseColor ?? Color.black);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", glow * intensity);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            return m;
        }

        public static void SetEmission(Material m, Color glow, float intensity)
        {
            if (!m) return;
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", glow * intensity);
        }

        public static Material Glass(Color tint, float alpha = 0.25f, float smoothness = 0.95f)
        {
            var m = FromTemplate("LitTransparent", Lit);
            MakeTransparent(m, false);
            tint.a = alpha;
            m.SetColor("_BaseColor", tint);
            m.SetFloat("_Smoothness", smoothness);
            return m;
        }

        /// <summary>Unlit see-through light: holograms, beams, ghost clones.</summary>
        public static Material Hologram(Color color, float intensity = 2f, float alpha = 0.6f, bool additive = true)
        {
            var m = FromTemplate(additive ? "UnlitAdditive" : "UnlitTransparent", Unlit);
            MakeTransparent(m, additive);
            var c = color * intensity;
            c.a = alpha;
            m.SetColor("_BaseColor", c);
            return m;
        }

        public static Material Particles(Color color)
        {
            var m = FromTemplate("ParticlesAdditive", ParticleShader);
            MakeTransparent(m, true);
            m.SetColor("_BaseColor", color);
            m.SetTexture("_BaseMap", SoftDot);
            return m;
        }

        public static void MakeTransparent(Material m, bool additive)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_SrcBlend", additive ? (float)BlendMode.One : (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHATEST_ON");
            m.renderQueue = (int)RenderQueue.Transparent;
        }

        // ── Gemini textures ─────────────────────────────────────────────────
        static readonly List<(Material mat, string texId)> Texturable = new List<(Material, string)>();
        static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();

        /// <summary>Environment material that picks up the Gemini-generated texture whenever it arrives.</summary>
        public static Material Environment(string texId, Color tint, Vector2 tiling, float smoothness, float metallic)
        {
            var m = Solid(tint, smoothness, metallic);
            m.SetTextureScale("_BaseMap", tiling);
            Texturable.Add((m, texId));
            if (Textures.TryGetValue(texId, out var tex)) m.SetTexture("_BaseMap", tex);
            return m;
        }

        public static void RegisterTexture(string id, Texture2D tex)
        {
            if (!tex) return;
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.anisoLevel = 8;
            Textures[id] = tex;
            foreach (var (mat, texId) in Texturable)
                if (texId == id && mat) mat.SetTexture("_BaseMap", tex);
        }

        public static bool HasTexture(string id) => Textures.ContainsKey(id);

        static Texture2D softDot;

        public static Texture2D SoftDot
        {
            get
            {
                if (softDot) return softDot;
                const int size = 64;
                softDot = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    a = a * a;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
                softDot.SetPixels32(pixels);
                softDot.Apply();
                return softDot;
            }
        }
    }
}

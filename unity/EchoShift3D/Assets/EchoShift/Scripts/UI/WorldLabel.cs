using System.Collections.Generic;
using UnityEngine;

namespace EchoShift
{
    /// <summary>Glowing text floating in the 3D world (room names, voice hints, holograms).</summary>
    public sealed class WorldLabel : MonoBehaviour
    {
        static readonly List<WorldLabel> Live = new List<WorldLabel>();
        static Shader shader;
        static bool hooked;

        TextMesh mesh;
        Material material;
        Font font;
        Color color;
        float intensity;
        public bool Billboard;

        public string Text
        {
            get => mesh.text;
            set => mesh.text = value;
        }

        public static WorldLabel Create(Transform parent, Vector3 localPosition, string text, float height, Color color,
            float intensity = 1.6f, bool title = false, bool billboard = false)
        {
            var go = new GameObject("Label: " + text);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var label = go.AddComponent<WorldLabel>();
            label.Init(text, height, color, intensity, title, billboard);
            return label;
        }

        void Init(string text, float height, Color c, float glow, bool title, bool billboard)
        {
            font = Ui.Font(title ? "Orbitron" : "RajdhaniBold");
            mesh = gameObject.AddComponent<TextMesh>();
            mesh.font = font;
            mesh.fontSize = 128;
            mesh.characterSize = height / 12.8f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.richText = false;
            mesh.text = text;
            var renderer = GetComponent<MeshRenderer>();
            if (!shader) shader = Shader.Find("EchoShift/WorldText");
            var template = Resources.Load<Material>("EchoMaterials/WorldText");
            material = template ? new Material(template) : (shader ? new Material(shader) : new Material(font.material));
            material.mainTexture = font.material.mainTexture;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Billboard = billboard;
            SetColor(c, glow);
            Live.Add(this);
            if (!hooked)
            {
                hooked = true;
                Font.textureRebuilt += OnFontRebuilt;
            }
        }

        public void SetColor(Color c, float glow)
        {
            color = c;
            intensity = glow;
            var hdr = c * glow;
            hdr.a = c.a;
            if (material.HasProperty("_Color")) material.SetColor("_Color", hdr);
            mesh.color = Color.white;
        }

        public void SetAlpha(float a)
        {
            var c = color;
            c.a = a;
            SetColor(c, intensity);
        }

        static void OnFontRebuilt(Font f)
        {
            foreach (var l in Live)
                if (l && l.font == f) l.material.mainTexture = f.material.mainTexture;
        }

        void OnDestroy() => Live.Remove(this);

        void LateUpdate()
        {
            if (!Billboard) return;
            var cam = Camera.main;
            if (cam) transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
        }
    }
}

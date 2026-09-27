using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EchoShift
{
    /// <summary>Code-built UGUI helpers (no prefabs, no scene setup).</summary>
    public static class Ui
    {
        static readonly Dictionary<string, Font> Fonts = new Dictionary<string, Font>();

        public static Font Font(string name)
        {
            if (Fonts.TryGetValue(name, out var f) && f) return f;
            f = Resources.Load<Font>("Fonts/" + name);
            if (!f) f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Fonts[name] = f;
            return f;
        }

        public static Font Title => Font("Orbitron");
        public static Font Body => Font("Rajdhani");
        public static Font Bold => Font("RajdhaniBold");

        public static Canvas CreateCanvas(string name, int order)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            if (!Object.FindFirstObjectByType<EventSystem>())
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            return canvas;
        }

        /// <summary>RectTransform anchored at a point of its parent (anchor & pivot share the same normalized point).</summary>
        public static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static Image Panel(Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color color, string name = "Panel")
        {
            var img = Rect(name, parent, anchor, position, size).gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static Image Border(Image panel, Color color, float thickness = 2f)
        {
            var o = panel.gameObject.AddComponent<Outline>();
            o.effectColor = color;
            o.effectDistance = new Vector2(thickness, -thickness);
            return panel;
        }

        public static Text Label(Transform parent, string text, Font font, int size, Color color, TextAnchor align,
            Vector2 anchor, Vector2 position, Vector2 box, string name = "Label")
        {
            var t = Rect(name, parent, anchor, position, box).gameObject.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.text = text;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.supportRichText = true;
            return t;
        }

        public static Text Glow(Text t, Color color, float distance = 2f)
        {
            var s = t.gameObject.AddComponent<Shadow>();
            s.effectColor = color;
            s.effectDistance = new Vector2(0, -distance);
            return t;
        }

        /// <summary>Horizontal bar whose fill is driven by anchorMax.x (works without sprites).</summary>
        public static RectTransform Bar(Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color back, Color fill, out Image fillImage)
        {
            var bg = Panel(parent, anchor, position, size, back, "Bar");
            var f = new GameObject("Fill", typeof(RectTransform)).GetComponent<RectTransform>();
            f.SetParent(bg.transform, false);
            f.anchorMin = Vector2.zero;
            f.anchorMax = new Vector2(0f, 1f);
            f.offsetMin = f.offsetMax = Vector2.zero;
            fillImage = f.gameObject.AddComponent<Image>();
            fillImage.color = fill;
            fillImage.raycastTarget = false;
            return f;
        }

        public static void SetFill(RectTransform fill, float value)
        {
            fill.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
        }

        public static Button Button(Transform parent, string text, Vector2 anchor, Vector2 position, Vector2 size, Color color, UnityEngine.Events.UnityAction onClick)
        {
            var img = Panel(parent, anchor, position, size, color, "Button: " + text);
            img.raycastTarget = true;
            var button = img.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            button.colors = colors;
            button.onClick.AddListener(onClick);
            Label(img.transform, text, Bold, 30, new Color(0.02f, 0.06f, 0.08f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            return button;
        }

        public static RawImage RawImage(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, Texture texture, Color? color = null)
        {
            var rt = Rect(name, parent, anchor, position, size);
            var img = rt.gameObject.AddComponent<RawImage>();
            img.texture = texture;
            img.color = color ?? Color.white;
            img.raycastTarget = false;
            return img;
        }

        public static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

        public static readonly Color Pink = new Color(1f, 0.42f, 0.82f);
        public static readonly Color PanelBg = new Color(0.02f, 0.05f, 0.09f, 0.84f);

        /// <summary>Home-page panel look: thin translucent outline plus four neon corner brackets. Returns the brackets (to recolour).</summary>
        public static List<Image> Frame(Image panel, Color accent, float length = 20f, float thickness = 3f)
        {
            // Thin edge lines (an Outline effect would tint the whole translucent panel).
            var edge = new Color(accent.r, accent.g, accent.b, 0.3f);
            var size = panel.rectTransform.sizeDelta;
            Panel(panel.transform, new Vector2(0.5f, 1), Vector2.zero, new Vector2(size.x, 1.5f), edge, "Edge");
            Panel(panel.transform, new Vector2(0.5f, 0), Vector2.zero, new Vector2(size.x, 1.5f), edge, "Edge");
            Panel(panel.transform, new Vector2(0, 0.5f), Vector2.zero, new Vector2(1.5f, size.y), edge, "Edge");
            Panel(panel.transform, new Vector2(1, 0.5f), Vector2.zero, new Vector2(1.5f, size.y), edge, "Edge");
            var brackets = new List<Image>();
            foreach (var corner in new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 0), new Vector2(1, 1) })
            {
                brackets.Add(Panel(panel.transform, corner, Vector2.zero, new Vector2(length, thickness), accent, "Bracket"));
                brackets.Add(Panel(panel.transform, corner, Vector2.zero, new Vector2(thickness, length), accent, "Bracket"));
            }
            return brackets;
        }

        public static void Tint(List<Image> images, Color color)
        {
            foreach (var i in images) if (i) i.color = color;
        }

        /// <summary>Stretched full-parent RawImage (for backdrops and gradient overlays).</summary>
        public static RawImage Fill(Transform parent, string name, Texture texture, Color color)
        {
            var img = Stretch(name, parent).gameObject.AddComponent<RawImage>();
            img.texture = texture;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static readonly Dictionary<string, Texture2D> Generated = new Dictionary<string, Texture2D>();

        /// <summary>Linear alpha ramp (white). horizontal: opaque on the left; vertical: opaque at the bottom.</summary>
        public static Texture2D Ramp(bool horizontal, float power = 1f)
        {
            string key = $"ramp{horizontal}{power}";
            if (Generated.TryGetValue(key, out var cached) && cached) return cached;
            const int n = 256;
            var tex = new Texture2D(horizontal ? n : 1, horizontal ? 1 : n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int i = 0; i < n; i++)
            {
                float a = Mathf.Pow(1f - i / (n - 1f), power);
                tex.SetPixel(horizontal ? i : 0, horizontal ? 0 : i, new Color(1, 1, 1, a));
            }
            tex.Apply();
            return Generated[key] = tex;
        }

        /// <summary>Soft radial glow (white, alpha falls off to the edge). ring > 0 makes a hollow ring.</summary>
        public static Texture2D Radial(float ring = 0f, float softness = 1f)
        {
            string key = $"radial{ring}{softness}";
            if (Generated.TryGetValue(key, out var cached) && cached) return cached;
            const int n = 256;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = ring > 0f
                    ? Mathf.Clamp01(1f - Mathf.Abs(d - ring) / (0.04f * softness))
                    : Mathf.Pow(Mathf.Clamp01(1f - d), 1.6f * softness);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Generated[key] = tex;
        }
    }
}

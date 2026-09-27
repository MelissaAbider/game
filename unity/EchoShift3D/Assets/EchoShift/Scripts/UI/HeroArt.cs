using System.Collections.Generic;
using UnityEngine;

namespace EchoShift
{
    /// <summary>
    /// Loads the generated hero portraits (rendered on a flat green studio background), removes the green,
    /// despills the edges and crops to the silhouette — the C# twin of frontend/src/home/cutout.ts.
    /// </summary>
    public static class HeroArt
    {
        static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        /// <summary>Opaque painting (room backdrops) stored as .bytes, loaded as-is.</summary>
        public static Texture2D Plain(string resource)
        {
            if (Cache.TryGetValue(resource, out var cached) && cached) return cached;
            var asset = Resources.Load<TextAsset>(resource);
            if (!asset) return null;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = resource };
            if (!tex.LoadImage(asset.bytes, true)) { Object.Destroy(tex); return null; }
            return Cache[resource] = tex;
        }

        /// <param name="resource">Resources path of a PNG stored as .bytes, e.g. "Images/hero_serious".</param>
        public static Texture2D Cutout(string resource)
        {
            if (Cache.TryGetValue(resource, out var cached) && cached) return cached;
            var asset = Resources.Load<TextAsset>(resource);
            if (!asset) return null;
            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!src.LoadImage(asset.bytes)) { Object.Destroy(src); return null; }

            int w = src.width, h = src.height;
            var px = src.GetPixels32();
            int minX = w, minY = h, maxX = -1, maxY = -1;
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                int greenness = c.g - Mathf.Max(c.r, c.b);
                if (greenness > 90) { px[i] = new Color32(0, 0, 0, 0); continue; }
                if (greenness > 25) c.a = (byte)Mathf.RoundToInt(c.a * Mathf.Clamp01(1f - (greenness - 25) / 65f));
                if (greenness > 0) c.g = (byte)Mathf.Max(c.r, c.b); // pull the green spill out of the edges
                px[i] = c;
                if (c.a <= 24) continue;
                int x = i % w, y = i / w;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
            Object.Destroy(src);
            if (maxX < 0) return null;

            const int pad = 6;
            int cx = Mathf.Max(0, minX - pad), cy = Mathf.Max(0, minY - pad);
            int cw = Mathf.Min(w, maxX + pad + 1) - cx, ch = Mathf.Min(h, maxY + pad + 1) - cy;
            var crop = new Color32[cw * ch];
            for (int y = 0; y < ch; y++)
                System.Array.Copy(px, (cy + y) * w + cx, crop, y * cw, cw);

            var tex = new Texture2D(cw, ch, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = resource };
            tex.SetPixels32(crop);
            tex.Apply(true, true);
            return Cache[resource] = tex;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace AR.Core
{
    /// <summary>Procedural fallback sprites so missing art never blocks a playtest.</summary>
    public static class Placeholder
    {
        private static readonly Dictionary<int, Sprite> _circles = new();
        private static Sprite _white;

        /// <summary>Small white sprite for panels, bars and flashes (1 unit at 4 px per unit).</summary>
        public static Sprite White
        {
            get
            {
                if (_white != null) return _white;
                var tex = new Texture2D(4, 4) { filterMode = FilterMode.Point };
                var px = new Color32[16];
                for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
                tex.SetPixels32(px);
                tex.Apply();
                _white = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
                return _white;
            }
        }

        /// <summary>Soft white disc of the given pixel size at 16 px per unit; tint it with SpriteRenderer.color.</summary>
        public static Sprite Circle(int size)
        {
            if (_circles.TryGetValue(size, out var s)) return s;
            var tex = new Texture2D(size, size) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                byte a = d > 1f ? (byte)0 : (byte)(255 * Mathf.Clamp01((1f - d) * 3f));
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(px);
            tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 16);
            _circles[size] = s;
            return s;
        }
    }
}

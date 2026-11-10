using System;
using System.Collections.Generic;
using System.Threading;
using AR.Core;
using AR.Pooling;
using UnityEngine;

namespace AR.VFX
{
    /// <summary>
    /// Plays pixelfx sprite sheets (Assets/VFX/&lt;category&gt;/&lt;name&gt;.png + .json) with pooled SpriteRenderers.
    /// Frames are cut from the sheet at runtime, so the PNG needs no manual slicing.
    /// </summary>
    public sealed class VfxSystem
    {
        [Serializable] private class SheetJson { public SheetMeta meta; }
        [Serializable] private class SheetMeta { public PixelfxMeta pixelfx; }
        [Serializable] private class PixelfxMeta
        {
            public int fps;
            public bool loop;
            public int frames;
            public int[] frameSize;
            public int[] pivot;
            public int columns;
        }

        private sealed class Sheet
        {
            public Sprite[] Frames;
            public float FrameTime;
        }

        private struct Playing
        {
            public SpriteRenderer Renderer;
            public Sheet Sheet;
            public float Time;
        }

        private readonly Dictionary<string, Sheet> _sheets = new();
        private readonly HashSet<string> _loading = new();
        private readonly List<Playing> _playing = new(64);
        private readonly ComponentPool<SpriteRenderer> _pool;
        private readonly float _pixelsPerUnit;

        public VfxSystem(Transform parent, int sortingOrder, float pixelsPerUnit = 40f)
        {
            _pixelsPerUnit = pixelsPerUnit;
            _pool = new ComponentPool<SpriteRenderer>(() =>
            {
                var go = new GameObject("Vfx");
                go.transform.SetParent(parent, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = sortingOrder;
                go.SetActive(false);
                return sr;
            }, 16, 256);
            _pool.Prewarm(16);
        }

        /// <summary>Load sheets ahead of time (call during the stage intro).</summary>
        public async Awaitable PreloadAsync(string id, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(id) || _sheets.ContainsKey(id) || !_loading.Add(id)) return;
            try
            {
                var tex = await AssetLoader.LoadAsync<Texture2D>(id, ct);
                var json = await AssetLoader.LoadAsync<TextAsset>(id + ".json", ct);
                _sheets[id] = tex != null && json != null ? BuildSheet(tex, json.text) : null;
            }
            finally
            {
                _loading.Remove(id);
            }
        }

        private Sheet BuildSheet(Texture2D tex, string json)
        {
            var m = JsonUtility.FromJson<SheetJson>(json)?.meta?.pixelfx;
            if (m == null || m.frameSize == null || m.frameSize.Length < 2 || m.columns <= 0) return null;
            int w = m.frameSize[0], h = m.frameSize[1];
            var pivot = m.pivot != null && m.pivot.Length >= 2
                ? new Vector2((float)m.pivot[0] / w, 1f - (float)m.pivot[1] / h)
                : new Vector2(0.5f, 0.5f);
            var frames = new Sprite[m.frames];
            for (int i = 0; i < m.frames; i++)
            {
                int cx = i % m.columns, cy = i / m.columns;
                var rect = new Rect(cx * w, tex.height - (cy + 1) * h, w, h);
                frames[i] = Sprite.Create(tex, rect, pivot, _pixelsPerUnit, 0, SpriteMeshType.FullRect);
            }
            return new Sheet { Frames = frames, FrameTime = 1f / Mathf.Max(1, m.fps) };
        }

        /// <summary>Plays a loaded sheet once at pos. Unloaded ids start loading and are skipped this time.</summary>
        public void Play(string id, Vector2 pos, float scale = 1f)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (!_sheets.TryGetValue(id, out var sheet))
            {
                _ = PreloadAsync(id, CancellationToken.None);
                return;
            }
            if (sheet == null) return;
            var sr = _pool.Get();
            sr.sprite = sheet.Frames[0];
            sr.transform.position = pos;
            sr.transform.localScale = new Vector3(scale, scale, 1f);
            _playing.Add(new Playing { Renderer = sr, Sheet = sheet, Time = 0f });
        }

        public void Tick(float dt)
        {
            for (int i = _playing.Count - 1; i >= 0; i--)
            {
                var p = _playing[i];
                p.Time += dt;
                int frame = (int)(p.Time / p.Sheet.FrameTime);
                if (frame >= p.Sheet.Frames.Length)
                {
                    _pool.Release(p.Renderer);
                    _playing[i] = _playing[_playing.Count - 1];
                    _playing.RemoveAt(_playing.Count - 1);
                    continue;
                }
                p.Renderer.sprite = p.Sheet.Frames[frame];
                _playing[i] = p;
            }
        }
    }
}

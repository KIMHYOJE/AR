using System.Collections.Generic;
using AR.Pooling;
using UnityEngine;

namespace AR.Battle
{
    /// <summary>
    /// All bullets of one side, moved in a single loop (no per-bullet Update) and drawn with pooled
    /// SpriteRenderers. Collision is circle-vs-circle math done by the caller via Count/PositionAt/RemoveAt.
    /// </summary>
    public sealed class BulletSystem
    {
        private struct Bullet
        {
            public Vector2 Pos;
            public Vector2 Vel;
            public float Radius;
            public SpriteRenderer Renderer;
        }

        private readonly List<Bullet> _bullets = new(512);
        private readonly ComponentPool<SpriteRenderer> _pool;

        public int Count => _bullets.Count;

        public BulletSystem(Transform parent, string name, int sortingOrder, int prewarm)
        {
            _pool = new ComponentPool<SpriteRenderer>(() =>
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = sortingOrder;
                go.SetActive(false);
                return sr;
            }, prewarm);
            _pool.Prewarm(prewarm);
        }

        public void Spawn(Vector2 pos, Vector2 vel, float radius, Sprite sprite, Color tint, float scale)
        {
            var sr = _pool.Get();
            sr.sprite = sprite;
            sr.color = tint;
            sr.transform.localScale = new Vector3(scale, scale, 1f);
            sr.transform.position = pos;
            _bullets.Add(new Bullet { Pos = pos, Vel = vel, Radius = radius, Renderer = sr });
        }

        public void Tick(float dt)
        {
            for (int i = _bullets.Count - 1; i >= 0; i--)
            {
                var b = _bullets[i];
                b.Pos += b.Vel * dt;
                if (BattleSpace.Outside(b.Pos, 1f))
                {
                    RemoveAt(i);
                    continue;
                }
                b.Renderer.transform.position = b.Pos;
                _bullets[i] = b;
            }
        }

        public Vector2 PositionAt(int i) => _bullets[i].Pos;
        public float RadiusAt(int i) => _bullets[i].Radius;

        /// <summary>Swap-remove: callers iterating must go backwards.</summary>
        public void RemoveAt(int i)
        {
            _pool.Release(_bullets[i].Renderer);
            int last = _bullets.Count - 1;
            _bullets[i] = _bullets[last];
            _bullets.RemoveAt(last);
        }

        public void Clear()
        {
            for (int i = _bullets.Count - 1; i >= 0; i--) RemoveAt(i);
        }
    }
}

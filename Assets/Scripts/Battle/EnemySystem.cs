using System;
using System.Collections.Generic;
using AR.Pooling;
using UnityEngine;
using Random = UnityEngine.Random;

namespace AR.Battle
{
    /// <summary>Moves every active enemy, fires their bullet patterns, and reports leaks past the tower line.</summary>
    public sealed class EnemySystem
    {
        private const float HoverDuration = 14f;
        private const float MaxShootY = BattleSpace.HalfH - 0.5f;
        private const float MinShootY = -BattleSpace.HalfH + 4f;

        private readonly ComponentPool<Enemy> _pool;
        private readonly List<Enemy> _active = new(128);
        private readonly BulletSystem _enemyBullets;
        private readonly Func<string, Sprite> _bulletSprite;

        public IReadOnlyList<Enemy> Active => _active;

        /// <summary>Raised when an enemy crosses the tower line (argument: damage).</summary>
        public event Action<int> Leaked;
        public event Action<Enemy> Shot;

        public EnemySystem(Transform parent, BulletSystem enemyBullets, Func<string, Sprite> bulletSprite)
        {
            _enemyBullets = enemyBullets;
            _bulletSprite = bulletSprite;
            _pool = new ComponentPool<Enemy>(() =>
            {
                var go = new GameObject("Enemy", typeof(SpriteRenderer), typeof(Enemy));
                go.transform.SetParent(parent, false);
                go.GetComponent<SpriteRenderer>().sortingOrder = 10;
                go.SetActive(false);
                return go.GetComponent<Enemy>();
            }, 32, 512);
            _pool.Prewarm(32);
        }

        public Enemy Spawn(EnemyDef def, BulletPatternDef pattern, Sprite sprite, float x, float hoverY)
        {
            var e = _pool.Get();
            e.Init(def, pattern, sprite, x, hoverY);
            _active.Add(e);
            return e;
        }

        public void Despawn(Enemy e)
        {
            int i = _active.IndexOf(e);
            if (i < 0) return;
            _active[i] = _active[_active.Count - 1];
            _active.RemoveAt(_active.Count - 1);
            _pool.Release(e);
        }

        public void Tick(float dt, Vector2 playerPos)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var e = _active[i];
                e.Age += dt;
                Move(e, dt);

                if (e.Pos.y < BattleSpace.TowerLineY)
                {
                    Leaked?.Invoke(e.Def.damage);
                    Despawn(e);
                    continue;
                }

                if (e.Pattern != null && e.Pos.y < MaxShootY && e.Pos.y > MinShootY) Fire(e, dt, playerPos);

                if (e.FlashTimer > 0f)
                {
                    e.FlashTimer -= dt;
                    e.Renderer.color = e.FlashTimer > 0f ? new Color(1f, 0.45f, 0.45f) : Color.white;
                }
                e.transform.position = e.Pos;
            }
        }

        private static void Move(Enemy e, float dt)
        {
            float speed = BattleSpace.Speed(e.Def.speed);
            switch (e.Def.movement)
            {
                case "sine":
                    e.Pos.y -= speed * dt;
                    e.Pos.x = e.BaseX + Mathf.Sin(e.Age * 2.2f) * 1.2f;
                    break;
                case "zigzag":
                    e.Pos.y -= speed * dt;
                    e.Pos.x = e.BaseX + (Mathf.PingPong(e.Age * 1.2f, 2f) - 1f) * 1.2f;
                    break;
                case "dive":
                    e.Pos.y -= speed * (1f + e.Age * 0.6f) * dt;
                    break;
                case "hover":
                    bool holding = e.Pos.y <= e.HoverY && e.Age < HoverDuration;
                    if (!holding) e.Pos.y -= speed * dt;
                    e.Pos.x = e.BaseX + Mathf.Sin(e.Age * 0.7f) * 0.8f;
                    break;
                default: // straight
                    e.Pos.y -= speed * dt;
                    break;
            }
            e.Pos.x = Mathf.Clamp(e.Pos.x, -BattleSpace.HalfW + 0.3f, BattleSpace.HalfW - 0.3f);
        }

        private void Fire(Enemy e, float dt, Vector2 playerPos)
        {
            var p = e.Pattern;
            if (e.BurstLeft > 0)
            {
                e.BurstTimer -= dt;
                if (e.BurstTimer > 0f) return;
                e.BurstLeft--;
                e.BurstTimer = p.burstGap;
                Volley(e, playerPos);
                return;
            }
            e.ShotTimer -= dt;
            if (e.ShotTimer > 0f) return;
            e.ShotTimer = p.interval;
            e.BurstLeft = Mathf.Max(1, p.burst) - 1;
            e.BurstTimer = p.burstGap;
            Volley(e, playerPos);
        }

        private void Volley(Enemy e, Vector2 playerPos)
        {
            var p = e.Pattern;
            int n = Mathf.Max(1, p.bullets);
            float speed = BattleSpace.Speed(p.speed);
            float radius = BattleSpace.Radius(p.radius);
            var sprite = _bulletSprite(p.bulletSprite);
            float scale = 1f;

            for (int i = 0; i < n; i++)
            {
                float angle;
                switch (p.type)
                {
                    case "aimed":
                        angle = BattleSpace.AngleTo(e.Pos, playerPos) + Spread(i, n, p.spreadDeg);
                        break;
                    case "fan":
                        angle = Spread(i, n, p.spreadDeg);
                        break;
                    case "ring":
                    case "spiral":
                        angle = e.SpiralAngle + 360f * i / n;
                        break;
                    default: // random
                        angle = Random.Range(-p.spreadDeg * 0.5f, p.spreadDeg * 0.5f);
                        break;
                }
                _enemyBullets.Spawn(e.Pos, BattleSpace.Dir(angle) * speed, radius, sprite, Color.white, scale);
            }
            if (p.type == "spiral") e.SpiralAngle += p.rotatePerShotDeg;
            Shot?.Invoke(e);
        }

        private static float Spread(int i, int n, float spreadDeg) =>
            n <= 1 ? 0f : -spreadDeg * 0.5f + spreadDeg * i / (n - 1);

        public void Clear()
        {
            for (int i = _active.Count - 1; i >= 0; i--) _pool.Release(_active[i]);
            _active.Clear();
        }
    }
}

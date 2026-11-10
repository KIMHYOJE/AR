using System.Collections.Generic;
using System.Threading;
using AR.Audio;
using AR.Core;
using AR.UI;
using AR.VFX;
using TMPro;
using UnityEngine;

namespace AR.Battle
{
    /// <summary>
    /// Runs one stage from Assets/Resources/Stages/&lt;stageId&gt;.json: wave timeline, enemies, bullets, tower HP.
    /// RunAsync returns true on a win (all waves cleared) and false when the tower falls.
    /// Every battle builds its own root and tears it down at the end.
    /// </summary>
    public sealed class BattleController : MonoBehaviour
    {
        private struct SpawnEvent
        {
            public float Time;
            public EnemyDef Enemy;
            public BulletPatternDef Pattern;
            public float X;
            public float HoverY;
            public int Wave;
        }

        private const string SfxShoot = "SFX/shoot";
        private const string SfxHit = "SFX/hit";
        private const string SfxExplosion = "SFX/explosion";
        private const string SfxHurt = "SFX/player_hurt";
        private const string SfxEnemyShot = "SFX/enemy_shot";
        private const string PlayerHitVfx = "impacts/bullet-impact";

        private Camera _cam;
        private TMP_FontAsset _font;

        public void Configure(Camera cam, TMP_FontAsset font)
        {
            _cam = cam;
            _font = font;
        }

        public async Awaitable<bool> RunAsync(string stageId, CancellationToken ct)
        {
            var stage = StageData.Load(stageId);
            if (stage == null) return true; // missing data must not soft-lock the story

            var root = new GameObject($"Battle {stageId}").transform;
            try
            {
                return await RunStageAsync(stage, root, ct);
            }
            finally
            {
                if (root != null) Destroy(root.gameObject);
            }
        }

        private async Awaitable<bool> RunStageAsync(StageData stage, Transform root, CancellationToken ct)
        {
            BattleSpace.FitCamera(_cam);
            var audio = AudioManager.Instance;
            if (audio != null) _ = audio.SetBgmAsync(stage.bgm, ct);

            // --- scene ---
            var bgSprite = await AssetLoader.LoadAsync<Sprite>(stage.background, ct);
            var bg = new GameObject("Background").AddComponent<SpriteRenderer>();
            bg.transform.SetParent(root, false);
            bg.sortingOrder = -100;
            if (bgSprite != null)
            {
                bg.sprite = bgSprite;
                var size = bgSprite.bounds.size;
                float s = Mathf.Max(BattleSpace.Width / size.x, BattleSpace.Height / size.y);
                bg.transform.localScale = new Vector3(s, s, 1f);
                bg.color = new Color(0.45f, 0.45f, 0.55f); // darken so bullets read
            }

            var tower = new GameObject("TowerLine").AddComponent<SpriteRenderer>();
            tower.transform.SetParent(root, false);
            tower.sprite = Placeholder.White;
            tower.color = new Color(0.4f, 0.9f, 1f, 0.35f);
            tower.sortingOrder = -50;
            tower.transform.position = new Vector3(0, BattleSpace.TowerLineY, 0);
            tower.transform.localScale = new Vector3(BattleSpace.Width, 0.08f, 1);

            var playerBullets = new BulletSystem(root, "PlayerBullet", 15, 128);
            var enemyBullets = new BulletSystem(root, "EnemyBullet", 30, 512);
            var sprites = new Dictionary<string, Sprite>();
            foreach (var p in stage.patterns) await CacheSprite(sprites, p.bulletSprite, ct);
            foreach (var e in stage.enemies) await CacheSprite(sprites, e.sprite, ct);
            Sprite BulletSprite(string address) =>
                !string.IsNullOrEmpty(address) && sprites.TryGetValue(address, out var s) && s != null ? s : Placeholder.Circle(8);

            var enemies = new EnemySystem(root, enemyBullets, BulletSprite);
            var vfx = new VfxSystem(root, 40);
            await vfx.PreloadAsync(PlayerHitVfx, ct);
            foreach (var e in stage.enemies) await vfx.PreloadAsync(e.deathVfx, ct);

            var playerGo = new GameObject("Player", typeof(SpriteRenderer), typeof(PlayerUnit));
            playerGo.transform.SetParent(root, false);
            var player = playerGo.GetComponent<PlayerUnit>();
            var playerSprite = await AssetLoader.LoadAsync<Sprite>("Player/aria_unit", ct);
            player.Init(_cam, playerSprite != null ? playerSprite : Placeholder.Circle(16));
            var playerBulletSprite = await AssetLoader.LoadAsync<Sprite>("Bullets/orb_cyan", ct) ?? Placeholder.Circle(6);

            // --- state ---
            int towerHp = Mathf.Max(1, stage.baseHp);
            int score = 0;
            enemies.Leaked += dmg =>
            {
                towerHp -= dmg;
                audio?.PlaySfx(SfxHurt);
            };
            enemies.Shot += _ => audio?.PlaySfx(SfxEnemyShot);

            var timeline = BuildTimeline(stage);
            int next = 0;
            int currentWave = 0;

            var hud = BuildHud(root);
            hud.banner.text = stage.title;
            float bannerTimer = 2f;

            float time = 0f;
            float endDelay = -1f;
            while (true)
            {
                await Awaitable.NextFrameAsync(ct);
                float dt = Mathf.Min(Time.deltaTime, 1f / 20f);
                time += dt;
                BattleSpace.FitCamera(_cam);

                while (next < timeline.Count && timeline[next].Time <= time)
                {
                    var ev = timeline[next++];
                    if (ev.Wave != currentWave)
                    {
                        currentWave = ev.Wave;
                        hud.banner.text = $"WAVE {currentWave}";
                        bannerTimer = 1.4f;
                    }
                    sprites.TryGetValue(ev.Enemy.sprite ?? "", out var es);
                    enemies.Spawn(ev.Enemy, ev.Pattern, es != null ? es : Placeholder.Circle(20), ev.X, ev.HoverY);
                }

                player.Tick(dt, playerBullets, playerBulletSprite);
                playerBullets.Tick(dt);
                enemyBullets.Tick(dt);
                enemies.Tick(dt, player.Position);
                vfx.Tick(dt);

                // player bullets vs enemies
                var active = enemies.Active;
                for (int b = playerBullets.Count - 1; b >= 0; b--)
                {
                    var bp = playerBullets.PositionAt(b);
                    float br = playerBullets.RadiusAt(b);
                    for (int i = active.Count - 1; i >= 0; i--)
                    {
                        var e = active[i];
                        float r = br + e.Radius;
                        if ((e.Pos - bp).sqrMagnitude > r * r) continue;
                        playerBullets.RemoveAt(b);
                        e.Hp--;
                        e.FlashTimer = 0.05f;
                        audio?.PlaySfx(SfxHit);
                        if (e.Hp <= 0)
                        {
                            score += e.Def.score;
                            vfx.Play(e.Def.deathVfx, e.Pos);
                            audio?.PlaySfx(SfxExplosion);
                            enemies.Despawn(e);
                        }
                        break;
                    }
                }

                // enemy bullets vs player
                if (!player.Invulnerable)
                {
                    var pp = player.Position;
                    for (int b = enemyBullets.Count - 1; b >= 0; b--)
                    {
                        float r = enemyBullets.RadiusAt(b) + PlayerUnit.HitRadius;
                        if ((enemyBullets.PositionAt(b) - pp).sqrMagnitude > r * r) continue;
                        enemyBullets.RemoveAt(b);
                        towerHp -= 1; // Aria's shield is linked to the tower
                        player.OnHit();
                        vfx.Play(PlayerHitVfx, pp);
                        audio?.PlaySfx(SfxHurt);
                        break;
                    }
                }

                if (bannerTimer > 0f)
                {
                    bannerTimer -= dt;
                    hud.banner.alpha = Mathf.Clamp01(bannerTimer * 2f);
                }
                hud.status.text = $"WAVE {Mathf.Max(1, currentWave)}/{stage.waves.Count}    TOWER {Mathf.Max(0, towerHp)}/{stage.baseHp}    SCORE {score}";

                if (towerHp <= 0)
                {
                    enemyBullets.Clear();
                    await ShowResultAsync(hud.banner, "TOWER FALLEN", new Color(1f, 0.4f, 0.4f), ct);
                    return false;
                }

                if (next >= timeline.Count && active.Count == 0)
                {
                    if (endDelay < 0f) endDelay = 1f;
                    endDelay -= dt;
                    if (endDelay <= 0f)
                    {
                        enemyBullets.Clear();
                        await ShowResultAsync(hud.banner, "DEFENSE SUCCESS", new Color(0.5f, 1f, 0.8f), ct);
                        return true;
                    }
                }
            }
        }

        private static async Awaitable CacheSprite(Dictionary<string, Sprite> cache, string address, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(address) || cache.ContainsKey(address)) return;
            cache[address] = await AssetLoader.LoadAsync<Sprite>(address, ct);
        }

        private static List<SpawnEvent> BuildTimeline(StageData stage)
        {
            var list = new List<SpawnEvent>();
            for (int w = 0; w < stage.waves.Count; w++)
            {
                var wave = stage.waves[w];
                foreach (var sp in wave.spawns)
                {
                    var def = stage.FindEnemy(sp.enemyId);
                    if (def == null)
                    {
                        Debug.LogError($"[Battle] {stage.stageId}: unknown enemy '{sp.enemyId}'");
                        continue;
                    }
                    var pattern = stage.FindPattern(sp.pattern);
                    for (int k = 0; k < sp.count; k++)
                    {
                        list.Add(new SpawnEvent
                        {
                            Time = wave.startTime + k * sp.interval,
                            Enemy = def,
                            Pattern = pattern,
                            X = BattleSpace.X(Mathf.Clamp(FormationX(sp, k), 0.05f, 0.95f)),
                            HoverY = sp.hoverY,
                            Wave = w + 1,
                        });
                    }
                }
            }
            list.Sort((a, b) => a.Time.CompareTo(b.Time));
            return list;
        }

        private static float FormationX(SpawnDef sp, int k)
        {
            float t = sp.count <= 1 ? 0.5f : (float)k / (sp.count - 1);
            switch (sp.formation)
            {
                case "line": return sp.x - sp.width * 0.5f + sp.width * t;
                case "v": return sp.x + (t - 0.5f) * sp.width * (k % 2 == 0 ? 1f : -1f);
                case "random": return sp.x + Random.Range(-0.5f, 0.5f) * sp.width;
                default: return sp.x; // column
            }
        }

        private (TextMeshProUGUI status, TextMeshProUGUI banner) BuildHud(Transform root)
        {
            var canvas = UIFactory.Canvas("Battle HUD", 10, root);
            var safe = UIFactory.Stretch(canvas.transform, "SafeArea");
            safe.gameObject.AddComponent<SafeAreaFitter>();
            var status = UIFactory.Text(safe, "Status", _font, 38, Color.white, TextAlignmentOptions.Top,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -90), new Vector2(-20, -20));
            var banner = UIFactory.Text(safe, "Banner", _font, 84, Color.white, TextAlignmentOptions.Center,
                new Vector2(0, 0.45f), new Vector2(1, 0.6f));
            return (status, banner);
        }

        private static async Awaitable ShowResultAsync(TextMeshProUGUI banner, string text, Color color, CancellationToken ct)
        {
            banner.text = text;
            banner.color = color;
            banner.alpha = 1f;
            await Awaitable.WaitForSecondsAsync(1.8f, ct);
        }
    }
}

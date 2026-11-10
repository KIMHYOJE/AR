using UnityEngine;

namespace AR.Battle
{
    /// <summary>
    /// One pooled enemy. Holds state only; EnemySystem moves it and fires its pattern in one loop
    /// (no Update here, so thousands of enemies cost one call).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class Enemy : MonoBehaviour
    {
        public SpriteRenderer Renderer { get; private set; }

        public EnemyDef Def;
        public BulletPatternDef Pattern;
        public Vector2 Pos;
        public float BaseX;
        public float HoverY;
        public float Age;
        public int Hp;
        public float ShotTimer;
        public int BurstLeft;
        public float BurstTimer;
        public float SpiralAngle;
        public float FlashTimer;
        public float Radius;

        private void Awake() => Renderer = GetComponent<SpriteRenderer>();

        public void Init(EnemyDef def, BulletPatternDef pattern, Sprite sprite, float x, float hoverNormalizedY)
        {
            Def = def;
            Pattern = pattern;
            Hp = def.hp;
            Radius = BattleSpace.Radius(def.radius);
            BaseX = x;
            Pos = new Vector2(x, BattleSpace.HalfH + 0.8f);
            HoverY = hoverNormalizedY > 0f ? BattleSpace.Y(hoverNormalizedY) : float.NegativeInfinity;
            Age = 0f;
            ShotTimer = pattern != null ? pattern.interval * Random.Range(0.4f, 1f) : 0f;
            BurstLeft = 0;
            BurstTimer = 0f;
            SpiralAngle = 0f;
            FlashTimer = 0f;
            Renderer.sprite = sprite;
            Renderer.color = Color.white;
            transform.position = Pos;
        }
    }
}

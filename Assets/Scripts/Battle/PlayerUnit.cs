using AR.Core;
using UnityEngine;

namespace AR.Battle
{
    /// <summary>Aria's unit: keyboard/gamepad move or relative touch/mouse drag, always auto-firing upward.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PlayerUnit : MonoBehaviour
    {
        public const float HitRadius = 0.14f;
        private const float KeySpeed = 9f;
        private const float FireInterval = 0.09f;
        private const float BulletSpeed = 20f;
        private const float InvulnTime = 1.6f;

        private SpriteRenderer _renderer;
        private Camera _cam;
        private bool _dragging;
        private Vector2 _lastPointerWorld;
        private float _fireTimer;
        private float _invuln;

        public Vector2 Position { get; private set; }
        public bool Invulnerable => _invuln > 0f;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _renderer.sortingOrder = 20;
        }

        public void Init(Camera cam, Sprite sprite)
        {
            _cam = cam;
            _renderer.sprite = sprite;
            Position = new Vector2(0f, -5.2f);
            transform.position = Position;
            _invuln = 0f;
        }

        public void Tick(float dt, BulletSystem playerBullets, Sprite bulletSprite)
        {
            var pos = Position;
            pos += GameInput.Move.ReadValue<Vector2>() * (KeySpeed * dt);

            if (GameInput.IsPointerDown && _cam != null)
            {
                Vector2 world = _cam.ScreenToWorldPoint(GameInput.PointerScreen);
                if (_dragging) pos += world - _lastPointerWorld;
                _lastPointerWorld = world;
                _dragging = true;
            }
            else
            {
                _dragging = false;
            }

            pos.x = Mathf.Clamp(pos.x, -BattleSpace.HalfW + 0.4f, BattleSpace.HalfW - 0.4f);
            pos.y = Mathf.Clamp(pos.y, BattleSpace.TowerLineY + 0.8f, 2f);
            Position = pos;
            transform.position = pos;

            _fireTimer -= dt;
            if (_fireTimer <= 0f)
            {
                _fireTimer = FireInterval;
                var v = new Vector2(0f, BulletSpeed);
                var tint = new Color(0.55f, 0.95f, 1f);
                playerBullets.Spawn(pos + new Vector2(-0.22f, 0.5f), v, 0.18f, bulletSprite, tint, 1f);
                playerBullets.Spawn(pos + new Vector2(0.22f, 0.5f), v, 0.18f, bulletSprite, tint, 1f);
            }

            if (_invuln > 0f)
            {
                _invuln -= dt;
                _renderer.enabled = _invuln <= 0f || Mathf.Repeat(_invuln, 0.16f) > 0.08f;
            }
        }

        public void OnHit()
        {
            _invuln = InvulnTime;
        }
    }
}

using UnityEngine;

namespace AR.Battle
{
    /// <summary>
    /// The 9x16-unit play column. Stage JSON uses normalized coordinates (x 0..1 left→right, y 0..1 bottom→top),
    /// speeds in column-heights per second and angles in degrees (0 = straight down, positive = clockwise).
    /// </summary>
    public static class BattleSpace
    {
        public const float Width = 9f;
        public const float Height = 16f;
        public const float HalfW = Width * 0.5f;
        public const float HalfH = Height * 0.5f;
        public const float TowerLineY = -HalfH + 0.6f;

        public static float X(float nx) => (nx - 0.5f) * Width;
        public static float Y(float ny) => (ny - 0.5f) * Height;
        public static float Speed(float s) => s * Height;
        public static float Radius(float r) => r * Width;

        /// <summary>Direction for an angle in degrees, 0 = down, positive = clockwise on screen.</summary>
        public static Vector2 Dir(float deg)
        {
            float r = deg * Mathf.Deg2Rad;
            return new Vector2(-Mathf.Sin(r), -Mathf.Cos(r));
        }

        /// <summary>Angle (same convention) pointing from 'from' to 'to'.</summary>
        public static float AngleTo(Vector2 from, Vector2 to)
        {
            var d = to - from;
            return Mathf.Atan2(-d.x, -d.y) * Mathf.Rad2Deg;
        }

        /// <summary>Fit the camera so the whole column is visible at any aspect ratio.</summary>
        public static void FitCamera(Camera cam)
        {
            if (cam == null) return;
            cam.orthographic = true;
            cam.orthographicSize = Mathf.Max(HalfH, HalfW / Mathf.Max(0.01f, cam.aspect));
            cam.transform.position = new Vector3(0, 0, -10);
        }

        public static bool Outside(Vector2 p, float margin) =>
            p.x < -HalfW - margin || p.x > HalfW + margin || p.y < -HalfH - margin || p.y > HalfH + margin;
    }
}

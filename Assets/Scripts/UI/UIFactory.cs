using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AR.UI
{
    /// <summary>Builds uGUI hierarchies in code so screens need no Inspector wiring.</summary>
    public static class UIFactory
    {
        public static readonly Vector2 ReferenceResolution = new(1080, 1920);

        public static Canvas Canvas(string name, int sortingOrder, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            if (parent != null) go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f; // portrait: lock to height, PC window keeps a 9:16 feel
            return canvas;
        }

        public static RectTransform Rect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin = default, Vector2 offsetMax = default)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return rt;
        }

        public static RectTransform Stretch(Transform parent, string name) => Rect(parent, name, Vector2.zero, Vector2.one);

        public static Image Image(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax,
            bool raycast = false)
        {
            var rt = Rect(parent, name, anchorMin, anchorMax);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, TMP_FontAsset font, float size, Color color,
            TextAlignmentOptions align, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin = default,
            Vector2 offsetMax = default)
        {
            var rt = Rect(parent, name, anchorMin, anchorMax, offsetMin, offsetMax);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }

        public static Button Button(Transform parent, string name, TMP_FontAsset font, string label, float fontSize,
            Color bg, Color fg)
        {
            var rt = Rect(parent, name, Vector2.zero, Vector2.one);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = bg;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var t = Text(rt, "Label", font, fontSize, fg, TextAlignmentOptions.Center, Vector2.zero, Vector2.one,
                new Vector2(24, 8), new Vector2(-24, -8));
            t.text = label;
            return btn;
        }
    }
}

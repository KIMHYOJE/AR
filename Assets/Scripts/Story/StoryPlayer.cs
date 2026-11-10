using System.Collections.Generic;
using System.Threading;
using AR.Audio;
using AR.Core;
using AR.Pooling;
using AR.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AR.Story
{
    /// <summary>
    /// Plays StoryChapter nodes: background, speaker sprite, typewriter text, choices, flags, screen effects.
    /// PlayAsync returns the node that hands control back to GameFlow (a battle or the end).
    /// The whole UI is built in code (no prefab / Inspector wiring).
    /// </summary>
    public sealed class StoryPlayer : MonoBehaviour
    {
        private const float CharsPerSecond = 38f;
        private const string BlipSfx = "SFX/text_blip";
        private const string ChoiceSfx = "SFX/ui_confirm";

        private Canvas _canvas;
        private RectTransform _shakeRoot;
        private Image _background;
        private Image _character;
        private Image _overlay;
        private RectTransform _dialogueBox;
        private TextMeshProUGUI _name;
        private TextMeshProUGUI _body;
        private TextMeshProUGUI _nextMarker;
        private RectTransform _choiceRoot;
        private TMP_FontAsset _font;
        private ComponentPool<Button> _choicePool;
        private readonly List<Button> _activeChoices = new();

        private bool _advanceRequested;
        private int _choiceResult = -1;
        private string _currentCharacter = "";

        public void Build(TMP_FontAsset font)
        {
            _font = font;
            _canvas = UIFactory.Canvas("VN Canvas", 0, transform);
            var root = _canvas.transform;

            _shakeRoot = UIFactory.Stretch(root, "ShakeRoot");

            // Background fills the screen while keeping 9:16.
            var bgHolder = UIFactory.Stretch(_shakeRoot, "Background");
            _background = bgHolder.gameObject.AddComponent<Image>();
            _background.color = new Color(0.05f, 0.06f, 0.1f);
            _background.raycastTarget = false;
            var fitter = bgHolder.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = 9f / 16f;

            _character = UIFactory.Image(_shakeRoot, "Character", Color.white, new Vector2(0.05f, 0.18f),
                new Vector2(0.95f, 0.92f));
            _character.preserveAspect = true;
            _character.enabled = false;

            // Tap anywhere to advance (choices sit on top of this).
            var advance = UIFactory.Image(_shakeRoot, "AdvanceArea", new Color(0, 0, 0, 0), Vector2.zero, Vector2.one, true);
            advance.gameObject.AddComponent<Button>().onClick.AddListener(() => _advanceRequested = true);

            var safe = UIFactory.Stretch(_shakeRoot, "SafeArea");
            safe.gameObject.AddComponent<SafeAreaFitter>();

            _dialogueBox = UIFactory.Rect(safe, "DialogueBox", new Vector2(0.03f, 0.02f), new Vector2(0.97f, 0.27f));
            var boxImg = _dialogueBox.gameObject.AddComponent<Image>();
            boxImg.color = new Color(0.03f, 0.04f, 0.08f, 0.86f);
            boxImg.raycastTarget = false;

            _name = UIFactory.Text(_dialogueBox, "Name", font, 46, new Color(0.55f, 0.85f, 1f), TextAlignmentOptions.TopLeft,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(40, -86), new Vector2(-40, -20));
            _body = UIFactory.Text(_dialogueBox, "Body", font, 44, Color.white, TextAlignmentOptions.TopLeft,
                Vector2.zero, Vector2.one, new Vector2(40, 40), new Vector2(-40, -96));
            _nextMarker = UIFactory.Text(_dialogueBox, "Next", font, 40, new Color(1, 1, 1, 0.8f), TextAlignmentOptions.BottomRight,
                new Vector2(1, 0), new Vector2(1, 0), new Vector2(-90, 20), new Vector2(-30, 80));
            _nextMarker.text = "▼";

            _choiceRoot = UIFactory.Rect(safe, "Choices", new Vector2(0.08f, 0.32f), new Vector2(0.92f, 0.72f));
            var layout = _choiceRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 36;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            _choicePool = new ComponentPool<Button>(CreateChoiceButton, 3, 8);

            _overlay = UIFactory.Image(root, "Overlay", new Color(0, 0, 0, 0), Vector2.zero, Vector2.one);
        }

        private Button CreateChoiceButton()
        {
            var btn = UIFactory.Button(_choiceRoot, "Choice", _font, "", 42, new Color(0.08f, 0.12f, 0.22f, 0.95f), Color.white);
            var le = btn.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 150;
            btn.gameObject.SetActive(false);
            return btn;
        }

        public void SetVisible(bool visible) => _canvas.enabled = visible;

        private void Update()
        {
            if (GameInput.Advance != null && GameInput.Advance.WasPressedThisFrame()) _advanceRequested = true;
        }

        public async Awaitable<StoryNode> PlayAsync(StoryChapter chapter, string nodeId, CancellationToken ct)
        {
            var chars = new Dictionary<string, StoryCharacter>();
            foreach (var c in chapter.characters) chars[c.id] = c;

            var node = chapter.GetNode(nodeId);
            while (node != null)
            {
                ct.ThrowIfCancellationRequested();
                await ApplyPresentationAsync(node, chars, ct);

                switch (node.type)
                {
                    case "dialogue":
                    case "narration":
                        await ShowLineAsync(node, chars, ct);
                        node = chapter.GetNode(node.next);
                        break;
                    case "choice":
                        node = chapter.GetNode(await ChooseAsync(node, ct));
                        break;
                    case "jump":
                        node = chapter.GetNode(GameState.HasFlag(node.requireFlag) ? node.next : node.elseNext);
                        break;
                    case "battle":
                        return node;
                    case "end":
                        await ShowLineAsync(node, chars, ct);
                        return node;
                    default:
                        Debug.LogError($"[Story] unknown node type '{node.type}' at {node.id}", this);
                        return node;
                }
            }
            Debug.LogError($"[Story] dangling node reference after '{nodeId}' (run Tools/validate_story.py)", this);
            return null;
        }

        private async Awaitable ApplyPresentationAsync(StoryNode node, Dictionary<string, StoryCharacter> chars,
            CancellationToken ct)
        {
            if (AudioManager.Instance != null)
            {
                _ = AudioManager.Instance.SetBgmAsync(node.bgm, ct);
                AudioManager.Instance.PlaySfx(node.sfx, 0f);
            }

            if (!string.IsNullOrEmpty(node.background))
            {
                var bg = await AssetLoader.LoadAsync<Sprite>(node.background, ct);
                _background.sprite = bg;
                _background.color = bg != null ? Color.white : new Color(0.05f, 0.06f, 0.1f);
            }

            if (!string.IsNullOrEmpty(node.speaker) && chars.TryGetValue(node.speaker, out var ch) &&
                !string.IsNullOrEmpty(ch.sprite))
            {
                string expr = string.IsNullOrEmpty(node.expression) ? "neutral" : node.expression;
                var sprite = await AssetLoader.LoadAsync<Sprite>($"{ch.sprite}_{expr}", ct)
                             ?? await AssetLoader.LoadAsync<Sprite>($"{ch.sprite}_neutral", ct);
                if (sprite != null)
                {
                    _character.sprite = sprite;
                    _character.enabled = true;
                    _character.color = Color.white;
                    _currentCharacter = node.speaker;
                }
                else if (_currentCharacter != node.speaker)
                {
                    _character.enabled = false;
                }
            }
            else if (_character.enabled)
            {
                _character.color = new Color(0.55f, 0.55f, 0.6f); // narration / unseen speaker: dim the last one
            }

            if (!string.IsNullOrEmpty(node.effect)) _ = PlayEffectAsync(node.effect, ct);
        }

        private async Awaitable ShowLineAsync(StoryNode node, Dictionary<string, StoryCharacter> chars, CancellationToken ct)
        {
            _dialogueBox.gameObject.SetActive(true);
            bool narration = string.IsNullOrEmpty(node.speaker);
            _name.text = narration ? "" : chars.TryGetValue(node.speaker, out var c) ? c.name : node.speaker;
            _body.fontStyle = narration ? FontStyles.Italic : FontStyles.Normal;
            _body.color = narration ? new Color(0.85f, 0.88f, 0.95f) : Color.white;
            _body.text = node.text;
            _body.maxVisibleCharacters = 0;
            _body.ForceMeshUpdate();
            int total = _body.textInfo.characterCount;
            _nextMarker.enabled = false;

            _advanceRequested = false;
            float shown = 0f;
            int lastBlip = 0;
            while (shown < total)
            {
                ct.ThrowIfCancellationRequested();
                if (_advanceRequested)
                {
                    _advanceRequested = false;
                    break;
                }
                shown += CharsPerSecond * Time.deltaTime;
                int visible = Mathf.Min(total, (int)shown);
                _body.maxVisibleCharacters = visible;
                if (!narration && visible - lastBlip >= 2 && AudioManager.Instance != null)
                {
                    lastBlip = visible;
                    AudioManager.Instance.PlaySfx(BlipSfx, 0.08f);
                }
                await Awaitable.NextFrameAsync(ct);
            }
            _body.maxVisibleCharacters = total;
            _nextMarker.enabled = true;

            while (!_advanceRequested)
            {
                _nextMarker.alpha = 0.4f + 0.6f * Mathf.PingPong(Time.time * 2f, 1f);
                await Awaitable.NextFrameAsync(ct);
            }
            _advanceRequested = false;
        }

        private async Awaitable<string> ChooseAsync(StoryNode node, CancellationToken ct)
        {
            if (!string.IsNullOrEmpty(node.text))
            {
                _name.text = "";
                _body.fontStyle = FontStyles.Normal;
                _body.text = node.text;
                _body.maxVisibleCharacters = int.MaxValue;
                _nextMarker.enabled = false;
            }

            _choiceResult = -1;
            for (int i = 0; i < node.choices.Count; i++)
            {
                int index = i;
                var btn = _choicePool.Get();
                btn.transform.SetAsLastSibling();
                btn.GetComponentInChildren<TextMeshProUGUI>().text = node.choices[i].text;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => _choiceResult = index);
                _activeChoices.Add(btn);
            }

            while (_choiceResult < 0) await Awaitable.NextFrameAsync(ct);

            foreach (var b in _activeChoices) _choicePool.Release(b);
            _activeChoices.Clear();
            _advanceRequested = false;

            var picked = node.choices[_choiceResult];
            GameState.SetFlag(picked.setFlag);
            GameState.AddAffinity(picked.affinityTarget, picked.affinity);
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(ChoiceSfx, 0f);
            return picked.next;
        }

        private async Awaitable PlayEffectAsync(string effect, CancellationToken ct)
        {
            try
            {
                switch (effect)
                {
                    case "shake": await ShakeAsync(0.35f, 28f, ct); break;
                    case "flash": await FadeOverlayAsync(Color.white, 0.9f, 0f, 0.45f, ct); break;
                    case "fade_black": await FadeOverlayAsync(Color.black, 1f, 0f, 0.9f, ct); break;
                    case "fade_white": await FadeOverlayAsync(Color.white, 1f, 0f, 0.9f, ct); break;
                    case "glitch": await GlitchAsync(0.5f, ct); break;
                }
            }
            catch (System.OperationCanceledException) { }
        }

        private async Awaitable ShakeAsync(float duration, float strength, CancellationToken ct)
        {
            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                float k = 1f - t / duration;
                _shakeRoot.anchoredPosition = Random.insideUnitCircle * strength * k;
                await Awaitable.NextFrameAsync(ct);
            }
            _shakeRoot.anchoredPosition = Vector2.zero;
        }

        private async Awaitable FadeOverlayAsync(Color color, float from, float to, float duration, CancellationToken ct)
        {
            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                color.a = Mathf.Lerp(from, to, t / duration);
                _overlay.color = color;
                await Awaitable.NextFrameAsync(ct);
            }
            color.a = to;
            _overlay.color = color;
        }

        private async Awaitable GlitchAsync(float duration, CancellationToken ct)
        {
            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                bool on = Random.value < 0.5f;
                _shakeRoot.anchoredPosition = on ? new Vector2(Random.Range(-40f, 40f), 0) : Vector2.zero;
                _character.color = on ? new Color(0.6f, 1f, 1f, 0.7f) : Color.white;
                await Awaitable.WaitForSecondsAsync(0.04f, ct);
            }
            _shakeRoot.anchoredPosition = Vector2.zero;
            _character.color = Color.white;
        }
    }
}

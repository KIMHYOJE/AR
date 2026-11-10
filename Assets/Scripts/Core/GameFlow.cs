using AR.Audio;
using AR.Battle;
using AR.Story;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace AR.Core
{
    /// <summary>
    /// The only component in Main.unity. Builds every system in code and runs the loop:
    /// story → battle (stageId from the story node) → story (winNext / loseNext) → … → end → restart.
    /// </summary>
    public sealed class GameFlow : MonoBehaviour
    {
        [SerializeField] private string _chapterResource = "Stories/Chapter1";
        [SerializeField] private string _fontResource = "Fonts/MainFont";

        private StoryPlayer _story;
        private BattleController _battle;

        private async Awaitable Start()
        {
            Application.targetFrameRate = 60;
            GameInput.Init();

            var cam = Camera.main;
            if (cam == null)
            {
                cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
                cam.tag = "MainCamera";
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.02f, 0.05f);
            BattleSpace.FitCamera(cam);

            if (FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            if (AudioManager.Instance == null) new GameObject("Audio", typeof(AudioManager));

            var font = Resources.Load<TMP_FontAsset>(_fontResource);
            if (font == null) Debug.LogWarning($"[GameFlow] Korean font missing at Resources/{_fontResource} (run AR/Setup Project)", this);

            _story = new GameObject("Story", typeof(StoryPlayer)).GetComponent<StoryPlayer>();
            _story.transform.SetParent(transform, false);
            _story.Build(font);
            _battle = gameObject.AddComponent<BattleController>();
            _battle.Configure(cam, font);

            var ct = destroyCancellationToken;
            try
            {
                while (true)
                {
                    var chapter = StoryChapter.Load(_chapterResource);
                    if (chapter == null) return;
                    GameState.Reset();
                    string nodeId = chapter.startNodeId;
                    while (true)
                    {
                        var stop = await _story.PlayAsync(chapter, nodeId, ct);
                        if (stop == null || stop.type != "battle") break;

                        _story.SetVisible(false);
                        bool win = await _battle.RunAsync(stop.battle.stageId, ct);
                        _story.SetVisible(true);
                        nodeId = win ? stop.battle.winNext : stop.battle.loseNext;
                    }
                    // chapter finished: loop back to the start (later: next chapter / title screen)
                }
            }
            catch (System.OperationCanceledException) { }
        }
    }
}

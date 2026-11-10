using System;
using System.Collections.Generic;
using UnityEngine;

namespace AR.Story
{
    /// <summary>Data model for Assets/Resources/Stories/*.json (see .claude/skills/story-generator).</summary>
    [Serializable]
    public class StoryChapter
    {
        public string chapterId;
        public string title;
        public string startNodeId;
        public List<StoryCharacter> characters = new();
        public List<StoryNode> nodes = new();

        [NonSerialized] private Dictionary<string, StoryNode> _lookup;

        public StoryNode GetNode(string id)
        {
            if (_lookup == null)
            {
                _lookup = new Dictionary<string, StoryNode>(nodes.Count);
                foreach (var n in nodes) _lookup[n.id] = n;
            }
            return _lookup.TryGetValue(id, out var node) ? node : null;
        }

        public static StoryChapter Load(string resourcePath)
        {
            var asset = Resources.Load<TextAsset>(resourcePath);
            if (asset == null)
            {
                Debug.LogError($"Story not found: Resources/{resourcePath}");
                return null;
            }
            return JsonUtility.FromJson<StoryChapter>(asset.text);
        }
    }

    [Serializable]
    public class StoryCharacter
    {
        public string id;
        public string name;
        public string sprite;      // Addressables address prefix; full address = sprite + "_" + expression
    }

    [Serializable]
    public class StoryNode
    {
        public string id;
        public string type;        // dialogue | narration | choice | battle | jump | end
        public string speaker;
        public string expression;
        public string background;
        public string bgm;
        public string sfx;
        public string effect;
        public string text;
        public string next;
        public string requireFlag;
        public string elseNext;
        public List<StoryChoice> choices = new();
        public StoryBattle battle = new();
    }

    [Serializable]
    public class StoryChoice
    {
        public string text;
        public string next;
        public string setFlag;
        public string affinityTarget;
        public int affinity;
    }

    [Serializable]
    public class StoryBattle
    {
        public string stageId;
        public int waveCount;
        public string winNext;
        public string loseNext;
    }
}

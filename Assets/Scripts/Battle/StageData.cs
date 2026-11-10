using System;
using System.Collections.Generic;
using UnityEngine;

namespace AR.Battle
{
    /// <summary>Data model for Assets/Resources/Stages/*.json (see .claude/skills/stage-designer).</summary>
    [Serializable]
    public class StageData
    {
        public string stageId;
        public string title;
        public string background;
        public string bgm;
        public int baseHp;
        public List<EnemyDef> enemies = new();
        public List<BulletPatternDef> patterns = new();
        public List<WaveDef> waves = new();

        public static StageData Load(string stageId)
        {
            var asset = Resources.Load<TextAsset>($"Stages/{stageId}");
            if (asset == null)
            {
                Debug.LogError($"Stage not found: Resources/Stages/{stageId}");
                return null;
            }
            return JsonUtility.FromJson<StageData>(asset.text);
        }

        public EnemyDef FindEnemy(string id) => enemies.Find(e => e.id == id);
        public BulletPatternDef FindPattern(string id) => string.IsNullOrEmpty(id) ? null : patterns.Find(p => p.id == id);
    }

    [Serializable]
    public class EnemyDef
    {
        public string id;
        public int hp;
        public float speed;
        public int damage;
        public int score;
        public float radius;
        public string movement;   // straight | sine | zigzag | dive | hover
        public string sprite;
        public string deathVfx;
    }

    [Serializable]
    public class BulletPatternDef
    {
        public string id;
        public string type;       // aimed | fan | ring | spiral | random
        public int bullets;
        public float spreadDeg;
        public float speed;
        public float interval;
        public int burst;
        public float burstGap;
        public float rotatePerShotDeg;
        public string bulletSprite;
        public float radius;
    }

    [Serializable]
    public class WaveDef
    {
        public float startTime;
        public List<SpawnDef> spawns = new();
    }

    [Serializable]
    public class SpawnDef
    {
        public string enemyId;
        public int count;
        public float interval;
        public string formation;  // line | column | v | random
        public float x;
        public float width;
        public string pattern;
        public float hoverY;
    }
}

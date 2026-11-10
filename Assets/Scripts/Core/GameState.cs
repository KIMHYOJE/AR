using System.Collections.Generic;

namespace AR.Core
{
    /// <summary>Run-wide story state: flags and per-character affinity set by story choices.</summary>
    public static class GameState
    {
        private static readonly HashSet<string> _flags = new();
        private static readonly Dictionary<string, int> _affinity = new();

        public static void SetFlag(string flag)
        {
            if (!string.IsNullOrEmpty(flag)) _flags.Add(flag);
        }

        public static bool HasFlag(string flag) => !string.IsNullOrEmpty(flag) && _flags.Contains(flag);

        public static void AddAffinity(string characterId, int amount)
        {
            if (string.IsNullOrEmpty(characterId) || amount == 0) return;
            _affinity.TryGetValue(characterId, out var cur);
            _affinity[characterId] = cur + amount;
        }

        public static int GetAffinity(string characterId) =>
            _affinity.TryGetValue(characterId, out var v) ? v : 0;

        public static void Reset()
        {
            _flags.Clear();
            _affinity.Clear();
        }
    }
}

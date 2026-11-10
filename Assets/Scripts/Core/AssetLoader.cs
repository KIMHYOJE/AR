using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace AR.Core
{
    /// <summary>
    /// Loads assets by the path-style addresses used in story/stage JSON ("Characters/aria_smile", "SFX/crash").
    /// Addresses are registered automatically by AR.Editor.AddressablesSync. A missing address resolves to null and
    /// never throws, so the game keeps running with placeholders while art is still being made.
    /// </summary>
    public static class AssetLoader
    {
        private static readonly Dictionary<string, Object> _cache = new();
        private static readonly HashSet<string> _missing = new();

        public static async Awaitable<T> LoadAsync<T>(string address, CancellationToken ct = default) where T : Object
        {
            if (string.IsNullOrEmpty(address)) return null;
            string key = typeof(T).Name + ":" + address;
            if (_cache.TryGetValue(key, out var cached)) return cached as T;
            if (_missing.Contains(key)) return null;

            var locations = Addressables.LoadResourceLocationsAsync(address, typeof(T));
            await locations.Task;
            bool found = locations.Status == AsyncOperationStatus.Succeeded && locations.Result.Count > 0;
            Addressables.Release(locations);
            ct.ThrowIfCancellationRequested();
            if (!found)
            {
                _missing.Add(key);
                Debug.LogWarning($"[AssetLoader] missing {typeof(T).Name} '{address}' (run Tools/sync_story_assets.py)");
                return null;
            }

            var handle = Addressables.LoadAssetAsync<T>(address);
            await handle.Task;
            ct.ThrowIfCancellationRequested();
            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                _missing.Add(key);
                return null;
            }
            _cache[key] = handle.Result;
            return handle.Result;
        }
    }
}

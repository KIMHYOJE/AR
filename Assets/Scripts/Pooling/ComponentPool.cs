using System;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace AR.Pooling
{
    /// <summary>
    /// ObjectPool for components whose GameObjects are built in code (no prefab needed).
    /// Get() activates, Release() deactivates; pooled objects never Destroy themselves.
    /// </summary>
    public sealed class ComponentPool<T> where T : Component
    {
        private readonly ObjectPool<T> _pool;

        public int CountActive => _pool.CountActive;

        public ComponentPool(Func<T> create, int defaultCapacity = 64, int maxSize = 4096)
        {
            _pool = new ObjectPool<T>(
                createFunc: create,
                actionOnGet: c => c.gameObject.SetActive(true),
                actionOnRelease: c => c.gameObject.SetActive(false),
                actionOnDestroy: c => { if (c != null) Object.Destroy(c.gameObject); },
                collectionCheck: false,
                defaultCapacity: defaultCapacity,
                maxSize: maxSize);
        }

        public T Get() => _pool.Get();

        public void Release(T item) => _pool.Release(item);

        public void Prewarm(int count)
        {
            var tmp = new T[count];
            for (int i = 0; i < count; i++) tmp[i] = _pool.Get();
            for (int i = 0; i < count; i++) _pool.Release(tmp[i]);
        }
    }
}

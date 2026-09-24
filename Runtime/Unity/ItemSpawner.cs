using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace InventorySystem
{
    /// <summary>
    /// Puts an item back into the world - e.g. when it is dropped out of the inventory UI. The object comes
    /// from <see cref="SpawnOverride"/> if set (e.g. a pool), otherwise from <c>Instantiate</c>; it is then
    /// placed, given its amount, and its <see cref="Collectible"/>'s On Release is invoked.
    /// </summary>
    /// <remarks>
    /// A UnityEvent can't hand back an object, so where dropped objects come from is the one thing wired
    /// in code. Set it once from the code that already knows every system (e.g. the GameManager), so the
    /// inventory itself never references the pool:
    /// <code>
    /// ItemSpawner.SpawnOverride = item =>
    ///     item.Prefab.TryGetComponent&lt;Poolable&gt;(out var poolable) ? PoolManager.Get(poolable.PoolType) : null;
    /// </code>
    /// </remarks>
    public static class ItemSpawner
    {
        /// <summary>Supplies the object for an item; return null to fall back to <c>Instantiate(item.Prefab)</c>.</summary>
        public static Func<ItemDefinition, GameObject> SpawnOverride { get; set; }

        /// <summary>
        /// Spawns <paramref name="item"/>'s prefab at <paramref name="position"/> holding <paramref name="amount"/>,
        /// then invokes its On Release. Returns its Collectible (null if the prefab has none), or null if
        /// the item has no prefab.
        /// </summary>
        public static Collectible Spawn(ItemDefinition item, int amount, Vector3 position, Quaternion rotation)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));

            if (item.Prefab == null)
            {
                Debug.LogWarning($"[ItemSpawner] '{item.DisplayName}' has no prefab to spawn.");
                return null;
            }

            var instance = SpawnOverride?.Invoke(item);
            if (instance == null)
            {
                instance = Object.Instantiate(item.Prefab);
            }

            instance.transform.SetPositionAndRotation(position, rotation);

            if (!instance.TryGetComponent<Collectible>(out var collectible))
            {
                return null;
            }

            collectible.Amount = amount;
            collectible.NotifyReleased();
            return collectible;
        }

        // Keeps the static state clean when "Enter Play Mode Options" skips the domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => SpawnOverride = null;
    }
}

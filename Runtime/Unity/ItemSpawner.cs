using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace InventorySystem
{
    /// <summary>
    /// Puts an item back into the world - e.g. when it is dropped out of the inventory UI. The object comes
    /// from <see cref="SpawnOverride"/> if set; otherwise, with PoolSystem in the project, from the pool its
    /// prefab was compiled into (a PoolData entry, so it has a Poolable) once <c>PoolManager</c> is initialized;
    /// otherwise from <c>Instantiate</c>. It is then placed, given its amount, and its
    /// <see cref="Collectible"/>'s On Release is invoked.
    /// </summary>
    /// <remarks>
    /// A pooled object goes back to its pool by itself when it is collected again (see <see cref="Collectible"/>).
    /// <see cref="SpawnOverride"/> is for anything else, e.g. a pool of your own.
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
#if HAS_POOL_SYSTEM
            if (instance == null && PoolSystem.PoolManager.TryGet(item.Prefab, out var pooled))
            {
                instance = pooled;
            }
#endif
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

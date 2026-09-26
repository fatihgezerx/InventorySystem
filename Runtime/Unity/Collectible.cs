using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace InventorySystem
{
    /// <summary>
    /// Makes a GameObject give an item to the inventory. Added automatically (and assigned its
    /// <see cref="ItemType"/>) by an <see cref="InventoryData"/>'s "Compile" step. Call <see cref="Collect"/>
    /// to pick it up - e.g. drag it into an <c>Interactable</c>'s <c>On Interact</c> and pick
    /// <c>Collect</c> - or turn on Collect On Trigger to pick it up on contact.
    /// </summary>
    /// <remarks>
    /// <b>On Take</b> fires once everything in it went into the inventory (Compile wires
    /// <c>GameObject.SetActive(false)</c> by default, so a taken object leaves the world), and <b>On Release</b>
    /// fires on an object that was just dropped from the inventory back into the world (see
    /// <see cref="ItemSpawner"/>). With PoolSystem in the project, an object taken from a pool (a dropped item)
    /// also goes back to it once taken - don't wire <c>Poolable &gt; ReleaseSelf</c> as well.
    /// If the inventory can't take everything, the object keeps what's left (<see cref="Amount"/>) and
    /// <b>On Inventory Full</b> fires. The amount resets to the Inspector value every time the object is
    /// enabled, so pooled collectibles come back full.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class Collectible : MonoBehaviour
    {
        // Written by InventoryData > Compile and shown read-only: InventoryData is where it is edited.
        [SerializeField] internal ItemTypes itemType = ItemTypes.None;

        [Tooltip("How many of the item this object gives.")]
        [Min(1)] [SerializeField] private int amount = 1;

        [Tooltip("Collect automatically when a collider with Collector Tag enters this object's trigger collider.")]
        [SerializeField] private bool collectOnTrigger;

        [Tooltip("Tag of the object (or its Rigidbody's object) that collects on trigger.")]
        [SerializeField] private string collectorTag = "Player";

        [Tooltip("Invoked once everything in this object went into the inventory. Remove the object from the world here: " +
                 "GameObject > SetActive(false), or Poolable > ReleaseSelf for pooled objects.")]
        [FormerlySerializedAs("onCollected")]
        [SerializeField] internal UnityEvent onTake = new();

        [Tooltip("Invoked on an object that was just dropped from the inventory back into the world, after its position and Amount are set.")]
        [SerializeField] private UnityEvent onRelease = new();

        [Tooltip("Invoked when the inventory couldn't take everything (nothing or only part of it was collected).")]
        [SerializeField] private UnityEvent onInventoryFull = new();

        private int _remaining;

        /// <summary>The item this object gives.</summary>
        public ItemTypes ItemType => itemType;

        /// <summary>The definition (name, icon...) of the item this object gives - e.g. for an "E - Pick up Apple" prompt.</summary>
        public ItemDefinition Item => InventoryManager.GetItem(itemType);

        /// <summary>How many of the item are still in this object. Set it after spawning to give a different amount.</summary>
        public int Amount
        {
            get => _remaining;
            set => _remaining = Mathf.Max(0, value);
        }

        private void OnEnable() => _remaining = amount;

        /// <summary>Puts as much as fits into the main inventory. For UnityEvents (e.g. an Interactable's On Interact).</summary>
        public void Collect()
        {
            if (!InventoryManager.IsInitialized)
            {
                Debug.LogError("[Collectible] InventoryManager is not initialized; nothing collected.", this);
                return;
            }

            CollectInto(InventoryManager.Main);
        }

        /// <summary>Puts as much as fits into <paramref name="inventory"/> and returns how many went in.</summary>
        public int CollectInto(Inventory inventory)
        {
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));

            if (_remaining <= 0)
            {
                return 0;
            }

            var added = inventory.Add(itemType, _remaining);
            _remaining -= added;

            if (_remaining > 0)
            {
                onInventoryFull.Invoke();
            }
            else
            {
                onTake.Invoke();
#if HAS_POOL_SYSTEM
                // Taken from a pool (e.g. dropped from the inventory): back to it. An object placed in the scene
                // isn't, and stays as On Take left it.
                PoolSystem.PoolManager.TryRelease(gameObject);
#endif
            }

            return added;
        }

        /// <summary>Called by <see cref="ItemSpawner"/> once a dropped object is placed and holds its amount.</summary>
        internal void NotifyReleased() => onRelease.Invoke();

        private void OnTriggerEnter(Collider other)
        {
            if (collectOnTrigger && _remaining > 0 && IsCollector(other))
            {
                Collect();
            }
        }

        private bool IsCollector(Collider other) =>
            other.CompareTag(collectorTag) || (other.attachedRigidbody != null && other.attachedRigidbody.CompareTag(collectorTag));
    }
}

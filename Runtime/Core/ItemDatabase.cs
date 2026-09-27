using System;
using System.Collections.Generic;
using UnityEngine;

namespace InventorySystem
{
    /// <summary>
    /// Every <see cref="ItemDefinition"/> of an <see cref="InventoryData"/>, looked up by
    /// <see cref="ItemTypes"/>. Backed by a plain array indexed by the item's id, so a lookup is a single
    /// array read - no hashing, no boxing.
    /// </summary>
    public sealed class ItemDatabase
    {
        private readonly ItemDefinition[] _byId;
        private readonly ItemDefinition[] _all;

        // The id of the group each item belongs to, by item id (0: none).
        private readonly int[] _groupById;

        public ItemDatabase(InventoryData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var items = new List<ItemDefinition>();
            var itemGroups = new List<int>();
            var maxId = 0;
            foreach (var group in data.Groups)
            {
                if (group == null)
                {
                    continue;
                }

                foreach (var item in group.Items)
                {
                    if (item == null || item.Id <= 0)
                    {
                        continue;
                    }

                    items.Add(item);
                    itemGroups.Add(group.Id);
                    maxId = Math.Max(maxId, item.Id);
                }
            }

            _byId = new ItemDefinition[maxId + 1];
            _groupById = new int[maxId + 1];
            var unique = new List<ItemDefinition>(items.Count);
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (_byId[item.Id] != null)
                {
                    Debug.LogWarning($"[ItemDatabase] '{item.DisplayName}' shares its id with '{_byId[item.Id].DisplayName}'; " +
                                      "ignoring it. Open the InventoryData and press Compile.");
                    continue;
                }

                if (!Enum.IsDefined(typeof(ItemTypes), item.Id))
                {
                    Debug.LogWarning($"[ItemDatabase] '{item.DisplayName}' has no ItemTypes member yet. " +
                                      "Open the InventoryData and press Compile.");
                }

                _byId[item.Id] = item;
                _groupById[item.Id] = itemGroups[i];
                unique.Add(item);
            }

            _all = unique.ToArray();
        }

        /// <summary>Every item, in the order they appear in the <see cref="InventoryData"/>.</summary>
        public IReadOnlyList<ItemDefinition> All => _all;

        /// <summary>Size of the id-indexed lookup: every item id is below this.</summary>
        public int IdCapacity => _byId.Length;

        /// <summary>The definition of <paramref name="type"/>. Throws if the data has no such item.</summary>
        public ItemDefinition Get(ItemTypes type)
        {
            if (TryGet(type, out var item))
            {
                return item;
            }

            throw new ArgumentException($"[ItemDatabase] '{type}' is not an item of this InventoryData. " +
                                        "Add it and press Compile.", nameof(type));
        }

        public bool TryGet(ItemTypes type, out ItemDefinition item)
        {
            var id = (int)type;
            item = (uint)id < (uint)_byId.Length ? _byId[id] : null;
            return item != null;
        }

        public bool Contains(ItemTypes type) => TryGet(type, out _);

        /// <summary>The group <paramref name="type"/> belongs to; <see cref="ItemGroups.None"/> when the data has no such item.</summary>
        public ItemGroups GroupOf(ItemTypes type)
        {
            var id = (int)type;
            return (uint)id < (uint)_groupById.Length ? (ItemGroups)_groupById[id] : ItemGroups.None;
        }
    }
}

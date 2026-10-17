#if HAS_VENEER
using Veneer;
using UnityEditor;

namespace InventorySystem
{
    /// <summary>
    /// Inspector of <see cref="Collectible"/> in the studio look, drawn from a Veneer layout: the project's own copy made in the Inspector
    /// Designer when there is one, otherwise <see cref="CollectibleDefaultLayout"/>. Pickup tab: the item it gives (read-only, with its
    /// icon - the Inventory Data is where it is edited, Compile assigns it), the amount, and the pickup options (collect on contact,
    /// collector tag). Events tab: On Take, On Release and On Inventory Full. The state pill, the item row and the "no item" hint are
    /// custom items (<see cref="CollectibleItems"/>). Works with several objects selected.
    /// </summary>
    [CustomEditor(typeof(Collectible))]
    [CanEditMultipleObjects]
    internal sealed class CollectibleStudioEditor : VeneerLayoutDrivenInspector<Collectible>
    {
        /// <inheritdoc />
        protected override VeneerBrand Brand => InventoryBrand.Value;
    }
}
#endif

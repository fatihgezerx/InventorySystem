#if HAS_VENEER
using System;
using Veneer;

namespace InventorySystem
{
    /// <summary>
    /// The design of the <see cref="Collectible"/> Inspector that Inventory System ships: a Pickup tab (Item card with the read-only item
    /// row, the "no item" hint and the amount; Pick up card with Collect on trigger, the collector tag and a hint for manual pickup) and
    /// an Events tab, plus the system's own items (state pill, item row, "no item" hint). With Veneer installed a project can copy it in
    /// the Inspector Designer and change the copy; this default is never saved as an asset.
    /// </summary>
    internal sealed class CollectibleDefaultLayout : IVeneerDefaultLayoutProvider
    {
        /// <inheritdoc />
        public Type TargetType => typeof(Collectible);

        /// <inheritdoc />
        public VeneerLayout CreateLayout()
        {
            VeneerLayout layout = new VeneerLayoutBuilder(typeof(Collectible), "Collectible", "Gives an item to the inventory")
                .Page("Pickup", "tag")
                .Card("Item", "tag")
                .Custom(CollectibleItems.StateId)
                .Field(CollectibleItems.ItemPath, "Item")
                .Custom(CollectibleItems.NoItemId)
                .Field("amount", "Amount", unit: "items", widget: VeneerWidget.Number)
                .Card("Pick up", "target")
                .Field("collectOnTrigger", "Collect on trigger", widget: VeneerWidget.Toggle)
                .Icon("bolt")
                .Field("collectorTag", "Collector tag", widget: VeneerWidget.Tag)
                .ShowIf("collectOnTrigger", VeneerCondition.IsTrue)
                .Note("Without Collect On Trigger, call Collect() from code or from an Interactable's On Interact.")
                .ShowIf("collectOnTrigger", VeneerCondition.IsFalse)
                .Page("Events", "bolt")
                .Card("Events", "bolt")
                .Field("onTake", "On Take")
                .Field("onRelease", "On Release")
                .Field("onInventoryFull", "On Inventory Full")
                .Build();

            // The look of the system (accent, style sheet). UseThemeLogo stays off on purpose: the Inspector's builder already carries the
            // brand and shows its logo in the header.
            layout.ThemeBrand = InventoryBrand.Value.Name;
            return layout;
        }

        /// <inheritdoc />
        public void RegisterItems()
        {
            CollectibleItems.Register();
        }
    }
}
#endif

#if HAS_VENEER
using System;
using Veneer;

namespace InventorySystem
{
    /// <summary>
    /// The design of the <see cref="GoldCollectible"/> Inspector that Inventory System ships: a Gold tab (Random toggle; the fixed amount,
    /// or the Min / Max slider while it is on) and an Events tab. With Veneer installed a project can copy it in the Inspector Designer
    /// and change the copy; this default is never saved as an asset.
    /// </summary>
    internal sealed class GoldCollectibleDefaultLayout : IVeneerDefaultLayoutProvider
    {
        /// <inheritdoc />
        public Type TargetType => typeof(GoldCollectible);

        /// <inheritdoc />
        public VeneerLayout CreateLayout()
        {
            VeneerLayout layout = new VeneerLayoutBuilder(typeof(GoldCollectible), "Gold Collectible", "Gives gold to the player")
                .Page("Gold", "tag")
                .Card("Gold", "tag")
                .Field("randomAmount", "Random amount", widget: VeneerWidget.Toggle)
                .Icon("bolt")
                .Field("amount", "Amount", unit: "gold", widget: VeneerWidget.Number)
                .ShowIf("randomAmount", VeneerCondition.IsFalse)
                .MinMax("range", "Amount range", 0f, GoldCollectible.MaxSliderGold, "gold")
                .ShowIf("randomAmount", VeneerCondition.IsTrue)
                .Note("Call Collect() from code or from an Interactable's On Interact.")
                .Page("Events", "bolt")
                .Card("Events", "bolt")
                .Field("onCollected", "On Collected")
                .Build();

            // The look of the system (accent, style sheet). UseThemeLogo stays off on purpose: the Inspector's builder already carries the
            // brand and shows its logo in the header.
            layout.ThemeBrand = InventoryBrand.Value.Name;
            return layout;
        }

        /// <inheritdoc />
        public void RegisterItems()
        {
        }
    }
}
#endif

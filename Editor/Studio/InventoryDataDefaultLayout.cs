#if HAS_VENEER
using System;
using Veneer;

namespace InventorySystem
{
    /// <summary>
    /// The design of the <see cref="InventoryData"/> Inspector that Inventory System ships: an Items tab (the Items pane) and a Groups tab
    /// (the info note and the Groups pane), plus the system's own items (state pill, Compile card and issue hints under the header). With
    /// Veneer installed a project can copy it in the Inspector Designer and change the copy; this default is never saved as an asset.
    /// </summary>
    internal sealed class InventoryDataDefaultLayout : IVeneerDefaultLayoutProvider
    {
        /// <summary>The default header title. While a layout keeps it, the header shows the asset's name instead.</summary>
        public const string DefaultTitle = "Inventory Data";

        /// <summary>The default header subtitle. While a layout keeps it, the header shows the group and item counts instead.</summary>
        public const string DefaultSubtitle = "Inventory data";

        private const string GroupsInfo =
            "Every group is an inventory of its own, with its own type and size (e.g. \"Books\" unlimited, \"Weapons\" a grid). " +
            "A group's name becomes its ItemGroups member and each item's name its ItemTypes member (\"Health Potion\" -> " +
            "ItemTypes.HealthPotion), so names must be unique. With more than one group, Compile also makes a popup script per " +
            "group (and Easy UI roles for its popup and tab button), and adds a Collectible to every item's prefab. " +
            "Press Compile after any change - and after renaming or reordering groups.";

        /// <inheritdoc />
        public Type TargetType => typeof(InventoryData);

        /// <inheritdoc />
        public VeneerLayout CreateLayout()
        {
            // The pill, the Compile card and the issue hints are drawn in their own slots (header, under the header); their entries sit in
            // the first card because a slot item needs an entry somewhere. The pane cards have no title: the panes draw their own boxes.
            VeneerLayout layout = new VeneerLayoutBuilder(typeof(InventoryData), DefaultTitle, DefaultSubtitle)
                .Page("Items", "tag")
                .Card("Items", "tag", false)
                .Custom(InventoryDataItems.StateId)
                .Custom(InventoryDataItems.CompileId)
                .Custom(InventoryDataItems.IssuesId)
                .Custom(InventoryDataItems.ItemsPaneId)
                .Page("Groups", "layers")
                .Card("Groups", "layers", false)
                .Note(GroupsInfo)
                .Custom(InventoryDataItems.GroupsPaneId)
                .Build();

            // Fields of the data asset that the old Inspector never showed.
            layout.HiddenPaths.Add("settings");
            layout.HiddenPaths.Add("settingsMigrated");
            layout.HiddenPaths.Add("nextItemId");
            layout.HiddenPaths.Add("nextGroupId");

            // The look of the system (accent, style sheet). UseThemeLogo stays off on purpose: the Inspector's builder already carries the
            // brand and shows its logo in the header.
            layout.ThemeBrand = InventoryBrand.Value.Name;
            return layout;
        }

        /// <inheritdoc />
        public void RegisterItems()
        {
            InventoryDataItems.Register();
        }
    }
}
#endif

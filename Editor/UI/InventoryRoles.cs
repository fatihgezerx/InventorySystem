#if HAS_EASYUI
using System;
using System.Collections.Generic;
using EasyUI;
using UnityEngine;

namespace InventorySystem
{
    /// <summary>
    /// The roles an Easy UI panel's elements can have for the inventory - picked in Easy UI, from the Role menu
    /// at the top of an element's inspector. Build UI (in the InventoryData's UI SETTINGS) finds the elements by
    /// these roles, never by name, and adds the inventory's views to them.
    /// </summary>
    /// <remarks>
    /// The panel you design is the inventory window itself, and the boxes in it are yours: roles only go on what
    /// does something - the slots and their container, texts, images, buttons, the weight slider, the examine
    /// view. What a box is follows from what is in it: the element holding the Tooltip Name is the tooltip, the
    /// one holding the Toast Message is the toast template. So does which item an Item Icon shows: the slot's
    /// inside the Slot Template, the notification's inside the toast template, the selected item's elsewhere.
    /// </remarks>
    public static class InventoryRoles
    {
        public const string SlotContainer = "inventory.slot-container";
        public const string SlotTemplate = "inventory.slot-template";
        public const string SlotAmount = "inventory.slot-amount";
        public const string CellTemplate = "inventory.cell-template";
        public const string CapacityText = "inventory.capacity-text";
        public const string WeightBar = "inventory.weight-bar";
        public const string CloseButton = "inventory.close-button";
        public const string OrganizeButton = "inventory.organize-button";
        public const string ItemIcon = "inventory.item-icon";
        public const string DetailsName = "inventory.details-name";
        public const string DetailsDescription = "inventory.details-description";
        public const string ExamineView = "inventory.examine-view";
        public const string DropButton = "inventory.drop-button";
        public const string UseButton = "inventory.use-button";
        public const string TooltipName = "inventory.tooltip-name";
        public const string TooltipDetails = "inventory.tooltip-details";
        public const string ToastMessage = "inventory.toast-message";

        /// <summary>What makes a tooltip: the element holding them is the tooltip box.</summary>
        public static readonly string[] TooltipRoles = { TooltipName, TooltipDetails };

        private static readonly List<EasyUINode> Found = new();

        /// <summary>
        /// Whether <paramref name="icon"/> (an Item Icon) shows the selected item: it isn't in the Slot Template, nor
        /// in the toast template (the element holding the Toast Message).
        /// </summary>
        public static bool IsSelectedItemIcon(EasyUIDocument document, EasyUINode icon)
        {
            var slot = EasyUIRoles.FindNode(document, SlotTemplate);
            if (slot != null && document.IsUnder(icon, slot))
            {
                return false;
            }

            var message = EasyUIRoles.FindNode(document, ToastMessage);
            var toast = message != null ? document.Find(message.parentId) : null;
            return toast == null || !document.IsUnder(icon, toast);
        }

        /// <summary>
        /// Whether something shows the selected item - Item Name, Item Description, Examine View, or an Item Icon
        /// that isn't a slot's or a notification's: it makes a Details view (and Can Click useful).
        /// </summary>
        public static bool HasDetails(EasyUIDocument document)
        {
            if (HasAny(document, DetailsName, DetailsDescription, ExamineView))
            {
                return true;
            }

            EasyUIRoles.FindNodes(document, ItemIcon, Found);
            foreach (var icon in Found)
            {
                if (IsSelectedItemIcon(document, icon))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>How much a line of the checklist matters for an inventory of a given type.</summary>
        public enum Need
        {
            /// <summary>Build UI can't do without it.</summary>
            Required,

            /// <summary>A feature: used when the panel has it.</summary>
            Optional,

            /// <summary>Not used by this inventory type (e.g. the Organize button outside Grid).</summary>
            Unused
        }

        /// <summary>One line of the UI SETTINGS checklist: a feature, what makes it, and how much it matters.</summary>
        public readonly struct Check
        {
            public readonly string Label;
            public readonly Need Need;
            private readonly string[] _roles;
            private readonly Func<EasyUIDocument, bool> _test;

            /// <summary>A feature any of <paramref name="roles"/> makes.</summary>
            public Check(string label, Need need, params string[] roles)
            {
                Label = label;
                Need = need;
                _roles = roles;
                _test = null;
            }

            /// <summary>A feature <paramref name="test"/> finds in a panel.</summary>
            public Check(string label, Need need, Func<EasyUIDocument, bool> test)
            {
                Label = label;
                Need = need;
                _roles = null;
                _test = test;
            }

            /// <summary>Whether <paramref name="document"/> has the feature.</summary>
            public bool IsIn(EasyUIDocument document) => _test != null ? _test(document) : HasAny(document, _roles);
        }

        /// <summary>The checklist of UI SETTINGS for an inventory of <paramref name="type"/>: first what Build UI needs, then the features.</summary>
        public static IEnumerable<Check> Checklist(InventoryType type)
        {
            var grid = type == InventoryType.Grid;
            yield return new Check("Slot Container", Need.Required, SlotContainer);
            yield return new Check("Slot Template", Need.Required, SlotTemplate);
            yield return new Check("Cell Template (else made from the Slot Template)", grid ? Need.Optional : Need.Unused, CellTemplate);
            yield return new Check("Capacity Text", Need.Optional, CapacityText);
            yield return new Check("Weight Bar", type == InventoryType.Weight ? Need.Optional : Need.Unused, WeightBar);
            yield return new Check("Close Button", Need.Optional, CloseButton);
            yield return new Check("Organize Button", grid ? Need.Optional : Need.Unused, OrganizeButton);
            yield return new Check("Details (name, description, icon or examine view)", Need.Optional, HasDetails);
            yield return new Check("Examine View", Need.Optional, ExamineView);
            yield return new Check("Drop Button", Need.Optional, DropButton);
            yield return new Check("Use Button", Need.Optional, UseButton);
            yield return new Check("Tooltip", Need.Optional, TooltipRoles);
            yield return new Check("Notifications", Need.Optional, ToastMessage);
        }

        /// <summary>Whether <paramref name="document"/> has any of <paramref name="roles"/>.</summary>
        public static bool HasAny(EasyUIDocument document, params string[] roles)
        {
            foreach (var role in roles)
            {
                if (EasyUIRoles.FindNode(document, role) != null)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Lists <see cref="InventoryRoles"/> in Easy UI's Role menu, under Inventory. Each role is offered only on the
    /// kind of element it becomes: text roles on Texts, image roles on Images, button roles on Buttons, the weight
    /// bar on Sliders, the examine view on Raw Images. Of the boxes, only the slots' are roles - the Slot Container
    /// and the Slot Template - on Empties and Images (a box with a background is an Image), the template on Buttons
    /// too.
    /// </summary>
    internal sealed class InventoryRoleProvider : IEasyUIRoleProvider
    {
        private static readonly EasyUIElementType[] Texts = { EasyUIElementType.Text };
        private static readonly EasyUIElementType[] Images = { EasyUIElementType.Image };
        private static readonly EasyUIElementType[] Buttons = { EasyUIElementType.Button };

        public IEnumerable<EasyUIRole> GetRoles()
        {
            // The slots.
            yield return new EasyUIRole(InventoryRoles.SlotContainer, "Inventory/Slot Container",
                "Where the slots are laid out - e.g. a Scroll View's Content. Its Grid Layout Group, if any, gives the cell size and spacing.",
                EasyUIElementType.EmptyObject, EasyUIElementType.Image);
            yield return new EasyUIRole(InventoryRoles.SlotTemplate, "Inventory/Slot Template",
                "One slot, copied for every slot (Grid: every item). Hidden itself.",
                EasyUIElementType.EmptyObject, EasyUIElementType.Image, EasyUIElementType.Button);

            // Images.
            yield return new EasyUIRole(InventoryRoles.ItemIcon, "Inventory/Item Icon",
                "An item's icon: the slot's inside the Slot Template, the notification's next to the Toast Message, " +
                "the selected item's anywhere else. Several elements can have it.", false, Images);
            yield return new EasyUIRole(InventoryRoles.CellTemplate, "Inventory/Cell Template",
                "Grid inventories: one empty background cell, copied for every cell of the grid (items lie over them). " +
                "Hidden itself. Optional: without it, cells are made from the Slot Template's look.", Images);

            // Texts.
            yield return new EasyUIRole(InventoryRoles.SlotAmount, "Inventory/Slot Amount",
                "Inside the Slot Template (or its Item Icon, to lie over it): the stack's amount.", Texts);
            yield return new EasyUIRole(InventoryRoles.CapacityText, "Inventory/Capacity Text",
                "How full the inventory is: \"12.5 / 50\" (Weight), \"8 / 20\" (Max Slot) or used / total cells (Grid).", Texts);
            yield return new EasyUIRole(InventoryRoles.DetailsName, "Inventory/Item Name", "The selected item's name.", Texts);
            yield return new EasyUIRole(InventoryRoles.DetailsDescription, "Inventory/Item Description", "The selected item's description.", Texts);
            yield return new EasyUIRole(InventoryRoles.TooltipName, "Inventory/Tooltip Name",
                "The hovered item's name. The element holding it is the tooltip, which follows the pointer.", Texts);
            yield return new EasyUIRole(InventoryRoles.TooltipDetails, "Inventory/Tooltip Details",
                "The hovered item's stack and weight, in the tooltip.", Texts);
            yield return new EasyUIRole(InventoryRoles.ToastMessage, "Inventory/Toast Message",
                "A notification's text (\"+3 Apple\", \"Inventory full\"). The element holding it (with an Item Icon, if you like) " +
                "is copied for every notification; its parent stacks them.", Texts);

            // Buttons.
            yield return new EasyUIRole(InventoryRoles.CloseButton, "Inventory/Close Button", "Closes the inventory.", Buttons);
            yield return new EasyUIRole(InventoryRoles.OrganizeButton, "Inventory/Organize Button", "Grid inventories: packs the items tightly.", Buttons);
            yield return new EasyUIRole(InventoryRoles.DropButton, "Inventory/Drop Button", "Drops the selected item into the world.", Buttons);
            yield return new EasyUIRole(InventoryRoles.UseButton, "Inventory/Use Button",
                "Uses the selected item. Does nothing yet by itself: the inventory window raises UseRequested for your code.", Buttons);

            // Slider.
            yield return new EasyUIRole(InventoryRoles.WeightBar, "Inventory/Weight Bar",
                "Weight inventories: filled to total / max weight (hidden in other types).", EasyUIElementType.Slider);

            // Raw Image.
            yield return new EasyUIRole(InventoryRoles.ExamineView, "Inventory/Examine View",
                "The selected item's 3D model, turned by dragging on it.", EasyUIElementType.RawImage);
        }
    }

    /// <summary>
    /// Where the InventoryData Inspector's Build UI hands off: set by the inventory's UI scripts in your MVC folder
    /// (<c>Panels/Editor/InventoryUIBinder</c>), which know the views. Null while they aren't in the project.
    /// </summary>
    public static class InventoryUIBuild
    {
        /// <summary>Builds <paramref name="panel"/> for <paramref name="data"/> and returns its root, or null if it didn't.</summary>
        public static Func<InventoryData, EasyUIPanel, GameObject> Builder { get; set; }
    }
}
#endif

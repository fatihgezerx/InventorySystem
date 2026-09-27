#if HAS_EASYUI
using System;
using System.Collections.Generic;
using EasyUI;
using UnityEditor;

namespace InventorySystem
{
    /// <summary>
    /// The roles an Easy UI panel's elements can have for the inventory - picked in Easy UI (Add Role, at the top of
    /// an element's inspector). When the panel is built (<c>GameObject &gt; UI (Canvas) &gt; Easy UI</c>), the
    /// inventory's UI scripts in your MVC folder (<c>Panels/Editor/InventoryUIBinder</c>) find the elements by these
    /// roles, never by name, and add the inventory's views to them, wired to each other.
    /// </summary>
    /// <remarks>
    /// The Inventory Panel (usually the panel's root) is the window, and the boxes in it are yours: roles only go on
    /// what does something - the slots and their container, texts, images, buttons, the weight slider, the examine
    /// view. What a box is follows from what is in it: the one holding the Toast Message is the toast template. So
    /// does which item an Item Icon shows: the slot's inside the Slot Template, the notification's inside the toast
    /// template, the selected item's elsewhere. How the slots behave is up to the roles next to the Slot Template:
    /// Clickable, Draggable, Drop To World - and with Basic Tooltip, its Shows Tooltip, filled by the slot with its
    /// item.
    /// </remarks>
    public static class InventoryRoles
    {
        public const string Panel = "inventory.panel";
        public const string ListContainer = "inventory.slot-container.list";
        public const string GridContainer = "inventory.slot-container.grid";
        public const string SlotTemplate = "inventory.slot-template";
        public const string Clickable = "inventory.slot.clickable";
        public const string Draggable = "inventory.slot.draggable";
        public const string DropToWorld = "inventory.slot.drop-to-world";
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
        public const string ToastMessage = "inventory.toast-message";

        /// <summary>The role of the popup of the group with id <paramref name="groupId"/> (only offered with several groups).</summary>
        public static string GroupPopup(int groupId) => $"inventory.group.{groupId}.popup";

        /// <summary>The role of the tab button that opens the popup of the group with id <paramref name="groupId"/>.</summary>
        public static string GroupTab(int groupId) => $"inventory.group.{groupId}.tab";

        // Localization System's Localized Text, by id: the inventory's texts are filled by code, and a
        // LocalizedText would overwrite them with one line.
        internal const string LocalizedText = "localization.localized-text";

        /// <summary>
        /// Whether <paramref name="icon"/> (an Item Icon) shows the selected item: it isn't in the Slot Template, nor
        /// in the toast template (the element holding the Toast Message).
        /// </summary>
        public static bool IsSelectedItemIcon(EasyUIDocument document, EasyUINode icon)
        {
            // Several groups have a Slot Template each.
            foreach (var node in document.nodes)
            {
                if (node.HasRole(SlotTemplate) && document.IsUnder(icon, node))
                {
                    return false;
                }
            }

            var message = EasyUIRoles.FindNode(document, ToastMessage);
            var toast = message != null ? document.Find(message.parentId) : null;
            return toast == null || !document.IsUnder(icon, toast);
        }

        /// <summary>Whether any element of <paramref name="document"/> has an inventory role.</summary>
        public static bool IsUsedIn(EasyUIDocument document)
        {
            foreach (var node in document.nodes)
            {
                foreach (var role in node.roles)
                {
                    if (role.StartsWith("inventory.", StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Lists <see cref="InventoryRoles"/> in Easy UI's Add Role menu, under Inventory - and, when the InventoryData has
    /// several groups, a Popup and a Tab Button role for each of them (after Compile). Each role is offered only on the
    /// kind of element it becomes: text roles on Texts, image roles on Images, button roles on Buttons, the weight
    /// bar on Sliders, the examine view on Raw Images. Of the boxes, only the window's and the slots' are roles - the
    /// Inventory Panel, the Slot Container and the Slot Template - on Empties and Images (a box with a background is
    /// an Image), the template on Buttons too.
    /// </summary>
    internal sealed class InventoryRoleProvider : IEasyUIRoleProvider
    {
        private static readonly EasyUIElementType[] Texts = { EasyUIElementType.Text };
        private static readonly EasyUIElementType[] Images = { EasyUIElementType.Image };
        private static readonly EasyUIElementType[] Buttons = { EasyUIElementType.Button };
        private static readonly EasyUIElementType[] Boxes = { EasyUIElementType.EmptyObject, EasyUIElementType.Image };
        private static readonly EasyUIElementType[] Slots = { EasyUIElementType.EmptyObject, EasyUIElementType.Image, EasyUIElementType.Button };
        private static readonly string[] FilledByCode = { InventoryRoles.LocalizedText };
        private static readonly string[] OnSlot = { InventoryRoles.SlotTemplate };

        public IEnumerable<EasyUIRole> GetRoles()
        {
            // The window.
            yield return new EasyUIRole(InventoryRoles.Panel, "Inventory/Inventory Panel",
                "The inventory window - usually the panel's root. Opens and closes whole (the Inventory action, or Tab / I), " +
                "and holds everything below. Blocks gameplay while open.", Boxes);

            // Everything a group's popup can have of its own - containers, slot templates and what the slots do, buttons,
            // texts, the weight bar - isn't unique: several elements can have the role. Only the window, the toast and the
            // examine view are.

            // The slots.
            yield return new EasyUIRole(InventoryRoles.ListContainer, "Inventory/Slot Container (List)",
                "Unlimited, Weight and Max Slot inventories: where the slots are laid out - e.g. a Scroll View's Content - by " +
                "its layout group: a Grid (rows), a Horizontal (one row, e.g. a hotbar) or a Vertical one (one column). " +
                "With several groups, one inside each group's popup.", false, Boxes)
            {
                Conflicts = new[] { InventoryRoles.GridContainer }
            };
            yield return new EasyUIRole(InventoryRoles.GridContainer, "Inventory/Slot Container (Grid)",
                "Grid inventories: where the grid is drawn, Columns x Rows of its group in the InventoryData. Its Grid Layout Group, if any, " +
                "gives the cell size and spacing (and goes: the panel places cells and items itself). " +
                "With several groups, one inside each group's popup.", false, Boxes);
            // Not unique: with several groups every group's popup can have a Slot Template of its own (one outside the
            // popups is shared by those without).
            yield return new EasyUIRole(InventoryRoles.SlotTemplate, "Inventory/Slot Template",
                "One slot, copied for every slot (Grid: every item). Hidden itself. Give it Clickable, Draggable and Drop To World " +
                "for what its slots do - and Tooltip > Shows Tooltip for its item's tooltip. With several groups, put one inside " +
                "each group's popup (or one outside them all, shared by the popups without).", false, Slots);

            // What the slots do: roles next to the Slot Template.
            yield return new EasyUIRole(InventoryRoles.Clickable, "Inventory/Clickable Slot",
                "Clicking an item - or starting to drag it - selects it and shows it in the details (Item Name, Description, " +
                "Icon, Examine View).", false, Slots)
            {
                Requires = OnSlot
            };
            yield return new EasyUIRole(InventoryRoles.Draggable, "Inventory/Draggable Slot",
                "Items can be dragged: moved, merged, split (right drag), swapped and carried to another window.", false, Slots)
            {
                Requires = OnSlot
            };
            yield return new EasyUIRole(InventoryRoles.DropToWorld, "Inventory/Drop To World",
                "An item dragged out of the UI is dropped into the world, in front of the player.", false, Slots)
            {
                Requires = new[] { InventoryRoles.SlotTemplate, InventoryRoles.Draggable }
            };

            // Images.
            yield return new EasyUIRole(InventoryRoles.ItemIcon, "Inventory/Item Icon",
                "An item's icon: the slot's inside the Slot Template, the notification's next to the Toast Message, " +
                "the selected item's anywhere else. Several elements can have it.", false, Images);
            yield return new EasyUIRole(InventoryRoles.CellTemplate, "Inventory/Cell Template",
                "Grid inventories: one empty background cell, copied for every cell of the grid (items lie over them). " +
                "Hidden itself. Optional: without it, cells are made from the Slot Template's look.", false, Images);

            // Texts.
            yield return new EasyUIRole(InventoryRoles.SlotAmount, "Inventory/Slot Amount",
                "Inside the Slot Template (or its Item Icon, to lie over it): the stack's amount.", false, Texts)
            {
                Conflicts = FilledByCode
            };
            yield return new EasyUIRole(InventoryRoles.CapacityText, "Inventory/Capacity Text",
                "How full the inventory is: \"12.5 / 50\" (Weight), \"8 / 20\" (Max Slot) or used / total cells (Grid).", false, Texts)
            {
                Conflicts = FilledByCode
            };
            yield return new EasyUIRole(InventoryRoles.DetailsName, "Inventory/Item Name", "The selected item's name.", false, Texts)
            {
                Conflicts = FilledByCode
            };
            yield return new EasyUIRole(InventoryRoles.DetailsDescription, "Inventory/Item Description", "The selected item's description.", false, Texts)
            {
                Conflicts = FilledByCode
            };
            yield return new EasyUIRole(InventoryRoles.ToastMessage, "Inventory/Toast Message",
                "A notification's text (\"+3 Apple\", \"Inventory full\"). The element holding it (with an Item Icon, if you like) " +
                "is copied for every notification; its parent stacks them, and shows while the inventory is closed.", Texts)
            {
                Conflicts = FilledByCode
            };

            // Buttons.
            yield return new EasyUIRole(InventoryRoles.CloseButton, "Inventory/Close Button", "Closes the inventory.", false, Buttons);
            yield return new EasyUIRole(InventoryRoles.OrganizeButton, "Inventory/Organize Button", "Grid inventories: packs the items tightly.", false, Buttons);
            yield return new EasyUIRole(InventoryRoles.DropButton, "Inventory/Drop Button", "Drops the selected item into the world.", false, Buttons);
            yield return new EasyUIRole(InventoryRoles.UseButton, "Inventory/Use Button",
                "Uses the selected item. Does nothing yet by itself: the inventory window raises UseRequested for your code.", false, Buttons);

            // Slider.
            yield return new EasyUIRole(InventoryRoles.WeightBar, "Inventory/Weight Bar",
                "Weight inventories: filled to total / max weight (hidden in other types).", false, EasyUIElementType.Slider);

            // Raw Image.
            yield return new EasyUIRole(InventoryRoles.ExamineView, "Inventory/Examine View",
                "The selected item's 3D model, turned by dragging on it.", EasyUIElementType.RawImage);

            // Several groups: a popup and a tab button for each, named after the group (as of the last Compile).
            var groups = ProjectGroups();
            if (groups.Count < 2)
            {
                yield break;
            }

            foreach (var group in groups)
            {
                var name = group.Header.Replace('/', ' ').Trim();
                yield return new EasyUIRole(InventoryRoles.GroupPopup(group.Id), $"Inventory/{name} Popup",
                    $"The popup of the {name} group, inside the Inventory Panel: shows that group's slots, and opens when its " +
                    "tab button is pressed (the first group's is open each time the window opens). Give it a Slot Container " +
                    "inside.", Boxes);
                yield return new EasyUIRole(InventoryRoles.GroupTab(group.Id), $"Inventory/{name} Tab Button",
                    $"Opens the {name} popup and closes the one that was open. Pressing it while its popup is open does nothing.",
                    Buttons);
            }
        }

        // The groups of the project's (first) InventoryData - those with an id - or none.
        private static List<ItemGroup> ProjectGroups()
        {
            var found = new List<ItemGroup>();
            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(InventoryData)))
            {
                var data = AssetDatabase.LoadAssetAtPath<InventoryData>(AssetDatabase.GUIDToAssetPath(guid));
                if (data == null)
                {
                    continue;
                }

                foreach (var group in data.Groups)
                {
                    if (group != null && group.Id > 0 && !string.IsNullOrWhiteSpace(group.Header))
                    {
                        found.Add(group);
                    }
                }

                break;
            }

            return found;
        }
    }
}
#endif

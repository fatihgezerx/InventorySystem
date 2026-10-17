#if HAS_VENEER
using System;
using Veneer;
using UnityEngine.UIElements;

namespace InventorySystem
{
    /// <summary>
    /// The pieces of the <see cref="InventoryData"/> Inspector that are not plain fields, registered with Veneer so the default layout (and a
    /// user's copy of it) can place them: the state pill, the Compile card, the issue hints, and the Items and Groups panes. They share one
    /// <see cref="InventoryDataSession"/> per open Inspector. They can be moved or hidden in the Inspector Designer, not changed.
    /// </summary>
    internal static class InventoryDataItems
    {
        /// <summary>Id of the header pill: errors, Compiled or Needs compile (header badge). It also keeps the title and counts of the header.</summary>
        public const string StateId = "inventory.state";

        /// <summary>Id of the Compile card under the header (after-header item).</summary>
        public const string CompileId = "inventory.compile";

        /// <summary>Id of the error, warning and "not compiled yet" hint rows (after-header item).</summary>
        public const string IssuesId = "inventory.issues";

        /// <summary>Id of the Items pane: group chips, item rows and the details of the selected item (card item).</summary>
        public const string ItemsPaneId = "inventory.items-pane";

        /// <summary>Id of the Groups pane: group rows and the capacity of the selected group (card item).</summary>
        public const string GroupsPaneId = "inventory.groups-pane";

        private const string GroupsPath = "groups";
        private const string BareClass = "inv-bare";
        private const string CardClass = "vn-card";

        private const string CompileTooltip =
            "Generates ItemTypes and ItemGroups, adds a Collectible to every item prefab and writes the group popup scripts.";

        private const string CompileText =
            "Writes the ItemTypes / ItemGroups enums and gives every item prefab its Collectible. " +
            "Press it after renaming, reordering or adding items.";

        /// <summary>Registers the custom items of <see cref="InventoryData"/>. Safe to call again (items are replaced).</summary>
        public static void Register()
        {
            Type type = typeof(InventoryData);
            string[] claims = { GroupsPath };

            VeneerCustomItems.Register(type, new VeneerCustomItem
            {
                Id = StateId,
                Label = "State pill",
                Icon = "check-circle",
                Description = "A pill in the header: n errors, Needs compile or Compiled. Also shows the asset's name and the group and item counts in the header.",
                Slot = VeneerCustomItemSlot.HeaderBadge,
                Build = BuildState,
            });

            VeneerCustomItems.Register(type, new VeneerCustomItem
            {
                Id = CompileId,
                Label = "Compile card",
                Icon = "play",
                Description = "The prominent Compile button with a line of explanation. Drawn under the header, whatever tab is open.",
                Slot = VeneerCustomItemSlot.AfterHeader,
                Build = BuildCompile,
            });

            VeneerCustomItems.Register(type, new VeneerCustomItem
            {
                Id = IssuesId,
                Label = "Issues",
                Icon = "warning",
                Description = "Hint rows for errors and warnings (each with a Show button that goes to the item or group) and the not-compiled hint.",
                Slot = VeneerCustomItemSlot.AfterHeader,
                Build = BuildIssues,
            });

            VeneerCustomItems.Register(type, new VeneerCustomItem
            {
                Id = ItemsPaneId,
                Label = "Items pane",
                Icon = "tag",
                Description = "Chips to pick a group, that group's items as reorderable rows and the details of the selected item.",
                Slot = VeneerCustomItemSlot.Item,
                Build = BuildItemsPane,
                Claims = claims,
            });

            VeneerCustomItems.Register(type, new VeneerCustomItem
            {
                Id = GroupsPaneId,
                Label = "Groups pane",
                Icon = "layers",
                Description = "The groups as reorderable rows and the capacity settings of the selected group.",
                Slot = VeneerCustomItemSlot.Item,
                Build = BuildGroupsPane,
                Claims = claims,
            });
        }

        private static VisualElement BuildState(VeneerCustomItemContext context)
        {
            InventoryDataSession session = InventoryDataSession.For(context);
            session.EnsureTracker(context.Builder.HeaderRight);
            session.BindHeader(context);
            Label pill = VeneerStyle.Pill(string.Empty, "idle");
            Action refresh = () =>
            {
                (string text, string kind) state = session.PillState();
                VeneerStyle.SetPill(pill, state.kind, state.text);
            };

            // The first run of Poll is on the first tick: show the right state at once.
            refresh();
            context.Poll(refresh);
            return pill;
        }

        private static VisualElement BuildCompile(VeneerCustomItemContext context)
        {
            InventoryDataSession session = InventoryDataSession.For(context);
            VeneerInspectorBuilder b = context.Builder;
            VisualElement card = b.Card();
            using (b.Into(card))
            {
                VisualElement row = b.Row();
                row.AddToClassList("inv-compile");
                using (b.Into(row))
                {
                    b.Button("Compile", "play", session.Compile, CompileTooltip, true);
                    Label text = VeneerStyle.MutedLabel(CompileText);
                    text.AddToClassList("inv-compile__text");
                    b.Add(text);
                }
            }

            return null;
        }

        private static VisualElement BuildIssues(VeneerCustomItemContext context)
        {
            return InventoryDataSession.For(context).BuildIssues();
        }

        private static VisualElement BuildItemsPane(VeneerCustomItemContext context)
        {
            MarkBare(context.Builder.Current);
            return InventoryDataSession.For(context).BuildItemsPane();
        }

        private static VisualElement BuildGroupsPane(VeneerCustomItemContext context)
        {
            MarkBare(context.Builder.Current);
            return InventoryDataSession.For(context).BuildGroupsPane();
        }

        // The panes are lists and details that already draw their own boxes: the card that holds one loses its border, background and
        // padding, so it looks as it did before the Inspector was drawn from a layout.
        private static void MarkBare(VisualElement container)
        {
            for (VisualElement e = container; e != null; e = e.parent)
            {
                if (e.ClassListContains(CardClass))
                {
                    e.AddToClassList(BareClass);
                    return;
                }
            }
        }
    }
}
#endif

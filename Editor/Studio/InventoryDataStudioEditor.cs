#if HAS_VENEER
using Veneer;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace InventorySystem
{
    /// <summary>
    /// Inspector of <see cref="InventoryData"/> in the studio look: a header card (counts and a compile-state pill), the prominent
    /// Compile button, validation as hint rows (each with a Show button), and two tabs - Items (the items of one group as rows with
    /// an icon slot, details one click away) and Groups (reorderable groups with the capacity settings of each). The Compile step
    /// itself is <see cref="InventoryCompiler"/>, unchanged; the UI is designed in Easy UI and set up by roles (see InventoryRoles.cs).
    /// </summary>
    [CustomEditor(typeof(InventoryData))]
    internal sealed class InventoryDataStudioEditor : VeneerInspector<InventoryData>
    {
        private const string TabKey = "InventorySystem.InventoryData.Tab";
        private const int MaxShownPerKind = 5;

        private const string GroupsInfo =
            "Every group is an inventory of its own, with its own type and size (e.g. \"Books\" unlimited, \"Weapons\" a grid). " +
            "A group's name becomes its ItemGroups member and each item's name its ItemTypes member (\"Health Potion\" -> " +
            "ItemTypes.HealthPotion), so names must be unique. With more than one group, Compile also makes a popup script per " +
            "group (and Easy UI roles for its popup and tab button), and adds a Collectible to every item's prefab. " +
            "Press Compile after any change - and after renaming or reordering groups.";

        private InventoryDataCheck.Report report;
        private InventoryItemsPane itemsPane;
        private InventoryGroupsPane groupsPane;
        private VisualElement tabBar;
        private VisualElement[] pages;
        private VisualElement issuesHost;
        private Label subtitle;
        private string issueKey;

        protected override VeneerBrand Brand => InventoryBrand.Value;

        protected override void Build(VeneerInspectorBuilder b)
        {
            if (TargetCount > 1)
            {
                b.Header("Inventory Data", "Several assets selected");
                b.Hint("Select a single Inventory Data asset to edit it.", "info");
                return;
            }

            var ctx = new InventoryStudioContext(serializedObject, Brand, GetReport);
            VisualElement header = b.Header(Target.name, SubtitleText());
            subtitle = header.Q<Label>(className: "vn-subtitle");
            b.StatePill(PillState);

            // The Compile action, always in front.
            VisualElement compileCard = b.Card();
            using (b.Into(compileCard))
            {
                VisualElement row = b.Row();
                row.AddToClassList("inv-compile");
                using (b.Into(row))
                {
                    b.Button("Compile", "play", OnCompile,
                        "Generates ItemTypes and ItemGroups, adds a Collectible to every item prefab and writes the group popup scripts.", true);
                    Label text = VeneerStyle.MutedLabel("Writes the ItemTypes / ItemGroups enums and gives every item prefab its Collectible. " +
                                                        "Press it after renaming, reordering or adding items.");
                    text.AddToClassList("inv-compile__text");
                    b.Add(text);
                }
            }

            issuesHost = b.Add(new VisualElement());

            b.Add(VeneerStyle.TabView(TabKey, new[] { "Items", "Groups" }, new[] { "tag", "layers" }, null, Brand, out pages, out tabBar));
            itemsPane = new InventoryItemsPane(ctx);
            groupsPane = new InventoryGroupsPane(ctx);
            itemsPane.RequestAddGroup = () =>
            {
                groupsPane.AddGroup();
                SelectTab(1);
            };
            pages[0].Add(itemsPane.Root);
            pages[1].Add(VeneerStyle.Hint(GroupsInfo, "info", Brand));
            pages[1].Add(groupsPane.Root);

            OnDataChanged();
            InventoryStudioHelpers.Tracker(b.Root, serializedObject, OnDataChanged);
        }

        private InventoryDataCheck.Report GetReport()
        {
            return report ??= InventoryDataCheck.Analyze(Target);
        }

        private (string text, string kind) PillState()
        {
            InventoryDataCheck.Report current = GetReport();
            if (current.Errors > 0)
            {
                return (current.Errors == 1 ? "1 error" : current.Errors + " errors", "error");
            }

            return current.Compiled ? ("Compiled", "ok") : ("Needs compile", "warn");
        }

        private string SubtitleText()
        {
            InventoryDataCheck.Report current = GetReport();
            return "Inventory data  |  " + current.GroupCount + (current.GroupCount == 1 ? " group" : " groups") + "  |  " +
                   current.ItemCount + (current.ItemCount == 1 ? " item" : " items");
        }

        private void OnDataChanged()
        {
            serializedObject.UpdateIfRequiredOrScript();
            report = null;
            itemsPane.OnDataChanged();
            groupsPane.OnDataChanged();
            RefreshIssues();
            if (subtitle != null)
            {
                subtitle.text = SubtitleText();
            }
        }

        private void OnCompile()
        {
            serializedObject.ApplyModifiedProperties();
            InventoryCompiler.Compile(Target);
            serializedObject.Update();
            OnDataChanged();
        }

        private void SelectTab(int index)
        {
            VeneerStyle.SetSegmentedSelected(tabBar, index);
            for (int i = 0; i < pages.Length; i++)
            {
                VeneerStyle.SetDisplay(pages[i], i == index);
            }

            SessionState.SetInt(TabKey, index);
        }

        private void JumpTo(int group, int item)
        {
            if (item >= 0)
            {
                SelectTab(0);
                itemsPane.Show(group, item);
            }
            else
            {
                SelectTab(1);
                groupsPane.Show(group);
            }
        }

        private void RefreshIssues()
        {
            InventoryDataCheck.Report current = GetReport();
            string key = current.Key + "|" + current.Compiled + "|" + current.StaleReason;
            if (string.Equals(key, issueKey, System.StringComparison.Ordinal))
            {
                return;
            }

            issueKey = key;
            issuesHost.Clear();
            AddIssues(current, InventoryDataCheck.Severity.Error, "error", current.Errors);
            AddIssues(current, InventoryDataCheck.Severity.Warning, "warn", current.Warnings);
            if (current.Errors == 0 && !current.Compiled && current.StaleReason != null)
            {
                issuesHost.Add(VeneerStyle.Hint("Not compiled yet: " + current.StaleReason + " Press Compile.", "info", Brand));
            }
        }

        private void AddIssues(InventoryDataCheck.Report current, InventoryDataCheck.Severity severity, string kind, int total)
        {
            int shown = 0;
            foreach (InventoryDataCheck.Issue issue in current.Issues)
            {
                if (issue.Severity != severity)
                {
                    continue;
                }

                if (shown == MaxShownPerKind)
                {
                    issuesHost.Add(VeneerStyle.Hint("... and " + (total - shown) + " more.", kind, Brand));
                    return;
                }

                shown++;
                VisualElement hint = VeneerStyle.Hint(issue.Message, kind, Brand);
                if (issue.Group >= 0)
                {
                    int group = issue.Group;
                    int item = issue.Item;
                    hint.Add(VeneerStyle.Button("Show", null, "Go to the item or group this is about.", () => JumpTo(group, item), false, Brand));
                }

                issuesHost.Add(hint);
            }
        }
    }
}
#endif

#if HAS_VENEER
using System;
using System.Runtime.CompilerServices;
using Veneer;
using UnityEditor;
using UnityEngine.UIElements;

namespace InventorySystem
{
    /// <summary>
    /// What the pieces of one open <see cref="InventoryData"/> Inspector share: the validation report (cached until the data changes), the
    /// Items and Groups panes, the issue hints, the header texts and the change tracker. The Inspector is drawn from a layout, so the
    /// pieces are separate custom items; each finds this object through <see cref="For"/>, keyed weakly by the Inspector's root element
    /// (no Unity object is kept by a static, and a closed Inspector's session is collected with its root).
    /// </summary>
    internal sealed class InventoryDataSession
    {
        private const int MaxShownPerKind = 5;
        private const string TabsClass = "vn-tabs";

        private static readonly ConditionalWeakTable<VisualElement, InventoryDataSession> Sessions =
            new ConditionalWeakTable<VisualElement, InventoryDataSession>();

        private readonly SerializedObject so;
        private readonly VeneerBrand brand;
        private readonly VisualElement root;
        private readonly InventoryStudioContext studio;

        private InventoryDataCheck.Report report;
        private InventoryItemsPane itemsPane;
        private InventoryGroupsPane groupsPane;
        private VisualElement trackerHost;
        private VisualElement issuesList;
        private Label subtitle;
        private string issueKey;
        private int errorsShown = -1;
        private string errorsText;

        private InventoryDataSession(SerializedObject so, VeneerBrand brand, VisualElement root)
        {
            this.so = so;
            this.brand = brand;
            this.root = root;
            studio = new InventoryStudioContext(so, brand, GetReport);
        }

        /// <summary>The session of the Inspector the item is being built for; made on first use.</summary>
        /// <param name="context">The context a custom item is built with.</param>
        public static InventoryDataSession For(VeneerCustomItemContext context)
        {
            VisualElement key = context.Builder.Root;
            if (!Sessions.TryGetValue(key, out InventoryDataSession session))
            {
                session = new InventoryDataSession(context.SerializedObject, context.Brand, key);
                Sessions.Add(key, session);
            }

            return session;
        }

        private InventoryData Target => so.targetObject as InventoryData;

        /// <summary>The validation report of the data; computed again after a change.</summary>
        public InventoryDataCheck.Report GetReport()
        {
            return report ??= InventoryDataCheck.Analyze(Target);
        }

        /// <summary>The text and kind of the header pill: errors, Compiled or Needs compile. Allocates only when the error count changes.</summary>
        public (string text, string kind) PillState()
        {
            InventoryDataCheck.Report current = GetReport();
            if (current.Errors > 0)
            {
                if (errorsShown != current.Errors)
                {
                    errorsShown = current.Errors;
                    errorsText = current.Errors == 1 ? "1 error" : current.Errors + " errors";
                }

                return (errorsText, "error");
            }

            return current.Compiled ? ("Compiled", "ok") : ("Needs compile", "warn");
        }

        private string SubtitleText()
        {
            InventoryDataCheck.Report current = GetReport();
            return "Inventory data  |  " + current.GroupCount + (current.GroupCount == 1 ? " group" : " groups") + "  |  " +
                   current.ItemCount + (current.ItemCount == 1 ? " item" : " items");
        }

        /// <summary>
        /// Finds the header texts once the header is drawn. While the layout still carries the system's default title and subtitle, the title
        /// becomes the asset's name and the subtitle the group and item counts (kept up to date); a text the user changed is left alone.
        /// </summary>
        public void BindHeader(VeneerCustomItemContext context)
        {
            if (context.Layout == null || subtitle != null)
            {
                return;
            }

            if (string.Equals(context.Layout.Title, InventoryDataDefaultLayout.DefaultTitle, StringComparison.Ordinal) && Target != null)
            {
                Label title = root.Q<Label>(className: "vn-title");
                if (title != null)
                {
                    title.text = Target.name;
                }
            }

            if (string.Equals(context.Layout.Subtitle, InventoryDataDefaultLayout.DefaultSubtitle, StringComparison.Ordinal))
            {
                subtitle = root.Q<Label>(className: "vn-subtitle");
                if (subtitle != null)
                {
                    subtitle.text = SubtitleText();
                }
            }
        }

        /// <summary>Adds the change tracker under <paramref name="parent"/> unless a live one exists (then data changes call <see cref="OnDataChanged"/>).</summary>
        public void EnsureTracker(VisualElement parent)
        {
            if (parent == null || TrackerAlive())
            {
                return;
            }

            trackerHost = InventoryStudioHelpers.Tracker(parent, so, OnDataChanged);
        }

        private bool TrackerAlive()
        {
            for (VisualElement e = trackerHost; e != null; e = e.parent)
            {
                if (e == root)
                {
                    return true;
                }
            }

            return false;
        }

        // ------------------------------------------------------------------ pieces

        /// <summary>Makes the Items pane (and tells it what to do when the user asks for a first group).</summary>
        public VisualElement BuildItemsPane()
        {
            itemsPane = new InventoryItemsPane(studio);
            itemsPane.RequestAddGroup = AddGroupAndShow;
            itemsPane.OnDataChanged();
            EnsureTracker(itemsPane.Root);
            return itemsPane.Root;
        }

        /// <summary>Makes the Groups pane.</summary>
        public VisualElement BuildGroupsPane()
        {
            groupsPane = new InventoryGroupsPane(studio);
            groupsPane.OnDataChanged();
            EnsureTracker(groupsPane.Root);
            return groupsPane.Root;
        }

        /// <summary>Makes the container of the issue hints (error and warning rows with Show buttons, and the "not compiled yet" hint).</summary>
        public VisualElement BuildIssues()
        {
            var host = new VisualElement();
            issuesList = new VisualElement();
            host.Add(issuesList);
            issueKey = null;
            RefreshIssues();
            EnsureTracker(host);
            return host;
        }

        /// <summary>Writes the enums and prefabs through <see cref="InventoryCompiler"/> and brings every piece in step.</summary>
        public void Compile()
        {
            so.ApplyModifiedProperties();
            InventoryCompiler.Compile(Target);
            so.Update();
            OnDataChanged();
        }

        /// <summary>Brings every built piece in step with the data (edits, Undo, Compile).</summary>
        public void OnDataChanged()
        {
            so.UpdateIfRequiredOrScript();
            report = null;
            if (itemsPane != null)
            {
                itemsPane.OnDataChanged();
            }

            if (groupsPane != null)
            {
                groupsPane.OnDataChanged();
            }

            RefreshIssues();
            if (subtitle != null)
            {
                subtitle.text = SubtitleText();
            }
        }

        private void AddGroupAndShow()
        {
            if (groupsPane != null)
            {
                groupsPane.AddGroup();
                SelectTabOf(groupsPane.Root);
                return;
            }

            // The Groups pane is not in this layout: add the group straight to the data.
            SerializedProperty groups = so.FindProperty("groups");
            groups.arraySize++;
            InventoryStudioHelpers.ResetGroup(groups.GetArrayElementAtIndex(groups.arraySize - 1));
            so.ApplyModifiedProperties();
            OnDataChanged();
        }

        private void JumpTo(int group, int item)
        {
            if (item >= 0)
            {
                if (itemsPane != null)
                {
                    SelectTabOf(itemsPane.Root);
                    itemsPane.Show(group, item);
                }
            }
            else if (groupsPane != null)
            {
                SelectTabOf(groupsPane.Root);
                groupsPane.Show(group);
            }
        }

        // Selects the tab whose page holds <element>: the tab view is a bar (vn-tabs) followed by one page per tab. Does nothing when the
        // element is not in a tab view (a layout with a single tab). The engine remembers a tab only when it is clicked, not here.
        private static void SelectTabOf(VisualElement element)
        {
            VisualElement page = element;
            while (page.parent != null)
            {
                VisualElement view = page.parent;
                if (view.childCount > 1 && view[0] != page && view[0].ClassListContains(TabsClass))
                {
                    int index = view.IndexOf(page) - 1;
                    VeneerStyle.SetSegmentedSelected(view[0], index);
                    for (int i = 1; i < view.childCount; i++)
                    {
                        VeneerStyle.SetDisplay(view[i], i == index + 1);
                    }

                    return;
                }

                page = view;
            }
        }

        // ------------------------------------------------------------------ issue hints

        private void RefreshIssues()
        {
            if (issuesList == null)
            {
                return;
            }

            InventoryDataCheck.Report current = GetReport();
            string key = current.Key + "|" + current.Compiled + "|" + current.StaleReason;
            if (string.Equals(key, issueKey, StringComparison.Ordinal))
            {
                return;
            }

            issueKey = key;
            issuesList.Clear();
            AddIssues(current, InventoryDataCheck.Severity.Error, "error", current.Errors);
            AddIssues(current, InventoryDataCheck.Severity.Warning, "warn", current.Warnings);
            if (current.Errors == 0 && !current.Compiled && current.StaleReason != null)
            {
                issuesList.Add(VeneerStyle.Hint("Not compiled yet: " + current.StaleReason + " Press Compile.", "info", brand));
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
                    issuesList.Add(VeneerStyle.Hint("... and " + (total - shown) + " more.", kind, brand));
                    return;
                }

                shown++;
                VisualElement hint = VeneerStyle.Hint(issue.Message, kind, brand);
                if (issue.Group >= 0)
                {
                    int group = issue.Group;
                    int item = issue.Item;
                    hint.Add(VeneerStyle.Button("Show", null, "Go to the item or group this is about.", () => JumpTo(group, item), false, brand));
                }

                issuesList.Add(hint);
            }
        }
    }
}
#endif

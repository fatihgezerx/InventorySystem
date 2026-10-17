using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace InventorySystem
{
    /// <summary>
    /// Looks at an <see cref="InventoryData"/> the way <see cref="InventoryCompiler"/> will: which problems would abort
    /// Compile (errors), which are only suspicious (warnings), and whether the generated <c>ItemTypes</c> /
    /// <c>ItemGroups</c> enums and the item prefabs' <see cref="Collectible"/>s already match the data. Plain logic
    /// with no UI, so the Inspector can show it as pills and hint rows. Reads only; changes nothing.
    /// </summary>
    internal static class InventoryDataCheck
    {
        /// <summary>How serious one finding is.</summary>
        internal enum Severity
        {
            /// <summary>Compile would stop on it.</summary>
            Error,

            /// <summary>Compile goes on, but the data is probably not what was meant.</summary>
            Warning
        }

        /// <summary>One finding, with the place it points at (for a "Show" button).</summary>
        internal readonly struct Issue
        {
            public readonly Severity Severity;
            public readonly string Message;

            /// <summary>Index of the group it is about, or -1 when it is about the whole asset.</summary>
            public readonly int Group;

            /// <summary>Index of the item in that group, or -1 when it is about the group itself.</summary>
            public readonly int Item;

            public Issue(Severity severity, string message, int group, int item)
            {
                Severity = severity;
                Message = message;
                Group = group;
                Item = item;
            }
        }

        /// <summary>The result of one <see cref="Analyze"/>.</summary>
        internal sealed class Report
        {
            public readonly List<Issue> Issues = new();
            public int Errors;
            public int Warnings;
            public int GroupCount;
            public int ItemCount;

            /// <summary>True when there is no error and the generated enums and the prefabs match the data.</summary>
            public bool Compiled;

            /// <summary>Why <see cref="Compiled"/> is false although there is no error (null when it is true or there are errors).</summary>
            public string StaleReason;

            /// <summary>A key that changes whenever the list of findings changes, so a view can skip redrawing an unchanged one.</summary>
            public string Key = string.Empty;
        }

        /// <summary>Analyzes <paramref name="data"/>.</summary>
        public static Report Analyze(InventoryData data)
        {
            var report = new Report();
            var groups = data.Groups;
            report.GroupCount = groups.Count;

            if (groups.Count == 0)
            {
                Add(report, Severity.Error, "There is no item group. Add one first.", -1, -1);
            }

            string stale = null;
            var groupNames = new Dictionary<string, string>(StringComparer.Ordinal);
            var itemNames = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
            var prefabOwners = new Dictionary<GameObject, ItemDefinition>();
            var prefabsToUpdate = 0;

            for (var g = 0; g < groups.Count; g++)
            {
                var group = groups[g];
                if (group == null)
                {
                    continue;
                }

                var groupLabel = string.IsNullOrWhiteSpace(group.Header) ? "Group " + (g + 1) : group.Header;
                var groupMember = InventoryCompiler.ToIdentifier(group.Header);
                if (groupMember == null)
                {
                    Add(report, Severity.Error, $"{groupLabel} has an empty or invalid name. Every group needs a unique name: it becomes its ItemGroups member.", g, -1);
                }
                else if (groupMember == nameof(ItemGroups.None))
                {
                    Add(report, Severity.Error, $"Group '{group.Header}' is named 'None', which is reserved. Rename it.", g, -1);
                }
                else if (groupNames.TryGetValue(groupMember, out var firstGroup))
                {
                    Add(report, Severity.Error, $"Groups '{firstGroup}' and '{group.Header}' both become ItemGroups.{groupMember}. Rename one.", g, -1);
                }
                else
                {
                    groupNames.Add(groupMember, group.Header);
                    if (group.Id <= 0 || Enum.GetName(typeof(ItemGroups), group.Id) != groupMember)
                    {
                        stale ??= $"Group '{group.Header}' is new or renamed.";
                    }
                }

                var settings = group.Settings;
                var items = group.Items;
                for (var i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    if (item == null)
                    {
                        continue;
                    }

                    report.ItemCount++;
                    var itemLabel = string.IsNullOrWhiteSpace(item.DisplayName) ? $"Item {i + 1} of '{groupLabel}'" : $"'{item.DisplayName}'";
                    var member = InventoryCompiler.ToIdentifier(item.DisplayName);
                    if (member == null)
                    {
                        Add(report, Severity.Error, $"{itemLabel} has an empty or invalid name. Every item needs a unique name: it becomes its ItemTypes member.", g, i);
                    }
                    else if (member == nameof(ItemTypes.None))
                    {
                        Add(report, Severity.Error, $"'{item.DisplayName}' is named 'None', which is reserved for \"no item\". Rename it.", g, i);
                    }
                    else if (itemNames.TryGetValue(member, out var sameName))
                    {
                        Add(report, Severity.Error, $"'{sameName.DisplayName}' and '{item.DisplayName}' both become ItemTypes.{member}. Rename one.", g, i);
                    }
                    else
                    {
                        itemNames.Add(member, item);
                        if (item.Id <= 0 || Enum.GetName(typeof(ItemTypes), item.Id) != member)
                        {
                            stale ??= $"'{item.DisplayName}' is new or renamed.";
                        }
                    }

                    if (item.Icon == null)
                    {
                        Add(report, Severity.Warning, $"{itemLabel} has no icon.", g, i);
                    }

                    if (settings != null && settings.InventoryType == InventoryType.Grid)
                    {
                        var size = item.Size;
                        var fits = (size.x <= settings.Columns && size.y <= settings.Rows)
                                   || (item.CanRotate && size.y <= settings.Columns && size.x <= settings.Rows);
                        if (!fits)
                        {
                            Add(report, Severity.Warning,
                                $"{itemLabel} ({size.x} x {size.y} cells) can't fit the {settings.Columns} x {settings.Rows} grid of '{groupLabel}' in any rotation.", g, i);
                        }
                    }

                    var prefab = item.Prefab;
                    if (prefab == null)
                    {
                        continue;
                    }

                    if (prefabOwners.TryGetValue(prefab, out var owner))
                    {
                        Add(report, Severity.Error,
                            $"Prefab '{prefab.name}' is used by both '{owner.DisplayName}' and '{item.DisplayName}'; a prefab can give only one item.", g, i);
                        continue;
                    }

                    prefabOwners.Add(prefab, item);
                    if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(prefab)))
                    {
                        Add(report, Severity.Warning, $"'{prefab.name}' ({itemLabel}) is not a prefab asset; no Collectible will be added.", g, i);
                    }
                    else if (!prefab.TryGetComponent<Collectible>(out var collectible) || collectible.itemType != item.Type)
                    {
                        prefabsToUpdate++;
                    }
                }
            }

            if (report.Errors == 0)
            {
                // Members of removed items or groups are still in the generated enums until the next Compile.
                if (Enum.GetNames(typeof(ItemTypes)).Length - 1 != itemNames.Count)
                {
                    stale ??= "Items were removed since the last Compile.";
                }

                if (Enum.GetNames(typeof(ItemGroups)).Length - 1 != groupNames.Count)
                {
                    stale ??= "Groups were removed since the last Compile.";
                }

                if (prefabsToUpdate > 0)
                {
                    stale ??= prefabsToUpdate == 1 ? "1 item prefab needs its Collectible." : $"{prefabsToUpdate} item prefabs need their Collectible.";
                }

                report.StaleReason = stale;
                report.Compiled = stale == null;
            }

            var key = new System.Text.StringBuilder();
            foreach (var issue in report.Issues)
            {
                key.Append(issue.Group).Append(',').Append(issue.Item).Append(':').Append(issue.Message).Append('\n');
            }

            report.Key = key.ToString();
            return report;
        }

        private static void Add(Report report, Severity severity, string message, int group, int item)
        {
            report.Issues.Add(new Issue(severity, message, group, item));
            if (severity == Severity.Error)
            {
                report.Errors++;
            }
            else
            {
                report.Warnings++;
            }
        }
    }
}

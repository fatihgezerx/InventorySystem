using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;

namespace InventorySystem
{
    /// <summary>
    /// Implements the "Compile" step of an <see cref="InventoryData"/>:
    /// <list type="number">
    /// <item>makes sure every item has a unique, stable id,</item>
    /// <item>gives every item's prefab a <see cref="Collectible"/> (only if missing) and writes its item
    /// type - prefabs already up to date are skipped, not re-saved. A newly added Collectible's On Take
    /// is wired to <c>GameObject.SetActive(false)</c>, so a taken object leaves the world by default;
    /// swap it for e.g. <c>Poolable &gt; ReleaseSelf</c> in the Inspector,</item>
    /// <item>regenerates <c>ItemTypes.cs</c>, only if its content actually changed, so compiling without
    /// renaming anything never triggers a script reload.</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// Unlike the pool compiler this needs no second pass after the reload: every <see cref="ItemTypes"/>
    /// member's value is the item's id, so a prefab can be given <c>(ItemTypes)id</c> right away and the
    /// member name catches up once Unity recompiles.
    /// </remarks>
    internal static class InventoryCompiler
    {
        private const string DefaultGeneratedPath = "Assets/Scripts/InventorySystem/Runtime/Generated/ItemTypes.cs";

        public static void Compile(InventoryData data)
        {
            Undo.RecordObject(data, "Compile Inventory Data");
            if (data.EnsureUniqueIds())
            {
                EditorUtility.SetDirty(data);
            }

            var members = new List<KeyValuePair<string, int>>();
            var names = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
            var prefabs = new List<KeyValuePair<string, ItemDefinition>>();
            var prefabOwners = new Dictionary<GameObject, ItemDefinition>();

            foreach (var group in data.Groups)
            {
                foreach (var item in group.Items)
                {
                    if (item == null)
                    {
                        continue;
                    }

                    var memberName = ToIdentifier(item.DisplayName);
                    if (memberName == null)
                    {
                        Debug.LogError($"[InventoryData] An item in group '{group.Header}' has an empty or invalid name " +
                                        $"('{item.DisplayName}'). Compile aborted - fix it and try again.", data);
                        return;
                    }

                    if (memberName == nameof(ItemTypes.None))
                    {
                        Debug.LogError($"[InventoryData] '{item.DisplayName}' in group '{group.Header}' is named 'None', which " +
                                        "is reserved for \"no item\". Rename it. Compile aborted.", data);
                        return;
                    }

                    if (names.TryGetValue(memberName, out var sameName))
                    {
                        Debug.LogError($"[InventoryData] '{item.DisplayName}' and '{sameName.DisplayName}' both become " +
                                        $"ItemTypes.{memberName}. Every item needs a unique name. Compile aborted.", data);
                        return;
                    }

                    names.Add(memberName, item);
                    members.Add(new KeyValuePair<string, int>(memberName, item.Id));

                    if (item.Prefab == null)
                    {
                        continue;
                    }

                    if (prefabOwners.TryGetValue(item.Prefab, out var owner))
                    {
                        Debug.LogError($"[InventoryData] Prefab '{item.Prefab.name}' is used by both '{owner.DisplayName}' and " +
                                        $"'{item.DisplayName}'; a prefab can give only one item. Compile aborted.", data);
                        return;
                    }

                    prefabOwners.Add(item.Prefab, item);

                    var path = AssetDatabase.GetAssetPath(item.Prefab);
                    if (string.IsNullOrEmpty(path))
                    {
                        Debug.LogWarning($"[InventoryData] '{item.Prefab.name}' ('{item.DisplayName}') is not a prefab asset; " +
                                          "no Collectible added.", data);
                        continue;
                    }

                    prefabs.Add(new KeyValuePair<string, ItemDefinition>(path, item));
                }
            }

            var updated = 0;
            var upToDate = 0;
            foreach (var pair in prefabs)
            {
                var item = pair.Value;
                if (item.Prefab.TryGetComponent<Collectible>(out var existing) && existing.itemType == item.Type)
                {
                    upToDate++;
                    continue;
                }

                using (var scope = new PrefabUtility.EditPrefabContentsScope(pair.Key))
                {
                    var root = scope.prefabContentsRoot;
                    if (!root.TryGetComponent<Collectible>(out var collectible))
                    {
                        collectible = root.AddComponent<Collectible>();
                        UnityEventTools.AddBoolPersistentListener(collectible.onTake, root.SetActive, false);
                    }

                    collectible.itemType = item.Type;
                }

                updated++;
            }

            AssetDatabase.SaveAssets();

            var enumChanged = WriteEnumSource(members, out var generatedPath);
            if (enumChanged)
            {
                AssetDatabase.ImportAsset(generatedPath);
            }

            Debug.Log($"[InventoryData] Compile finished: {members.Count} item(s), {updated} prefab(s) updated, " +
                      $"{upToDate} already up to date." + (enumChanged ? " ItemTypes regenerated; scripts are recompiling." : string.Empty));
        }

        /// <summary>Writes ItemTypes.cs; returns false (and writes nothing) if it already has this content.</summary>
        private static bool WriteEnumSource(List<KeyValuePair<string, int>> members, out string assetPath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated>");
            sb.AppendLine("// Generated by InventoryData > Compile. Do not edit by hand - it is overwritten every time an");
            sb.AppendLine("// InventoryData asset is compiled. Every value is the item's stable id, so reordering or removing");
            sb.AppendLine("// items never shifts the value of an ItemTypes already saved in a scene or prefab.");
            sb.AppendLine("// </auto-generated>");
            sb.AppendLine("namespace InventorySystem");
            sb.AppendLine("{");
            sb.AppendLine("    public enum ItemTypes");
            sb.AppendLine("    {");
            sb.AppendLine("        None = 0,");
            foreach (var member in members)
            {
                sb.AppendLine($"        {member.Key} = {member.Value},");
            }
            sb.AppendLine("    }");
            sb.AppendLine("}");

            assetPath = FindGeneratedPath();
            var fullPath = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName,
                assetPath.Replace('/', Path.DirectorySeparatorChar));

            var source = sb.ToString();
            if (File.Exists(fullPath) && NormalizeNewlines(File.ReadAllText(fullPath)) == NormalizeNewlines(source))
            {
                return false;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, source);
            return true;
        }

        // Follows ItemTypes.cs if the InventorySystem folder was moved; falls back to the default location.
        private static string FindGeneratedPath()
        {
            foreach (var guid in AssetDatabase.FindAssets("ItemTypes t:MonoScript"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("/Generated/ItemTypes.cs", StringComparison.Ordinal))
                {
                    return path;
                }
            }

            return DefaultGeneratedPath;
        }

        private static string NormalizeNewlines(string text) => text.Replace("\r\n", "\n");

        // "Health Potion" -> "HealthPotion", "ak-47" -> "Ak47", "9mm Ammo" -> "_9mmAmmo".
        private static string ToIdentifier(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            var sb = new StringBuilder();
            var capitalizeNext = true;
            foreach (var c in raw.Trim())
            {
                if (char.IsLetterOrDigit(c) || c == '_')
                {
                    sb.Append(capitalizeNext ? char.ToUpperInvariant(c) : c);
                    capitalizeNext = false;
                }
                else
                {
                    capitalizeNext = true;
                }
            }

            if (sb.Length == 0)
            {
                return null;
            }

            if (char.IsDigit(sb[0]))
            {
                sb.Insert(0, '_');
            }

            return sb.ToString();
        }
    }
}

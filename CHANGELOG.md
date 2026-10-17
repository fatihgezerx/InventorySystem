# Changelog

## [Unreleased] - Studio editor look

### Editor
- **Inventory Data Inspector rebuilt on Veneer** (`Editor/Studio/`): header card with counts and a compile-state pill, a prominent
  Compile button (the `InventoryCompiler` step is unchanged), validation as hint rows with Show buttons (`InventoryDataCheck`: duplicate or
  empty names, reserved `None`, shared prefabs, missing icons, items too big for their grid, enums / prefabs out of date), an **Items** tab
  (group chips, item rows with icon slot / name / key numbers, drag reorder, details of the selected item) and a **Groups** tab (reorderable
  group rows, capacity settings of the selected group). The old IMGUI Inspector, `ItemDefinitionDrawer` and `GroupDragReorder` are gone.
- **Collectible Inspector** rebuilt: *Pickup* tab (item read-only with icon, amount, collect on trigger, collector tag) and *Events* tab.
- Brand: `InventoryBrand` (green #30A46C) with `Editor/Brand/InventoryBrand.uss`.
- Veneer is an optional dependency (`HAS_VENEER`, listed in `DependencyGuard`). Without it the Inventory Data uses Unity's
  default Inspector plus the Compile button, and the Collectible the default Inspector.
- Dropped: the remembered folded state of groups (the Groups tab selects one group instead).

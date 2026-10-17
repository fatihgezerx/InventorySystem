# Changelog

## [Unreleased] - Studio editor look

### Editor
- **Collectible Inspector now drawn from a default Veneer layout** (`CollectibleDefaultLayout`: tabs Pickup and Events, same cards and fields; show-if rules and notes instead of code; system items in `CollectibleItems` for the state pill, the "no item" hint and the read-only item row as a custom widget of `itemType`). The editor derives from `VeneerLayoutDrivenInspector<Collectible>`; customisable in the Inspector Designer (Customize / Reset to system default). Without Veneer nothing changes. Not run in Unity yet.
- **Inventory Data Inspector now drawn from a default Veneer layout** (`InventoryDataDefaultLayout`: Items and Groups tabs; system items in `InventoryDataItems` - state pill, Compile card, issue hints, Items pane, Groups pane - sharing one `InventoryDataSession` per open Inspector). The editor derives from `VeneerLayoutDrivenInspector<InventoryData>`; customisable in the Inspector Designer (Customize / Reset to system default). The panes' logic is unchanged. Without Veneer nothing changes. Not run in Unity yet.
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

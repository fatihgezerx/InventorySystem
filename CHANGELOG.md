# Changelog

## [Unreleased] - Studio editor look

### Gold
- **General gold** (no item group, no slot): `InventoryManager.Gold`, `AddGold`, `HasGold`, `TrySpendGold`, `SetGold` and `GoldChangedEvent` (`Gold`, `Delta`) through `EventManager`. Reset by `Initialize` / `Shutdown`. Offline check (scratch, outside the project): add, spend, overspend, clamps, no-op without an event, reset.
- **`GoldCollectible`**: `Collect()` for an Interactable's On Interact. **Random amount** toggle: off = fixed **Amount**, on = a random whole number from the **Amount range** Min / Max slider (both included, rolled at each collect). **On Collected** (wired to `SetActive(false)` when the component is added); returns to its pool with PoolSystem; once per enable. Veneer Inspector (`GoldCollectibleDefaultLayout` / `GoldCollectibleStudioEditor`: Gold and Events tabs).
- **UI**: Easy UI role **Inventory > Gold Text** (text, several allowed, filled by code so it conflicts with Localized Text). `InventoryPanel` listens to `GoldChangedEvent` once and sets its Gold Texts when the gold changes (allocation-free), not each time the window opens; it also sets them when the inventories are (re)built. `InventoryUIBinder` wires the role. The `.cs.txt` templates of both are in sync.
- Not run in Unity yet (compiled offline; the Inspector and the role were not seen).

### Item columns (list rows with real data)
- `ItemDefinition` gained **Type** (`Category`, translatable) and **Value** (`Value`); **Weight** is now shown in every inventory mode, not only used by Weight ones. The Items tab has a *Type* field in the Item card and a *Weight and value* card (always visible). `ItemText` formats weight and value for a UI (a dash for 0).
- Easy UI roles: **Slot Name / Slot Type / Slot Weight / Slot Value** (inside the Slot Template, for list rows) and **Item Type / Item Weight / Item Value** (the item clicked). `InventorySlotButton` fills the slot's four texts when it shows an item and clears them when empty; `InventoryDetailsPanel` fills the new details texts; `InventoryUIBinder` wires them. All texts are filled by code, so Sync Project leaves them alone (`ILocalizedByCode` on the slot button and the details panel), and the content panels redraw when the language changes.
- The `.cs.txt` templates of the changed MVC scripts are in sync. Not run in Unity with a real inventory yet.

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

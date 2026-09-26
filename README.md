# InventorySystem

Data-driven inventory system for Unity with four capacity modes: **Unlimited**, **Weight**, **MaxSlot**
and **Grid**. Every change is published through **EventSystem**, so a UI only listens and never polls.
With **UniMVC**, a ready-made inventory UI is added to your project (see [UI](#ui)).

## Requirements

| Dependency | Required | Why |
|---|---|---|
| [EventSystem](https://github.com/fatihgezerx/EventSystem) | Yes | Publishes the inventory's events |
| [UniMVC](https://github.com/fatihgezerx/UniMVC) | For the UI | The inventory UI is built from UniMVC views |
| Input System (`com.unity.inputsystem`) | For the UI | Open / close key, rotating items |
| [LocalizationSystem](https://github.com/fatihgezerx/LocalizationSystem) | No | Translating item names and descriptions (see [Localization](#localization)) |

Importing InventorySystem never breaks your project. A small setup script checks for these, leaves
InventorySystem out of compilation while EventSystem is missing, and offers to install what's missing
(if you pick **Not now**, it asks again in the next editor session or when InventorySystem is imported again). EventSystem and UniMVC are downloaded
into `Assets/Scripts/EventSystem/` and `Assets/Scripts/MVC/`, exactly as if you had copied them there. InventorySystem itself contains no UI code
and no code of any other system: pooling, interaction and UI are all wired from the outside.

## Installation

Clone or download this repository and copy it into a folder under `Assets/` (e.g.
`Assets/Scripts/InventorySystem/`). **Compile** writes the generated `ItemTypes.cs` into the system's own
`Runtime/Generated/` folder.

## Setup

**1. Create the data asset** via `Create > Inventory System > Inventory Data`. Its Inspector has two
blocks: **INVENTORY SETTINGS** and **ITEMS**.

| Inventory Type | Extra settings | Limit |
|---|---|---|
| `Unlimited` | none | none; slots are added as needed |
| `Weight` | Max Weight | total weight of every item ≤ Max Weight |
| `MaxSlot` | Max Slots | a fixed number of slots |
| `Grid` | Columns, Rows | a Columns x Rows grid, like a Resident Evil 4 attaché case; every item covers a Width x Height rectangle of cells |

**2. Add your items to groups.** Group names only organize the Inspector. Every item has:

| Field | Meaning |
|---|---|
| Icon | 50x50 sprite, shown on the left |
| Name | Name for UI. Compile turns it into its `ItemTypes` member (`Health Potion` → `ItemTypes.HealthPotion`), so it must be unique |
| Prefab | The world object that gives this item (optional) |
| Stackable / Max Stack | Stackable items pile up in one slot up to Max Stack, then open a new slot. Other items (e.g. a weapon) take a new slot every time |
| Description | What the item is, e.g. for a tooltip (optional). Read it as `item.Description` |
| Weight | **Weight mode only.** Weight of one item |
| Size / Can Rotate | **Grid mode only.** Width x Height of the item's rectangle, in cells (e.g. `1 x 1` for ammo, `3 x 2` for a handgun, `9 x 2` for a rifle). The preview turns red if it can't fit the grid. With Can Rotate, the item may be turned 90° to fit |

**3. Click Compile.** It:
- generates the `ItemTypes` enum; each member's value is the item's stable id, so reordering or deleting
  items never shifts an `ItemTypes` already saved in a scene or prefab,
- adds a `Collectible` component to every item's prefab (only if missing) and assigns its item.

Prefabs that are already up to date are skipped, and `ItemTypes.cs` is only rewritten when it changes.

**Grid extras.** A `GridInventory` (the inventory of Grid mode) can also:

- `Organize()`: pack every item tightly, like the attaché case's "Organize" - the largest first, each in
  the first spot it fits, turned if that is the only way. If the packing can't find room for everything
  (possible when the grid is nearly full), nothing moves and it returns false. The window's **Organize**
  button calls it.
- `Resize(columns, rows)`: make the case bigger, e.g. one bought from a merchant. Items keep their cells;
  a smaller size is refused (false) if an item would end up outside. The UI redraws itself
  (`Inventory.LayoutChanged`).

```csharp
var grid = (GridInventory)InventoryManager.Main;
grid.Resize(grid.Columns + 1, grid.Rows + 1);
grid.Organize();
```

**4. Initialize from your GameManager:**

```csharp
public InventoryData InventoryData;

private void Start() => InventoryManager.Initialize(InventoryData);
private void OnDestroy() => InventoryManager.Shutdown();
```

## Collecting

A `Collectible` gives its item to the inventory when `Collect()` is called:

- **With InteractionSystem:** drag the `Collectible` into the `Interactable`'s **On Interact** and pick `Collect`.
- **On contact:** turn on **Collect On Trigger** and set **Collector Tag** (the object needs a trigger collider).
- **From code:** `collectible.Collect()` or `collectible.CollectInto(someInventory)`.

**Amount** is how many items the object gives. If the inventory can't take everything, the object keeps
the rest and **On Inventory Full** fires.

What happens to the object itself is wired in the Inspector, so the inventory never needs to know
about pooling:

| Event | When | Typical wiring |
|---|---|---|
| **On Take** | Everything in the object went into the inventory | `GameObject > SetActive(false)` (Compile wires this by default), or `Poolable > ReleaseSelf` for pooled objects |
| **On Release** | The object was just dropped from the inventory back into the world, with its position and Amount set | Effects, sounds... |
| **On Inventory Full** | The inventory couldn't take everything | A "full" sound or message |

### Dropping items back into the world

`ItemSpawner.Spawn(item, amount, position, rotation)` (used by the UI's `ItemDropper`) spawns an item's
prefab, sets its amount and invokes its On Release. A UnityEvent can't hand back an object, so where
that object comes from is the one thing set in code: `Instantiate` by default, or anything you assign
to `ItemSpawner.SpawnOverride`. With PoolSystem, set it once from your own bootstrap code (e.g. a
GameManager), so the two systems still don't reference each other:

```csharp
ItemSpawner.SpawnOverride = item =>
    item.Prefab.TryGetComponent<Poolable>(out var poolable) && poolable.PoolType != PoolTypes.None
        ? PoolManager.Get(poolable.PoolType)
        : null;
```

## Code API

```csharp
InventoryManager.Add(ItemTypes.Apple, 5);           // returns how many fit
InventoryManager.Remove(ItemTypes.Apple, 2);        // returns how many were removed
InventoryManager.GetCount(ItemTypes.Apple);         // O(1)
InventoryManager.Contains(ItemTypes.Key);
InventoryManager.CanAdd(ItemTypes.Rifle);           // all-or-nothing check, changes nothing
InventoryManager.GetItem(ItemTypes.Apple).Icon;     // name, icon, max stack...

var inventory = InventoryManager.Main;
inventory.Slots[i];                                 // Item, Amount, IsEmpty, SpaceLeft...
inventory.Move(from, to);                           // drag & drop: merge / swap / move into empty
inventory.Move(from, to, amount);                   // move part of a stack
inventory.Split(slotIndex, amount);                 // new slot index, or -1
inventory.RemoveAt(slotIndex, amount);              // drop / use
inventory.TransferTo(chest, slotIndex);             // into another inventory
```

`Add` and `Remove` are partial: they do as much as they can and return the amount. For all-or-nothing
(e.g. crafting), check `CanAdd` / `Contains` first.

Other inventories (chests, shops) are created the same way, with their own settings:

```csharp
var chest = Inventory.Create(InventoryManager.Database, InventorySettings.WithMaxSlots(12));
```

Only `InventoryManager.Main` publishes through `EventManager`; other inventories raise the same events
as C# events (`SlotChanged`, `SlotCountChanged`, `ItemAdded`, `ItemRemoved`, `AddRejected`, `Changed`).

## UI

The inventory UI is built on [UniMVC](https://github.com/fatihgezerx/UniMVC). The setup script copies
these views into your MVC folder on its own, creating the subfolders as needed: right away if UniMVC is
already in the project when you import InventorySystem, or as soon as UniMVC is added later (by you or by
the setup dialog). They become your own project code, so edit them freely. Existing files are never
overwritten, and a view you delete isn't brought back unless InventorySystem or UniMVC is imported again.
Until EventSystem and the Input System are installed too, the views compile to nothing, so they never
cause errors. The same goes if you remove InventorySystem later and keep the views: they stay in your
project, compiled to nothing, and come back to life when InventorySystem is imported again.

| File | Base | What it does |
|---|---|---|
| `Panels/InventoryPanel` | `PanelViewBase` | The window: open / close, capacity header (`12.5 / 50` + a bar in Weight mode, `8 / 20` slots, used / total cells in Grid), shows the content panel matching the mode, drops items dragged out of the UI into the world |
| `Panels/InventoryContentPanel` | `PanelViewBase` | Base of the two below: binding, drag and drop, tooltips |
| `Panels/InventoryListPanel` | `InventoryContentPanel` | Unlimited / Weight / MaxSlot: rows of slots |
| `Panels/InventoryGridPanel` | `InventoryContentPanel` | Grid: cells, items sized to their rectangle, green / red drop preview, rotate while dragging, `Organize()` |
| `Panels/InventoryTooltipPanel` | `PanelViewBase` | Item name, stack and weight on hover |
| `Controllers/InventoryController` | `ControllerBase` | The only listener to the inventory's events; passes them on to the views |
| `Panels/InventoryNotificationPanel` | `PanelViewBase` | "+3 Apple" / "Inventory full" toasts, merged per item; shown only while a toast is on screen |
| `Panels/InventoryToastPanel` | `PanelViewBase` | One toast |
| `Buttons/InventorySlotButton` | `ButtonViewBase` | One slot: icon, amount, click / hover / drag |
| `Buttons/InventoryCloseButton` | `ButtonViewBase` | Closes the window it sits in |
| `Buttons/InventoryOrganizeButton` | `ButtonViewBase` | Packs the grid of the window it sits in; shown only in Grid mode |

**Setup:** lay out the UI yourself (by hand, or with a UI editor such as
[EasyUI](https://github.com/fatihgezerx/EasyUI)), add these views to its objects and fill in their
fields. Add a `UIManager` and the `InventoryController` to the canvas, then press **Collect From
Children** on the `UIManager` and on each panel. Call `UIManager.Initialize()` from your bootstrap code,
after `InventoryManager.Initialize`.

The grid panel uses every Image under its Cell Layer as a background cell, so cells placed there in the
editor can be seen and styled before play. At runtime it adds any missing ones from its Cell Template
(e.g. after `Resize`).

**Controls**

| Input | Action |
|---|---|
| Tab / I / gamepad Select | Open / close (project-wide action `Inventory` if you add one, or assign Toggle Action) |
| Escape | Close (`UI/Cancel`) |
| Left drag | Move the whole stack: merge into the same item, swap with another (list), move into an empty slot |
| Right drag | Move half the stack |
| R while dragging | Rotate the item (Grid; project-wide action `RotateItem` if you add one) |
| Organize button | Pack the grid tightly (Grid) |
| Drag onto another window | Transfer (e.g. player <-> chest) |
| Drag out of the UI | Drop into the world, in front of the `Player` (`ItemSpawner`) |

While a window is open, the cursor is freed and the `Player` action map is paused (except the toggle).
Content panels only listen while visible and redraw only the slots that change.

For a chest, add a second `InventoryPanel`, turn off **Bind To Main Inventory** and **Toggle With
Input**, and call `chestPanel.Open(chestInventory)`. `InventoryContentPanel.SlotClicked` (panel, slot index,
button) is the hook for "use" / "equip".

## Localization

With [LocalizationSystem](https://github.com/fatihgezerx/LocalizationSystem) in the project, every item's
**Name** and **Description** become translatable: **Sync Project** in LocalizationSystem's Language Data
window finds them in the InventoryData and adds each one as a row. The UI's tooltip and pickup toasts
then show item names in the current language. In your own code, show
`LocalizationRuntime.Get(item.DisplayName)` / `LocalizationRuntime.Get(item.Description)`. The Name is
still what Compile turns into the `ItemTypes` member, so translating it never changes your code. When the
language changes, `InventoryController` resizes the open inventory window, tooltip and notifications to
their translated texts in the same frame (UniMVC's `RebuildLayoutLater`). Hidden ones are resized when
they are shown, if their **Rebuild Layout On Show** is ticked.

It is optional: without LocalizationSystem, InventorySystem compiles and shows the Names as written.
Install it later and they become translatable on their own, no change needed. The UI labels filled by
code (the capacity counter, the tooltip's name and details) keep themselves marked with
`ExcludeFromLocalization`, so Sync Project never overwrites them.

## Events

`InventoryManager` publishes every change of its Main inventory through `EventManager`. Every event is a
`readonly struct` carrying its `Inventory`, so raising them never allocates:

| Event | When | Fields |
|---|---|---|
| `SlotChangedEvent` | A slot's contents changed | `SlotIndex`, `Slot` |
| `SlotCountChangedEvent` | Slots were added or trimmed (not in MaxSlot) | `Count` |
| `ItemAddedEvent` | Items were added | `Item`, `Amount` added |
| `ItemRemovedEvent` | Items were removed | `Item`, `Amount` removed |
| `InventoryFullEvent` | An add didn't fully fit | `Item`, `Amount` that didn't fit |
| `InventoryChangedEvent` | Anything changed; once per call, after the others | - |

```csharp
private void OnEnable()  => EventManager.Register<ItemAddedEvent>(OnItemAdded);
private void OnDisable() => EventManager.Unregister<ItemAddedEvent>(OnItemAdded);

private void OnItemAdded(ItemAddedEvent e) => Debug.Log($"+{e.Amount} {e.Item.DisplayName}");
```

Other inventories (chests...) raise the same changes as C# events on the `Inventory` itself
(`SlotChanged`, `SlotCountChanged`, `ItemAdded`, `ItemRemoved`, `AddRejected`, `Changed`).

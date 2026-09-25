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

Importing InventorySystem never breaks your project. A small setup script checks for these, leaves
InventorySystem out of compilation while EventSystem is missing, and offers to install what's missing
(**Tools > Inventory System > Install MVC Scripts** offers UniMVC again). EventSystem and UniMVC are downloaded
into `Assets/Scripts/EventSystem/` and `Assets/Scripts/MVC/`, exactly as if you had copied them there. InventorySystem itself contains no
UI code and no code of any other system: pooling, interaction and UI are all wired from the outside.

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
| `Grid` | Columns, Rows | a Columns x Rows grid; every item covers the cells of its Shape |

**2. Add your items to groups.** Group names only organize the Inspector. Every item has:

| Field | Meaning |
|---|---|
| Icon | 50x50 sprite, shown on the left |
| Name | Name for UI. Compile turns it into its `ItemTypes` member (`Health Potion` → `ItemTypes.HealthPotion`), so it must be unique |
| Prefab | The world object that gives this item (optional) |
| Stackable / Max Stack | Stackable items pile up in one slot up to Max Stack, then open a new slot. Other items (e.g. a weapon) take a new slot every time |
| Weight | **Weight mode only.** Weight of one item |
| Shape / Can Rotate | **Grid mode only.** `Square 1x1`, `Rectangle 2x1`, `L Shape` (3 cells), `Rectangle 3x1`, `Square 2x2`. With Can Rotate, the item may be turned 90° to fit |

**3. Click Compile.** It:
- generates the `ItemTypes` enum; each member's value is the item's stable id, so reordering or deleting
  items never shifts an `ItemTypes` already saved in a scene or prefab,
- adds a `Collectible` component to every item's prefab (only if missing) and assigns its item.

Prefabs that are already up to date are skipped, and `ItemTypes.cs` is only rewritten when it changes.

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

The inventory UI is built on [UniMVC](https://github.com/fatihgezerx/UniMVC). When UniMVC and the Input
System are installed, the setup script copies these views into your MVC folder, creating the subfolders
as needed. They become your own project code, so edit them freely. Existing files are never
overwritten, and **Tools > Inventory System > Install MVC Scripts** adds any missing ones again.

| File | Base | What it does |
|---|---|---|
| `Panels/InventoryPanel` | `PanelViewBase` | The window: open / close, capacity header (`12.5 / 50` + a bar in Weight mode, `8 / 20` slots, used / total cells in Grid), shows the content panel matching the mode, drops items dragged out of the UI into the world |
| `Panels/InventoryContentPanel` | `PanelViewBase` | Base of the two below: binding, drag and drop, tooltips |
| `Panels/InventoryListPanel` | `InventoryContentPanel` | Unlimited / Weight / MaxSlot: rows of slots |
| `Panels/InventoryGridPanel` | `InventoryContentPanel` | Grid: cells, items sized to their shape, green / red drop preview, rotate while dragging |
| `Panels/InventoryTooltipPanel` | `PanelViewBase` | Item name, stack and weight on hover |
| `Panels/InventoryNotificationPanel` | `PanelViewBase` | "+3 Apple" / "Inventory full" toasts, merged per item |
| `Panels/InventoryToastPanel` | `PanelViewBase` | One toast |
| `Buttons/InventorySlotButton` | `ButtonViewBase` | One slot: icon, amount, click / hover / drag |
| `Buttons/InventoryCloseButton` | `ButtonViewBase` | Closes the window it sits in |
| `Editor/InventoryUIBuilder` | - | `GameObject > UI > Inventory System` menu |

**Setup:** right-click the Canvas, then **UI > Inventory System > Inventory Window** (and
**Inventory Notifications**). This builds everything fully wired, adds a `UIManager` to the canvas if it
has none, and lists the new panels in it (and the window's own views in the window). Call
`UIManager.Initialize()` from your bootstrap code, after `InventoryManager.Initialize`.

**Controls**

| Input | Action |
|---|---|
| Tab / I / gamepad Select | Open / close (project-wide action `Inventory` if you add one, or assign Toggle Action) |
| Escape | Close (`UI/Cancel`) |
| Left drag | Move the whole stack: merge into the same item, swap with another (list), move into an empty slot |
| Right drag | Move half the stack |
| R while dragging | Rotate the item (Grid; project-wide action `RotateItem` if you add one) |
| Drag onto another window | Transfer (e.g. player <-> chest) |
| Drag out of the UI | Drop into the world, in front of the `Player` (`ItemSpawner`) |

While a window is open, the cursor is freed and the `Player` action map is paused (except the toggle).
Content panels only listen while visible and redraw only the slots that change.

For a chest, add a second `InventoryPanel`, turn off **Bind To Main Inventory** and **Toggle With
Input**, and call `chestPanel.Open(chestInventory)`. `InventoryContentPanel.SlotClicked` (panel, slot index,
button) is the hook for "use" / "equip".

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

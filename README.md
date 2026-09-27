# InventorySystem

Data-driven inventory system for Unity with four capacity modes: **Unlimited**, **Weight**, **MaxSlot**
and **Grid**. Every change is published through **EventSystem**, so a UI only listens and never polls.
With **UniMVC**, a ready-made inventory UI is added to your project (see [UI](#ui)), and with **EasyUI**
you design it there and it is set up by roles (see [Building the UI with EasyUI](#building-the-ui-with-easyui)).

## Requirements

| Dependency | Required | Why |
|---|---|---|
| [EventSystem](https://github.com/fatihgezerx/EventSystem) | Yes | Publishes the inventory's events |
| [UniMVC](https://github.com/fatihgezerx/UniMVC) | For the UI | The inventory UI is built from UniMVC views |
| Input System (`com.unity.inputsystem`) | For the UI | Open / close key, rotating items |
| [LocalizationSystem](https://github.com/fatihgezerx/LocalizationSystem) | No | Translating item names and descriptions (see [Localization](#localization)) |
| [EasyUI](https://github.com/fatihgezerx/EasyUI) | No | Designing the inventory UI in EasyUI, set up by roles (see [Building the UI with EasyUI](#building-the-ui-with-easyui)) |
| [BasicTooltip](https://github.com/fatihgezerx/BasicTooltip) | No | Item tooltips on hover (without it, there are none) |
| [PoolSystem](https://github.com/fatihgezerx/PoolSystem) | No | Taking dropped and examined items from pools instead of instantiating them (see [Dropping items back into the world](#dropping-items-back-into-the-world)) |

Importing InventorySystem never breaks your project. A small setup script checks for these, leaves
InventorySystem out of compilation while EventSystem is missing, and offers to install what's missing
(if you pick **Not now**, it asks again in the next editor session or when InventorySystem is imported again). EventSystem and UniMVC are downloaded
into `Assets/Scripts/EventSystem/` and `Assets/Scripts/MVC/`, exactly as if you had copied them there. InventorySystem itself contains no UI code;
the optional systems are used only while they are in the project, each behind its own scripting define symbol.

## Installation

Clone or download this repository and copy it into a folder under `Assets/` (e.g.
`Assets/Scripts/InventorySystem/`). **Compile** writes the generated `ItemTypes.cs` into the system's own
`Runtime/Generated/` folder.

## Setup

**1. Create the data asset** via `Create > Inventory System > Inventory Data`. Its Inspector has
**INVENTORY SETTINGS**, then **ITEMS SETTINGS**. The UI is designed in EasyUI (see
[Building the UI with EasyUI](#building-the-ui-with-easyui)).

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

What happens to the object itself is wired in the Inspector:

| Event | When | Typical wiring |
|---|---|---|
| **On Take** | Everything in the object went into the inventory | `GameObject > SetActive(false)` (Compile wires this by default). With PoolSystem, an object taken from a pool also goes back to it by itself - don't wire `Poolable > ReleaseSelf` too |
| **On Release** | The object was just dropped from the inventory back into the world, with its position and Amount set | Effects, sounds... |
| **On Inventory Full** | The inventory couldn't take everything | A "full" sound or message |

### Dropping items back into the world

`ItemSpawner.Spawn(item, amount, position, rotation)` (used when an item is dropped from the UI) spawns
an item's prefab, sets its amount and invokes its On Release. The object comes from:

1. `ItemSpawner.SpawnOverride`, if you set one (e.g. a pool of your own);
2. with [PoolSystem](https://github.com/fatihgezerx/PoolSystem) in the project, the pool the prefab was
   compiled into - add the item prefabs to a PoolData and press its Compile, and call
   `PoolManager.Initialize` before items are dropped;
3. otherwise `Instantiate`.

A pooled object goes back to its pool by itself when it is collected again. Objects placed in the
scene by hand aren't from a pool, so they only do what On Take says. The Examine View takes its model
from the same pools (see [Examine view](#examine-view)).

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
| `Panels/InventoryContentPanel` | `PanelViewBase` | Base of the two below: binding, drag and drop, tooltips (with BasicTooltip) |
| `Panels/InventoryListPanel` | `InventoryContentPanel` | Unlimited / Weight / MaxSlot: rows of slots |
| `Panels/InventoryGridPanel` | `InventoryContentPanel` | Grid: cells, items sized to their rectangle, green / red drop preview, rotate while dragging, `Organize()` |
| `Controllers/InventoryController` | `ControllerBase` | The only listener to the inventory's events; passes them on to the views |
| `Panels/InventoryNotificationPanel` | `PanelViewBase` | "+3 Apple" / "Inventory full" toasts, merged per item; shown only while a toast is on screen |
| `Panels/InventoryToastPanel` | `PanelViewBase` | One toast |
| `Buttons/InventorySlotButton` | `ButtonViewBase` | One slot: icon, amount, click / hover / drag |
| `Buttons/InventoryCloseButton` | `ButtonViewBase` | Closes the window it sits in |
| `Buttons/InventoryOrganizeButton` | `ButtonViewBase` | Packs the grid of the window it sits in; shown only in Grid mode |
| `Panels/InventoryDetailsPanel` | `PanelViewBase` | The item clicked: name, description, icon and examine view; follows its slot, empties with it |
| `Panels/InventoryExamineView` | - | On a Raw Image: the item's 3D model, rendered by a camera of its own and turned by dragging on it |
| `Buttons/InventoryDropButton` | `ButtonViewBase` | Drops the selected item into the world; can't be pressed while nothing is selected |
| `Buttons/InventoryUseButton` | `ButtonViewBase` | Raises the window's `UseRequested` for the selected item (nothing happens while nothing listens); can't be pressed while nothing is selected |
| `Panels/Editor/InventoryUIBinder` | - | With EasyUI: sets up a panel built with Inventory roles (not in `MVC/Editor/`: that folder is UniMVC's own editor assembly, which can't see your views) |

**Setup by hand:** lay out the UI yourself, add these views to its objects and fill in their fields. Add
a `UIManager` and the `InventoryController` to the canvas, then press **Collect From Children** on the
`UIManager` and on each panel. Or let EasyUI do all of it (see
[Building the UI with EasyUI](#building-the-ui-with-easyui)).

The grid panel uses every Image under its Cell Layer as a background cell, so cells placed there in the
editor can be seen and styled before play. At runtime it adds any missing ones from its Cell Template
(e.g. after `Resize`).

### Building the UI with EasyUI

With [EasyUI](https://github.com/fatihgezerx/EasyUI) in the project too, the inventory UI is designed there and
set up when it is built - nothing to wire by hand:

1. Design the inventory window in EasyUI (`Tools > Easy UI`). Its boxes (header, details...) are yours to lay
   out.
2. Mark what does something with **Inventory roles**: select an element and pick **Add Role > Inventory** at
   the top of EasyUI's inspector. An element can have several roles. The menu lists only the roles that fit
   the element - text roles on a Text, image roles on an Image, button roles on a Button, the Weight Bar on a
   Slider, the Examine View on a Raw Image, the boxes' roles on an Empty or an Image (a box with a background is
   an Image). Names don't matter: elements are found by role only.
3. Save, then build it into the scene with **GameObject > UI (Canvas) > Easy UI > your panel**.

| Role | On | Needed | Becomes |
|---|---|---|---|
| Inventory Panel | Empty, Image (usually the panel's root) | Yes | The window (`InventoryPanel`): opens and closes whole, blocks gameplay while open |
| Slot Container (List) / (Grid) | Empty, Image | Yes, one of them | The content panel, where the slots are laid out, e.g. a Scroll View's Content. (List): Unlimited, Weight, Max Slot, laid out by its Grid, Horizontal or Vertical Layout Group. (Grid): the grid itself |
| Slot Template | Empty, Image, Button | Yes | One slot (`InventorySlotButton`), copied for every slot (Grid: every item); hidden itself |
| Clickable Slot | next to Slot Template | No | A click on an item - or the start of a drag - selects it and shows it in the details |
| Draggable Slot | next to Slot Template | No | Items can be dragged: moved, merged, split, swapped and carried to another window |
| Drop To World | next to Slot Template and Draggable Slot | No | An item dragged out of the UI is dropped in front of the player. The Drop button drops either way |
| Tooltip > Shows Tooltip | next to Slot Template (BasicTooltip's role) | No | Hovering an item shows its name, description and icon in BasicTooltip's tooltip, designed in this panel or any other: every slot gives its item's (`ITooltipSource`) |
| Item Icon | Image | No | An item's icon. **Where it sits says which item**: inside the Slot Template the slot's (made when missing), inside the toast box the notification's, anywhere else the item clicked. Several elements can have it |
| Slot Amount | Text | No | Inside the Slot Template: the stack's amount; made when missing. It may sit inside the Item Icon (to lie over it): it is moved up to the slot, so it doesn't turn with a rotated grid item |
| Cell Template | Image | No | Grid: one empty background cell, copied for every cell (the items lie over the cells); hidden itself. Without it, cells are made from the Slot Template's look |
| Capacity Text | Text | No | "12.5 / 50", "8 / 20" or used / total cells, whatever the inventory's type |
| Weight Bar | Slider | No | Weight: set to total / max weight (the player can't drag it); hidden in other types |
| Close Button / Organize Button | Button | No | `InventoryCloseButton` / `InventoryOrganizeButton` (Grid) |
| Item Name / Item Description | Text | No | The item clicked |
| Examine View | Raw Image | No | `InventoryExamineView`: the item clicked in 3D |
| Drop Button | Button | No | `InventoryDropButton`: drops the selected item into the world |
| Use Button | Button | No | `InventoryUseButton`: raises the window's `UseRequested` (inventory, slot) for the selected item, for your code to use it; does nothing by itself yet |
| Toast Message | Text | No | A notification. **The box holding it is the toast template** (put an Item Icon next to it for the item's icon), copied for every notification; **its parent stacks them** (`InventoryNotificationPanel`) |

The texts filled by code (Slot Amount, Capacity Text, Item Name, Item Description, Toast Message) can't also be
LocalizationSystem's *Localized Text*: they are shown translated already.

When the panel is built:

- **Grid** (Slot Container (Grid)): an `InventoryGridPanel` whose cells are made right away from the
  InventoryData's Columns x Rows (the project's first InventoryData), sized by the container's Grid Layout
  Group (cell size and spacing; the group itself is then removed, since the panel places cells itself) or else
  by the Cell Template. The container gets a Layout Element the panel sizes to the whole grid, so a Content
  Size Fitter on it (or a layout group around it) fits the grid. At runtime the grid follows the inventory it
  shows. **List**: an `InventoryListPanel` laid out by the container's layout group, as designed - a **Grid**
  Layout Group gives rows of as many columns as fit, a **Horizontal** / **Vertical** one a single row / column
  (e.g. a hotbar scrolling sideways); without one, a Grid Layout Group at the Slot Template's size. Empty
  placeholder slots fill what is seen (the Scroll View's viewport). In a Scroll View without a Content Size
  Fitter, the Content gets one so it grows with its slots, along its row or column.
- The item clicked is shown by an `InventoryDetailsPanel` on an invisible object in the window, wherever Item
  Name, Description, (its) Item Icon and Examine View are.
- The notifications' box is taken out of the window to the canvas, so they show while the inventory is closed;
  it gets a Vertical Layout Group to stack them if it has no layout group.
- The canvas gets a `UIManager` and an `InventoryController` if it has none (and the scene an EventSystem), and
  every view is listed in the panel it sits in, or in the `UIManager`, which initializes them all in its
  `Start`. Templates aren't listed: their copies are set up by their panels.
- It is one undo step. A panel with inventory roles but no Inventory Panel, Slot Container or Slot Template is
  built without the inventory's views, and the Console says what is missing.

### Examine view

The Examine View shows the selected item's prefab in 3D. With PoolSystem and the prefab in a pool, the model is
taken from that pool - nothing is instantiated - with its scripts, colliders and physics switched off while it
is shown; it is put back as it was, and returned, when another item (or none) is shown. Otherwise a copy with
only its meshes and renderers (none of its scripts, colliders or rigidbodies ever run) is made once per item and
kept for the next time. The model sits far from the scene on its own layer (**Layer**, 31 by default: pick one
nothing else uses). A camera of its own renders only that layer into a
render texture (**Resolution** x Resolution, 1024 by default) shown on the Raw Image, and only while an item
is shown and the window is open. Drag on the image with the left button to turn the model: right turns its
front to the right, up tips it up. **Start Rotation**, **Field Of View**, **Framing**, **Background** and
**Light Intensity** are on the component too.

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
| Click an item | Select it: shown by Item Name, Item Description, Item Icon and the Examine View |
| Drag on the Examine View | Turn the selected item's model |

While a window is open, the cursor is freed and the `Player` action map is paused (except the toggle).
Opening a window with details selects its first item (in a grid, the top-left one), so its name, description
and examine view show right away; an empty inventory selects nothing. When the language changes, the selected
item's name and description follow at once.
Content panels only listen while visible and redraw only the slots that change.

`InventoryPanel` shows none of its fields in the Inspector: its parts and the role-driven behaviour (Select
On Click from Clickable Slot, Drop Outside from Drop To World) are set when the panel is built, and the rest
keep defaults that fit the player's inventory - toggled with the Inventory action (or Tab / I), closed with
Escape, cursor freed and the `Player` action map paused while open, drops landing 1.5 m in front of the
`Player`. To change one, switch the Inspector to **Debug** mode.

For a chest, add a second `InventoryPanel`, turn off **Bind To Main Inventory** and **Toggle With
Input** (Debug Inspector), and call `chestPanel.Open(chestInventory)`. `InventoryContentPanel.SlotClicked` (panel, slot index,
button) is the hook for "use" / "equip".

## Localization

With [LocalizationSystem](https://github.com/fatihgezerx/LocalizationSystem) in the project, every item's
**Name** and **Description** become translatable: **Sync Project** in LocalizationSystem's Language Data
window finds them in the InventoryData and adds each one as a row. The UI's tooltips and pickup toasts
then show item names in the current language. In your own code, show
`LocalizationRuntime.Get(item.DisplayName)` / `LocalizationRuntime.Get(item.Description)`. The Name is
still what Compile turns into the `ItemTypes` member, so translating it never changes your code. When the
language changes, `InventoryController` resizes the open inventory window and notifications to
their translated texts in the same frame (UniMVC's `RebuildLayoutLater`). Hidden ones are resized when
they are shown, if their **Rebuild Layout On Show** is ticked.

It is optional: without LocalizationSystem, InventorySystem compiles and shows the Names as written.
Install it later and they become translatable on their own, no change needed. The UI labels filled by
code (the capacity counter, the item's name and description) tell Sync Project so (LocalizationSystem's
`ILocalizedByCode`): it never puts a `LocalizedText` on them, and nothing is added to their objects.

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

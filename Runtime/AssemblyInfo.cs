using System.Runtime.CompilerServices;

// Lets InventoryData's Compile step write Collectible.itemType and assign item ids without exposing
// public setters that gameplay code could use to change what an object gives at runtime.
[assembly: InternalsVisibleTo("InventorySystem.Editor")]

#if HAS_VENEER
using Veneer;
using UnityEditor;

namespace InventorySystem
{
    /// <summary>
    /// Inspector of <see cref="InventoryData"/> in the studio look, drawn from a Veneer layout: the project's own copy made in the Inspector
    /// Designer when there is one, otherwise <see cref="InventoryDataDefaultLayout"/>. Under the header (counts and a compile-state pill)
    /// sit the prominent Compile card and the validation hint rows; two tabs follow - Items (the items of one group as rows with an icon
    /// slot, details one click away) and Groups (reorderable groups with the capacity settings of each). The pill, the Compile card, the
    /// hints and the two panes are custom items (<see cref="InventoryDataItems"/>); the Compile step itself is
    /// <see cref="InventoryCompiler"/>, unchanged. The UI is designed in Easy UI and set up by roles (see InventoryRoles.cs). A selection of
    /// several assets only shows a header and a hint.
    /// </summary>
    [CustomEditor(typeof(InventoryData))]
    internal sealed class InventoryDataStudioEditor : VeneerLayoutDrivenInspector<InventoryData>
    {
        /// <inheritdoc />
        protected override VeneerBrand Brand => InventoryBrand.Value;

        /// <inheritdoc />
        protected override bool BuildMultiSelection(VeneerInspectorBuilder b)
        {
            b.Header("Inventory Data", "Several assets selected");
            b.Hint("Select a single Inventory Data asset to edit it.", "info");
            return true;
        }
    }
}
#endif

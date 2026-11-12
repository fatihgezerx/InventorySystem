#if HAS_VENEER
using Veneer;
using UnityEditor;

namespace InventorySystem
{
    /// <summary>
    /// Inspector of <see cref="GoldCollectible"/> in the studio look, drawn from a Veneer layout: the project's own copy made in the
    /// Inspector Designer when there is one, otherwise <see cref="GoldCollectibleDefaultLayout"/>. Gold tab: a Random toggle, then the fixed
    /// amount or - with Random on - the Min / Max slider. Events tab: On Collected. Works with several objects selected.
    /// </summary>
    [CustomEditor(typeof(GoldCollectible))]
    [CanEditMultipleObjects]
    internal sealed class GoldCollectibleStudioEditor : VeneerLayoutDrivenInspector<GoldCollectible>
    {
        /// <inheritdoc />
        protected override VeneerBrand Brand => InventoryBrand.Value;
    }
}
#endif

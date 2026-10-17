#if HAS_VENEER
using Veneer;
using UnityEngine;

namespace InventorySystem
{
    /// <summary>
    /// The Inventory System's look in the studio editor kit: green accent (#30A46C, exact through the
    /// <c>InventoryBrand.uss</c> sheet), its logo and banner from <c>Editor/Brand/</c>. One cached instance.
    /// </summary>
    internal static class InventoryBrand
    {
        private const string BrandFolder = "Assets/Scripts/InventorySystem/Editor/Brand/";

        /// <summary>The brand every Inventory System Inspector uses.</summary>
        public static readonly VeneerBrand Value = VeneerBrand.FromAssets(
            name: "Inventory System",
            subtitle: "Items, groups and capacity",
            accent: new Color32(0x30, 0xA4, 0x6C, 255),
            logoPath: BrandFolder + "logo.png",
            bannerPath: BrandFolder + "banner.png",
            styleSheetPath: BrandFolder + "InventoryBrand.uss");
    }
}
#endif

namespace InventorySystem
{
    /// <summary>
    /// How an item's numbers read in a UI: the text a list's Weight and Value columns and the item details show.
    /// A number that isn't set (0) reads as a dash, as a list row without weight or worth does in most games.
    /// </summary>
    public static class ItemText
    {
        /// <summary>What an unset number reads as.</summary>
        public const string Unset = "-";

        /// <summary>A weight: up to two decimals ("0.25", "1", "12.5"), a dash for 0.</summary>
        public static string Weight(float weight) => weight > 0f ? weight.ToString("0.##") : Unset;

        /// <summary>A value: the whole number, a dash for 0.</summary>
        public static string Value(int value) => value > 0 ? value.ToString() : Unset;
    }
}

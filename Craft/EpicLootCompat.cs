using UnityEngine;

namespace StoreAndCraft
{
    /// <summary>
    /// Soft Epic Loot compatibility (no hard DLL reference): name patterns for pieces
    /// and materials so chest-pull / displays work alongside RandyKnapp.EpicLoot.
    /// </summary>
    internal static class EpicLootCompat
    {
        public static bool IsEnchantingPiece(Piece piece)
        {
            if (piece == null)
                return false;
            return NameLooksEnchanting(piece.gameObject != null ? piece.gameObject.name : null)
                || NameLooksEnchanting(piece.m_name);
        }

        public static bool IsEnchantingStation(CraftingStation station)
        {
            if (station == null)
                return false;
            string n = station.gameObject != null ? station.gameObject.name : null;
            if (NameLooksEnchanting(n))
                return true;
            return NameLooksEnchanting(station.m_name);
        }

        public static bool NameLooksEnchanting(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return false;
            string s = raw.ToLowerInvariant();
            return s.Contains("enchant") || s.Contains("augment") || s.Contains("epicloot");
        }
    }
}

using UnityEngine;

namespace StoreAndCraft
{
    internal static class ChestNames
    {
        public const string ZdoKey = "kac_label";
        public const int MaxLength = 32;
        public const string IgnorePrefix = "[I]";

        public static bool IsIgnored(Container container)
        {
            return IsIgnoredName(Get(container));
        }

        public static bool IsIgnoredName(string stored)
        {
            if (string.IsNullOrEmpty(stored))
                return false;
            string t = stored.TrimStart();
            // Only the ignore tag "[I]", not every renamed chest.
            return t.StartsWith(IgnorePrefix, System.StringComparison.OrdinalIgnoreCase);
        }

        public static string DisplayName(string stored)
        {
            if (string.IsNullOrEmpty(stored))
                return stored;
            string t = stored.Trim();
            if (t.StartsWith(IgnorePrefix, System.StringComparison.OrdinalIgnoreCase))
                t = t.Substring(IgnorePrefix.Length).TrimStart();
            return t;
        }

        public static bool CanRename(Container container)
        {
            ZNetView nv = Refs.View(container);
            return nv != null && nv.IsValid() && nv.GetZDO() != null;
        }

        public static string Get(Container container)
        {
            ZNetView nv = Refs.View(container);
            if (nv == null || !nv.IsValid() || nv.GetZDO() == null)
                return null;

            string name = nv.GetZDO().GetString(ZdoKey, string.Empty);
            return string.IsNullOrEmpty(name) ? null : name;
        }

        public static bool Set(Container container, string name)
        {
            ZNetView nv = Refs.View(container);
            if (nv == null || !nv.IsValid() || nv.GetZDO() == null)
                return false;

            if (!nv.IsOwner())
                nv.ClaimOwnership();

            ContainerFilter.RefreshInventory(container);
            Inventory inv = container.GetInventory();
            if (inv != null && inv.NrOfItems() > 0)
                ContainerFilter.SaveInventory(container);

            nv.GetZDO().Set(ZdoKey, Sanitize(name));
            return true;
        }

        public static string Sanitize(string name)
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;

            name = name.Trim();
            if (name.Length > MaxLength)
                name = name.Substring(0, MaxLength);
            return name;
        }

        public static Container Hovered()
        {
            Player player = Player.m_localPlayer;
            if (player == null)
                return null;

            GameObject hover = player.GetHoverObject();
            Container fromHover = hover != null ? hover.GetComponentInParent<Container>() : null;
            if (fromHover != null)
                return fromHover;

            Piece piece = player.GetHoveringPiece();
            return piece != null ? piece.GetComponent<Container>() : null;
        }
    }

    internal class ChestRenameReceiver : MonoBehaviour, TextReceiver
    {
        public string GetText()
        {
            return ChestNames.Get(GetComponent<Container>()) ?? string.Empty;
        }

        public void SetText(string text)
        {
            Container container = GetComponent<Container>();
            if (container == null)
                return;

            if (!PrivateArea.CheckAccess(container.transform.position, 0f, false, true))
                return;

            ChestNames.Set(container, text);
        }
    }
}

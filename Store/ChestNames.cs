using UnityEngine;

namespace StoreAndCraft
{
    internal static class ChestNames
    {
        public const string ZdoKey = "kac_label";
        public const int MaxLength = 32;
        /// <summary>Fully ignored: dump, craft, displays, search.</summary>
        public const string IgnorePrefix = "[I]";
        /// <summary>Hidden from dump/store/craft pull; still counted on Storage Displays.</summary>
        public const string HiddenPrefix = "[H]";

        /// <summary>Dump / auto-store / craft / auto-fill / build-grab skip these ([I] or [H]).</summary>
        public static bool IsIgnored(Container container)
        {
            return IsIgnoredName(Get(container));
        }

        public static bool IsIgnoredName(string stored)
        {
            return IsFullyIgnoredName(stored) || IsHiddenName(stored);
        }

        /// <summary>Only [I] — excluded from displays and search.</summary>
        public static bool IsFullyIgnored(Container container)
        {
            return IsFullyIgnoredName(Get(container));
        }

        public static bool IsFullyIgnoredName(string stored)
        {
            if (string.IsNullOrEmpty(stored))
                return false;
            string t = stored.TrimStart();
            return t.StartsWith(IgnorePrefix, System.StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Only [H] — store/craft ignore, displays still count.</summary>
        public static bool IsHidden(Container container)
        {
            return IsHiddenName(Get(container));
        }

        public static bool IsHiddenName(string stored)
        {
            if (string.IsNullOrEmpty(stored) || IsFullyIgnoredName(stored))
                return false;
            string t = stored.TrimStart();
            return t.StartsWith(HiddenPrefix, System.StringComparison.OrdinalIgnoreCase);
        }

        public static string DisplayName(string stored)
        {
            if (string.IsNullOrEmpty(stored))
                return stored;
            string t = stored.Trim();
            if (t.StartsWith(IgnorePrefix, System.StringComparison.OrdinalIgnoreCase))
                t = t.Substring(IgnorePrefix.Length).TrimStart();
            else if (t.StartsWith(HiddenPrefix, System.StringComparison.OrdinalIgnoreCase))
                t = t.Substring(HiddenPrefix.Length).TrimStart();
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

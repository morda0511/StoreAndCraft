using UnityEngine;

namespace StoreAndCraft
{
    internal static class ChestNames
    {
        public const string ZdoKey = "kac_label";
        public const int MaxLength = 32;

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

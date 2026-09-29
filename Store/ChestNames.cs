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
        /// <summary>
        /// Manual fill: dump / ground intake / auto-store skip depositing into this chest.
        /// Station pull, craft pull, and [lN] links still work. Combines with links: [M] [l3] Wood.
        /// Type the prefix in the chest name (no extra toggle). Not compatible with [I]/[H].
        /// </summary>
        public const string ManualPrefix = "[M]";

        /// <summary>Dump / craft / auto-fill / build-grab skip these entirely ([I] or [H]). Not [M].</summary>
        public static bool IsIgnored(Container container)
        {
            return IsIgnoredName(Get(container));
        }

        public static bool IsIgnoredName(string stored)
        {
            return IsFullyIgnoredName(stored) || IsHiddenName(stored);
        }

        /// <summary>Manual-fill chests: no automated deposit in; station/craft pull still allowed.</summary>
        public static bool IsManualFill(Container container)
        {
            return IsManualFillName(Get(container));
        }

        public static bool IsManualFillName(string stored)
        {
            if (string.IsNullOrEmpty(stored) || IsFullyIgnoredName(stored) || IsHiddenName(stored))
                return false;
            string t = stored.TrimStart();
            return t.StartsWith(ManualPrefix, System.StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>True when dump / intake / auto-store / station output must not put items in.</summary>
        public static bool BlocksDeposit(Container container)
        {
            return BlocksDepositName(Get(container));
        }

        public static bool BlocksDepositName(string stored)
        {
            return IsIgnoredName(stored) || IsManualFillName(stored);
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
            else if (t.StartsWith(ManualPrefix, System.StringComparison.OrdinalIgnoreCase))
                t = t.Substring(ManualPrefix.Length).TrimStart();
            // Hide [l1]…[l9] / legacy [linkN] like [I]/[H]/[M] — hover shows link/status separately.
            t = StationLink.StripTags(t).Trim();
            return t;
        }

        /// <summary>Visible label for hover / rename body, falling back to vanilla chest name.</summary>
        public static string ResolveDisplayName(Container container, string stored)
        {
            string shown = DisplayName(stored);
            if (!string.IsNullOrEmpty(shown))
                return shown;
            return LocalizedVanillaName(container);
        }

        public static string LocalizedVanillaName(Container container)
        {
            string vanilla = Refs.VanillaHoverName(container);
            if (string.IsNullOrEmpty(vanilla))
                return string.Empty;
            if (Localization.instance != null)
                vanilla = Localization.instance.Localize(vanilla);
            return vanilla ?? string.Empty;
        }

        /// <summary>
        /// Apply ignore mode to a stored chest name. Keeps [lN] and the rest.
        /// ignore off → no prefix; ignore on + showOnDisplay → [H]; ignore on alone → [I].
        /// When enabling a flag with an empty body, uses fallbackName (vanilla) so OK never saves bare [I].
        /// </summary>
        public static string ApplyIgnoreFlags(string stored, bool ignore, bool showOnDisplay, string fallbackName = null)
        {
            // Clear [I]/[H] only. Keep [M] when ignore is turned off (link + manual fill stay together).
            string work = StripIgnorePrefix(stored);
            if (ignore)
            {
                // [I]/[H] replace manual fill — incompatible modes.
                work = StripManualPrefix(work);
                string body = StationLink.StripTags(work).Trim();
                if (string.IsNullOrEmpty(body) && !string.IsNullOrEmpty(fallbackName))
                {
                    int link = StationLink.ParseFromName(work);
                    work = link > 0
                        ? (StationLink.Tag(link) + " " + fallbackName.Trim()).Trim()
                        : fallbackName.Trim();
                }
            }

            if (!ignore)
                return work.Trim();
            if (showOnDisplay)
                return (HiddenPrefix + (string.IsNullOrEmpty(work) ? "" : " " + work)).Trim();
            return (IgnorePrefix + (string.IsNullOrEmpty(work) ? "" : " " + work)).Trim();
        }

        /// <summary>
        /// Before saving a rename: if flags/link exist but the visible body is empty, attach vanilla name.
        /// Bare empty string still clears the custom label.
        /// </summary>
        public static string FinalizeRename(Container container, string text)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(text.Trim()))
                return string.Empty;

            string body = DisplayName(text);
            if (!string.IsNullOrEmpty(body))
                return Sanitize(text);

            if (!IsIgnoredName(text) && !IsManualFillName(text) && StationLink.ParseFromName(text) <= 0)
                return Sanitize(text);

            string vanilla = LocalizedVanillaName(container);
            if (string.IsNullOrEmpty(vanilla))
                return Sanitize(text);

            bool ignore = IsIgnoredName(text);
            bool show = IsHiddenName(text);
            bool manual = IsManualFillName(text);
            int link = StationLink.ParseFromName(text);
            string work = link > 0 ? StationLink.Tag(link) + " " + vanilla : vanilla;
            if (ignore)
                work = ApplyIgnoreFlags(work, true, show);
            else if (manual)
                work = (ManualPrefix + " " + work).Trim();
            return Sanitize(work);
        }

        public static string StripIgnorePrefix(string stored)
        {
            if (string.IsNullOrEmpty(stored))
                return string.Empty;
            string t = stored.TrimStart();
            if (t.StartsWith(IgnorePrefix, System.StringComparison.OrdinalIgnoreCase))
                return t.Substring(IgnorePrefix.Length).TrimStart();
            if (t.StartsWith(HiddenPrefix, System.StringComparison.OrdinalIgnoreCase))
                return t.Substring(HiddenPrefix.Length).TrimStart();
            return t;
        }

        public static string StripManualPrefix(string stored)
        {
            if (string.IsNullOrEmpty(stored))
                return string.Empty;
            string t = stored.TrimStart();
            if (t.StartsWith(ManualPrefix, System.StringComparison.OrdinalIgnoreCase))
                return t.Substring(ManualPrefix.Length).TrimStart();
            return t;
        }

        /// <summary>Colored status lines above hover text (Ignore / Manual fill / Show on display).</summary>
        public static void PrependStatusHover(ref string text, string stored)
        {
            if (string.IsNullOrEmpty(text))
                return;

            if (IsFullyIgnoredName(stored))
            {
                text = "<color=#e74c3c>" + Loc.T("Status: Ignore", "Status: Ignorieren") + "</color>\n" + text;
                return;
            }

            if (IsHiddenName(stored))
            {
                // [H] = ignored for dump/store/craft, still on Storage Displays.
                string lines = "<color=#e74c3c>" + Loc.T("Status: Ignore", "Status: Ignorieren") + "</color>\n"
                    + "<color=#e67e22>" + Loc.T("Display ✓", "Display ✓") + "</color>";
                text = lines + "\n" + text;
                return;
            }

            if (IsManualFillName(stored))
            {
                text = "<color=#3498db>" + Loc.T("Status: Manual fill", "Status: Manuell befüllen") + "</color>\n" + text;
            }
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

            // Only touch inventory Save when Load looks consistent — never write empty over a full ZDO.
            if (ContainerFilter.TryReadyForWrite(container))
            {
                Inventory inv = container.GetInventory();
                if (inv != null && inv.NrOfItems() > 0)
                    ContainerFilter.SaveInventory(container);
            }

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

            string stored = ChestNames.FinalizeRename(container, text);
            if (ChestNames.Set(container, stored))
                ActivityLog.Note(Loc.T("Chest", "Kiste"), "\"" + ChestNames.Sanitize(stored) + "\"");
        }
    }
}

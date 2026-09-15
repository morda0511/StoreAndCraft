using HarmonyLib;

namespace StoreAndCraft
{
    [HarmonyPatch(typeof(Container), nameof(Container.Interact))]
    internal static class ContainerInteractRenamePatch
    {
        private static bool Prefix(Container __instance, Humanoid character, bool hold, bool alt, ref bool __result)
        {
            if (hold || character != Player.m_localPlayer)
                return true;

            if (!ChestRename.WantsRename(alt))
                return true;

            if (!ChestRename.TryOpen(__instance, false))
                return true;

            __result = true;
            return false;
        }

        private static void Postfix(Container __instance, bool __result)
        {
            if (!__result || __instance == null)
                return;
            NearbyIndex.MarkInventorySeen(__instance);
        }
    }

    [HarmonyPatch(typeof(Container), nameof(Container.GetHoverName))]
    internal static class ContainerHoverNamePatch
    {
        private static void Postfix(Container __instance, ref string __result)
        {
            string custom = ChestNames.Get(__instance);
            if (!string.IsNullOrEmpty(custom))
            {
                string shown = ChestNames.DisplayName(custom);
                if (string.IsNullOrEmpty(shown))
                    shown = custom;
                __result = shown;
            }
        }
    }

    [HarmonyPatch(typeof(Container), nameof(Container.GetHoverText))]
    internal static class ContainerHoverTextPatch
    {
        private static void Postfix(Container __instance, ref string __result)
        {
            if (string.IsNullOrEmpty(__result))
                return;

            string custom = ChestNames.Get(__instance);
            bool ignored = ChestNames.IsIgnoredName(custom);
            if (!string.IsNullOrEmpty(custom))
            {
                string shown = ChestNames.DisplayName(custom);
                if (string.IsNullOrEmpty(shown))
                    shown = custom;

                string vanilla = Refs.VanillaHoverName(__instance);
                if (!string.IsNullOrEmpty(vanilla) && Localization.instance != null)
                    vanilla = Localization.instance.Localize(vanilla);

                if (!string.IsNullOrEmpty(vanilla) && __result.StartsWith(vanilla))
                    __result = shown + __result.Substring(vanilla.Length);
                else if (__result.IndexOf(shown, System.StringComparison.Ordinal) < 0
                    && __result.IndexOf(custom, System.StringComparison.Ordinal) < 0)
                    __result = shown + "\n" + __result;
                else if (__result.IndexOf(custom, System.StringComparison.Ordinal) >= 0 && custom != shown)
                    __result = __result.Replace(custom, shown);
            }

            __result += "\n[<color=yellow><b>" + ChestRename.PromptLabel() + "</b></color>] Rename";
            if (ignored)
                __result = "<color=#e74c3c>" + __result + "</color>";
        }
    }
}

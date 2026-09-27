using HarmonyLib;

namespace StoreAndCraft
{
    /// <summary>
    /// Vanilla Aoe.CauseTriggerDamage logs every physics frame when a collider sits
    /// inside an AOE that has trigger damage disabled (e.g. "default spiky").
    /// That LogWarning spam can hitch the game; skip the method — vanilla only logs then returns.
    /// </summary>
    [HarmonyPatch(typeof(Aoe), "CauseTriggerDamage")]
    internal static class AoeSpamPatch
    {
        private static bool Prefix(Aoe __instance)
        {
            if (__instance == null)
                return false;
            if (!__instance.m_useTriggers)
                return false;
            return true;
        }
    }
}

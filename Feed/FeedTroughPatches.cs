using HarmonyLib;
using UnityEngine;

namespace StoreAndCraft
{
    [HarmonyPatch(typeof(MonsterAI), "FindClosestConsumableItem")]
    internal static class MonsterAIFindTroughFoodPatch
    {
        private static void Postfix(MonsterAI __instance, float maxRange, ref ItemDrop __result)
        {
            if (__instance == null)
                return;
            if (Plugin.Settings == null || !Plugin.Settings.FeedTroughEnabled.Value)
                return;
            if (FeedTrough.Live.Count == 0)
                return;

            Vector3 from = __instance.transform.position;
            float best = __result != null
                ? Vector3.Distance(from, __result.transform.position)
                : maxRange + 1f;

            ItemDrop closest = __result;
            for (int i = 0; i < FeedTrough.Live.Count; i++)
            {
                FeedTrough trough = FeedTrough.Live[i];
                if (trough == null)
                    continue;

                ItemDrop bait = trough.FindBaitFor(__instance, from, maxRange, best);
                if (bait == null)
                    continue;

                float dist = Vector3.Distance(from, bait.transform.position);
                if (dist >= best)
                    continue;

                best = dist;
                closest = bait;
            }

            __result = closest;
        }
    }

    [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.RemoveOne))]
    internal static class ItemDropTroughRemoveOnePatch
    {
        private static bool Prefix(ItemDrop __instance, ref bool __result)
        {
            if (!FeedTroughBait.IsBait(__instance))
                return true;

            FeedTrough trough = FeedTroughBait.OwnerOf(__instance);
            __result = trough != null && trough.TryAnimalEat(__instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.Interact))]
    internal static class ItemDropTroughInteractPatch
    {
        private static bool Prefix(ItemDrop __instance, ref bool __result)
        {
            if (!FeedTroughBait.IsBait(__instance))
                return true;
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.GetHoverText))]
    internal static class ItemDropTroughHoverPatch
    {
        private static bool Prefix(ItemDrop __instance, ref string __result)
        {
            if (!FeedTroughBait.IsBait(__instance))
                return true;
            __result = "";
            return false;
        }
    }
}

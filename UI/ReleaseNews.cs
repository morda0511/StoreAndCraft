using HarmonyLib;
using UnityEngine;

namespace StoreAndCraft
{
    /// <summary>
    /// After an update Hugin visits once and tells what is new in this StoreAndCraft version.
    /// Uses the vanilla tutorial path (Tutorial.m_texts + Player.ShowTutorial): the raven comes,
    /// talking to him marks the key as seen on the character, and SAC also stores the version in
    /// its local config so other characters do not get the same news again. Update the text below
    /// with every release (only NEW FEATURES, short).
    /// </summary>
    internal static class ReleaseNews
    {
        private const float DelayAfterSpawn = 8f;
        private static float _showAt;

        private static string Key => "sac_news_" + Plugin.ModVersion;

        private static string Topic => "StoreAndCraft " + Plugin.ModVersion;

        private static string Text => Loc.T(
            "New in this version:\n"
            + "- Carved storage displays with their own layouts (Shift+RMB) and hammer icons\n"
            + "- Sign displays get layouts too, columns can fill bottom-up\n"
            + "- Feed trough: animal food filter and auto-fill (Alt+E)\n"
            + "- Fermenter: mead filter and batches of the same base (Alt+E, [E] in the first minute)\n"
            + "- Linked stations show in red what their chests ran out of\n"
            + "- Station capacities in the F10 menu (admin)\n"
            + "- Filters only list items you have already discovered",
            "Neu in dieser Version:\n"
            + "- Geschnitzte Storage Displays mit eigenen Layouts (Shift+RMB) und Hammer-Icons\n"
            + "- Schild-Displays haben jetzt auch Layouts, Säulen füllen sich von unten\n"
            + "- Futtertrog: Futter-Filter und Auto-Fill (Alt+E)\n"
            + "- Fermenter: Met-Filter und Chargen derselben Basis (Alt+E, [E] in der ersten Minute)\n"
            + "- Gelinkte Stationen zeigen rot, was in ihren Kisten fehlt\n"
            + "- Stations-Maxima im F10-Menü (Admin)\n"
            + "- Filter zeigen nur Items, die du schon entdeckt hast");

        private static bool AlreadySeen()
        {
            ModConfig c = Plugin.Settings;
            return c == null || !c.ShowReleaseNews.Value || c.ReleaseNewsSeen.Value == Plugin.ModVersion;
        }

        /// <summary>Player spawned: schedule the visit a few seconds later (not during the load screen).</summary>
        internal static void OnSpawned(Player player)
        {
            if (player == null || player != Player.m_localPlayer || AlreadySeen())
                return;
            if (player.HaveSeenTutorial(Key))
            {
                MarkSeen();
                return;
            }
            _showAt = Time.time + DelayAfterSpawn;
        }

        /// <summary>Plugin.Update: one float compare while nothing is scheduled.</summary>
        internal static void Tick()
        {
            if (_showAt <= 0f || Time.time < _showAt)
                return;
            _showAt = 0f;
            Show();
        }

        private static void Show()
        {
            Player player = Player.m_localPlayer;
            if (player == null || Tutorial.instance == null || AlreadySeen())
                return;
            string key = Key;
            if (!Tutorial.instance.m_texts.Exists(t => t.m_name == key))
            {
                Tutorial.instance.m_texts.Add(new Tutorial.TutorialText
                {
                    m_name = key,
                    m_topic = Topic,
                    m_text = Text,
                    // Also listed in the compendium, readable later.
                    m_label = Topic
                });
            }
            player.ShowTutorial(key);
        }

        /// <summary>Raven.Talk postfix: the player listened → never again for this version.</summary>
        internal static void AfterRavenTalk()
        {
            Player player = Player.m_localPlayer;
            if (player != null && player.HaveSeenTutorial(Key))
                MarkSeen();
        }

        private static void MarkSeen()
        {
            if (Plugin.Settings != null && Plugin.Settings.ReleaseNewsSeen.Value != Plugin.ModVersion)
                Plugin.Settings.ReleaseNewsSeen.Value = Plugin.ModVersion;
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
    internal static class ReleaseNewsSpawnPatch
    {
        private static void Postfix(Player __instance)
        {
            ReleaseNews.OnSpawned(__instance);
        }
    }

    [HarmonyPatch(typeof(Raven), "Talk")]
    internal static class ReleaseNewsRavenTalkPatch
    {
        private static void Postfix()
        {
            ReleaseNews.AfterRavenTalk();
        }
    }
}

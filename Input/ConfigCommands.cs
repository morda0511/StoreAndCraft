using System;
using System.Globalization;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace StoreAndCraft
{
    internal static class ConfigCommands
    {
        public const string RpcAdminCmd = "KAC_AdminCmd";
        public const string RpcAdminResult = "KAC_AdminResult";
        private static bool _rpcRegistered;

        public static void RegisterRpc()
        {
            if (_rpcRegistered || ZRoutedRpc.instance == null)
                return;
            ZRoutedRpc.instance.Register<string>(RpcAdminCmd, RPC_AdminCmd);
            ZRoutedRpc.instance.Register<string>(RpcAdminResult, RPC_AdminResult);
            _rpcRegistered = true;
        }

        public static bool TryHandleChat(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            string text = raw.Trim();
            if (!text.StartsWith("/", StringComparison.Ordinal))
                return false;

            if (!IsOurCommand(text))
                return false;

            Run(text);
            return true;
        }

        public static void OnConsole(Terminal.ConsoleEventArgs args)
        {
            if (args == null || args.Length < 1)
            {
                ShowHelp();
                return;
            }

            var parts = new System.Collections.Generic.List<string>();
            for (int i = 0; i < args.Length; i++)
                parts.Add(args[i]);
            Run("/" + string.Join(" ", parts.ToArray()));
        }

        public static void ShowHelp()
        {
            foreach (string line in HelpLines())
                PrintLine(line);

            Player player = Player.m_localPlayer;
            if (player != null)
                player.Message(
                    MessageHud.MessageType.Center,
                    Loc.T("StoreAndCraft help printed in console (F5).", "StoreAndCraft-Hilfe in der Konsole (F5)."),
                    0, null, false);
        }

        private static bool IsOurCommand(string text)
        {
            string lower = text.ToLowerInvariant();
            if (lower == "/help store" || lower.StartsWith("/help store "))
                return true;
            if (lower == "/help storeandcraft" || lower.StartsWith("/help storeandcraft "))
                return true;
            return lower.StartsWith("/storerange")
                || lower.StartsWith("/dumprange")
                || lower.StartsWith("/craftrange")
                || lower.StartsWith("/storagerange")
                || lower.StartsWith("/sac")
                || lower == "/storehelp"
                || lower.StartsWith("/storehelp ");
        }

        private static void Run(string text)
        {
            string[] parts = Split(text);
            if (parts.Length == 0)
            {
                ShowHelp();
                return;
            }

            string cmd = parts[0].TrimStart('/').ToLowerInvariant();

            // Help — anyone can view
            if (IsHelpRequest(cmd, parts))
            {
                ShowHelp();
                return;
            }

            if (cmd == "sac" && parts.Length >= 2 && parts[1].Equals("status", StringComparison.OrdinalIgnoreCase))
            {
                Tell(StatusText());
                PrintLine(StatusText());
                return;
            }

            if (!CanIssue())
            {
                Tell("Only the host / server admin can change StoreAndCraft config.");
                return;
            }

            if (AdminUtil.IsServer())
            {
                string result;
                bool ok = TryApply(parts, out result);
                if (ok)
                    SaveAndSync();
                Tell(result);
                PrintLine(result);
                return;
            }

            if (ZRoutedRpc.instance == null)
            {
                Tell("Not connected.");
                return;
            }

            // Dump/store ranges are enforced on the CLIENT. Apply locally first so the
            // change is immediate, then let the server save + sync as source of truth.
            string localResult;
            if (TryApply(parts, out localResult))
            {
                Tell(localResult);
                PrintLine(localResult);
            }

            ZRoutedRpc.instance.InvokeRoutedRPC(VersionGate.ServerPeerId(), RpcAdminCmd, text);
        }

        private static void RPC_AdminResult(long sender, string message)
        {
            if (string.IsNullOrEmpty(message))
                return;
            Tell(message);
            PrintLine(message);
        }

        private static bool IsHelpRequest(string cmd, string[] parts)
        {
            if (cmd == "storehelp")
                return true;
            if (cmd == "sac" && parts.Length >= 2 && parts[1].Equals("help", StringComparison.OrdinalIgnoreCase))
                return true;
            if (cmd == "help" && parts.Length >= 2
                && (parts[1].Equals("store", StringComparison.OrdinalIgnoreCase)
                    || parts[1].Equals("storeandcraft", StringComparison.OrdinalIgnoreCase)
                    || parts[1].Equals("sac", StringComparison.OrdinalIgnoreCase)))
                return true;
            return false;
        }

        private static void RPC_AdminCmd(long sender, string text)
        {
            if (!AdminUtil.IsServer() || string.IsNullOrWhiteSpace(text))
                return;

            ZNetPeer peer = ZNet.instance != null ? ZNet.instance.GetPeer(sender) : null;
            if (!ConfigSync.PeerIsAdmin(peer))
            {
                Plugin.Log.LogWarning("Ignored config command from non-admin peer " + sender);
                return;
            }

            string[] parts = Split(text);
            if (IsHelpRequest(parts[0].TrimStart('/').ToLowerInvariant(), parts))
                return;

            string result;
            if (!TryApply(parts, out result))
            {
                Plugin.Log.LogInfo("Admin cmd failed: " + result);
                if (ZRoutedRpc.instance != null)
                    ZRoutedRpc.instance.InvokeRoutedRPC(sender, RpcAdminResult, result);
                return;
            }

            SaveAndSync();
            ConfigSync.SendToPeer(sender);
            if (ZRoutedRpc.instance != null)
                ZRoutedRpc.instance.InvokeRoutedRPC(sender, RpcAdminResult, result + " | " + StatusText());
            Plugin.Log.LogInfo("Admin cmd OK from " + sender + ": " + result);
        }

        private static bool CanIssue()
        {
            ZNet znet = ZNet.instance;
            if (znet == null)
                return true;
            if (znet.IsServer())
                return true;
            if (ConfigSync.ServerGrantedEdit)
                return true;
            try
            {
                return znet.LocalPlayerIsAdminOrHost();
            }
            catch
            {
                return false;
            }
        }

        private static bool TryApply(string[] parts, out string message)
        {
            message = null;
            if (Plugin.Settings == null)
            {
                message = "Settings not loaded.";
                return false;
            }

            string key;
            string valueToken;
            if (!ResolveKeyValue(parts, out key, out valueToken))
            {
                message = "Unknown command. Type: /help store";
                return false;
            }

            float value;
            if (!float.TryParse(valueToken, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                && !float.TryParse(valueToken, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
            {
                message = "Invalid number: " + valueToken;
                return false;
            }

            if (value < 0f || value > 500f)
            {
                message = "Range must be between 0 and 500.";
                return false;
            }

            ConfigEntry<float> entry = EntryFor(key);
            if (entry == null)
            {
                message = "Unknown setting: " + key;
                return false;
            }

            entry.Value = value;
            message = key + " = " + value.ToString("0.##", CultureInfo.InvariantCulture) + " (saved + synced)";
            return true;
        }

        private static bool ResolveKeyValue(string[] parts, out string key, out string valueToken)
        {
            key = null;
            valueToken = null;
            if (parts == null || parts.Length == 0)
                return false;

            string cmd = parts[0].TrimStart('/').ToLowerInvariant();

            if (cmd == "storerange" || cmd == "dumprange" || cmd == "craftrange" || cmd == "storagerange")
            {
                if (parts.Length < 2)
                    return false;
                key = cmd;
                valueToken = parts[1];
                return true;
            }

            if (cmd == "sac")
            {
                if (parts.Length < 3)
                    return false;
                string sub = parts[1].ToLowerInvariant();
                if (sub == "store" || sub == "storerange")
                    key = "storerange";
                else if (sub == "dump" || sub == "dumprange")
                    key = "dumprange";
                else if (sub == "craft" || sub == "craftrange")
                    key = "craftrange";
                else if (sub == "storage" || sub == "storagerange")
                    key = "storagerange";
                else
                    return false;
                valueToken = parts[2];
                return true;
            }

            return false;
        }

        private static ConfigEntry<float> EntryFor(string key)
        {
            switch (key.ToLowerInvariant())
            {
                case "storerange":
                    return Plugin.Settings.StoreRange;
                case "dumprange":
                    return Plugin.Settings.PlayerDumpRange;
                case "craftrange":
                    return Plugin.Settings.CraftRange;
                case "storagerange":
                    return Plugin.Settings.StorageRange;
                default:
                    return null;
            }
        }

        private static void SaveAndSync()
        {
            ConfigWatch.SuppressReload(2f);
            try
            {
                if (Plugin.Instance != null)
                    Plugin.Instance.Config.Save();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning("Config save after command: " + ex.Message);
            }

            if (AdminUtil.IsServer())
                ConfigSync.BroadcastConfig();
        }

        private static string[] Split(string text)
        {
            return text.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static string[] HelpLines()
        {
            return new[]
            {
                "========== StoreAndCraft ==========",
                "Chat (host/admin):",
                "  /help store                 — this help",
                "  /dumprange <m>              — dump / middle-click range",
                "  /storerange <m>             — auto-store range (ground → chest)",
                "  /storagerange <m>           — take-stack / search / displays",
                "  /craftrange <m>             — craft / build / station pull",
                "  /sac status                 — show current ranges",
                "  /sac dump|store|storage|craft <m>",
                "Console (F5), same ideas:",
                "  help store   |  sac help  |  sac status",
                "  dumprange 50 |  storerange 50 |  craftrange 50 |  storagerange 50",
                "Changes save to com.morda.storeandcraft.cfg and sync to clients.",
                "==================================="
            };
        }

        private static string StatusText()
        {
            if (Plugin.Settings == null)
                return "No settings.";
            return "Dump=" + Plugin.Settings.PlayerDumpRange.Value
                + " Store=" + Plugin.Settings.StoreRange.Value
                + " Storage=" + Plugin.Settings.StorageRange.Value
                + " Craft=" + Plugin.Settings.CraftRange.Value
                + " Lock=" + Plugin.Settings.LockConfig.Value;
        }

        private static void Tell(string msg)
        {
            Player player = Player.m_localPlayer;
            if (player != null)
                player.Message(MessageHud.MessageType.Center, msg, 0, null, false);
            else
                Plugin.Log.LogInfo(msg);
        }

        private static void PrintLine(string line)
        {
            if (Console.instance != null)
                Console.instance.Print(line);
            Plugin.Log.LogInfo(line);
        }
    }

    [HarmonyPatch(typeof(Chat), "InputText")]
    internal static class ChatConfigCommandPatch
    {
        private static readonly System.Reflection.FieldInfo InputField =
            AccessTools.Field(typeof(Chat), "m_input");

        private static bool Prefix(Chat __instance)
        {
            if (__instance == null)
                return true;

            string text = ReadInputText(__instance);
            if (string.IsNullOrWhiteSpace(text))
                return true;

            if (!ConfigCommands.TryHandleChat(text))
                return true;

            ClearInputText(__instance);
            return false;
        }

        private static string ReadInputText(Chat chat)
        {
            object input = InputField != null ? InputField.GetValue(chat) : null;
            if (input == null)
                return null;
            var prop = AccessTools.Property(input.GetType(), "text");
            if (prop != null)
                return prop.GetValue(input, null) as string;
            var field = AccessTools.Field(input.GetType(), "text");
            return field != null ? field.GetValue(input) as string : null;
        }

        private static void ClearInputText(Chat chat)
        {
            object input = InputField != null ? InputField.GetValue(chat) : null;
            if (input == null)
                return;
            var prop = AccessTools.Property(input.GetType(), "text");
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(input, "", null);
                return;
            }
            var field = AccessTools.Field(input.GetType(), "text");
            if (field != null)
                field.SetValue(input, "");
        }
    }
}

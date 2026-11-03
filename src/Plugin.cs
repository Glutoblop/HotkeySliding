using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace HotkeySliding
{
    public enum TriggerMode
    {
        /// <summary>Fires the moment the key goes down.</summary>
        Press,
        /// <summary>Fires on release if the key was held briefly and nothing else was pressed meanwhile.</summary>
        Tap,
        /// <summary>Two clean taps in quick succession.</summary>
        DoubleTap,
    }

    // Client-only: no network code, no RPCs, no ZDO data, no Jotunn version check, so it works on vanilla servers.
    // Using an item from the local player's own inventory is already a purely local action in vanilla.
    [BepInPlugin(Guid, Name, Version)]
    [BepInProcess("valheim.exe")]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "jordan.valheim.hotkeysliding";
        public const string Name = "Hotkey Sliding";
        public const string Version = "0.1.0";

        internal static Plugin Instance;

        internal static ConfigEntry<KeyCode> NextRowKeyOverride;
        internal static ConfigEntry<TriggerMode> NextRowTrigger;
        internal static ConfigEntry<KeyCode> PreviousRowKeyOverride;
        internal static ConfigEntry<TriggerMode> PreviousRowTrigger;
        internal static ConfigEntry<bool> ReverseDirection;
        internal static ConfigEntry<float> TapMaxSeconds;
        internal static ConfigEntry<float> DoubleTapWindowSeconds;
        internal static ConfigEntry<bool> AllowWhileInventoryOpen;

        internal static ConfigEntry<bool> ResetOnDeathOrLogout;

        internal static ConfigEntry<bool> ShowIndicator;
        internal static ConfigEntry<bool> HideIndicatorOnFirstRow;
        internal static ConfigEntry<string> IndicatorFormat;
        internal static ConfigEntry<float> IndicatorOffsetX;
        internal static ConfigEntry<float> IndicatorOffsetY;
        internal static ConfigEntry<bool> HighlightRowInInventory;

        private Harmony _harmony;
        private readonly KeyTrigger _nextTrigger = new KeyTrigger(Bindings.NextRow);
        private readonly KeyTrigger _prevTrigger = new KeyTrigger(Bindings.PrevRow);

        private void Awake()
        {
            Instance = this;

            // The keys themselves are bound in Settings -> Controls ("Next hotbar row" / "Previous hotbar row"),
            // like any vanilla key. These overrides exist only as a fallback (e.g. if a game update breaks the menu entry).
            NextRowKeyOverride = Config.Bind("Keys", "NextRowKeyOverride", KeyCode.None,
                "Normally leave this at None and bind 'Next hotbar row' in the game's Settings -> Controls " +
                "(default there: Left Ctrl). If set to a key, that key is used instead and the Controls binding is ignored.");

            NextRowTrigger = Config.Bind("Keys", "NextRowTrigger", TriggerMode.DoubleTap,
                "How the 'Next hotbar row' key must be pressed. Press = on key down. Tap = quick press+release with no " +
                "other key pressed in between. DoubleTap = two such taps in quick succession.\n" +
                "DoubleTap is the default because the default key (Left Ctrl) is also Valheim's Crouch, and crouch is a " +
                "toggle: a single tap already toggles crouch, so Tap would cycle rows every time you crouch. A double tap " +
                "toggles crouch twice (net: no change) and slides the hotbar. If you bind a key the game doesn't use, Press is fine.");

            PreviousRowKeyOverride = Config.Bind("Keys", "PreviousRowKeyOverride", KeyCode.None,
                "Normally leave this at None and bind 'Previous hotbar row' in Settings -> Controls (unbound by default). " +
                "If set to a key, that key is used instead and the Controls binding is ignored.");

            PreviousRowTrigger = Config.Bind("Keys", "PreviousRowTrigger", TriggerMode.Press,
                "How the 'Previous hotbar row' key must be pressed (same options as NextRowTrigger).");

            ReverseDirection = Config.Bind("Keys", "ReverseDirection", false,
                "Swap directions: 'Next hotbar row' goes up a row (1 -> last -> ... -> 2) and 'Previous hotbar row' goes down.");

            TapMaxSeconds = Config.Bind("Keys", "TapMaxSeconds", 0.25f,
                new ConfigDescription("Longest key hold that still counts as a tap.", new AcceptableValueRange<float>(0.05f, 2f)));

            DoubleTapWindowSeconds = Config.Bind("Keys", "DoubleTapWindowSeconds", 0.35f,
                new ConfigDescription("Max time between releasing the first tap and pressing the second.", new AcceptableValueRange<float>(0.05f, 2f)));

            AllowWhileInventoryOpen = Config.Bind("Keys", "AllowWhileInventoryOpen", true,
                "Allow switching rows while the inventory is open (never while a text field has focus).");

            ResetOnDeathOrLogout = Config.Bind("General", "ResetOnDeathOrLogout", false,
                "Return to row 1 when you die or log out / switch character.");

            ShowIndicator = Config.Bind("HUD", "ShowRowIndicator", true,
                "Show a small 'Row X/Y' label next to the hotkey bar.");

            HideIndicatorOnFirstRow = Config.Bind("HUD", "HideIndicatorOnFirstRow", false,
                "Hide the label while row 1 (the vanilla hotbar) is active.");

            IndicatorFormat = Config.Bind("HUD", "IndicatorFormat", "Row {0}/{1}",
                "Label text. {0} = active row (1-based), {1} = number of rows.");

            IndicatorOffsetX = Config.Bind("HUD", "IndicatorOffsetX", 8f,
                "Horizontal offset of the label from the right edge of hotbar slot 8.");

            IndicatorOffsetY = Config.Bind("HUD", "IndicatorOffsetY", 0f,
                "Vertical offset of the label from the vertical centre of the hotbar.");

            HighlightRowInInventory = Config.Bind("HUD", "HighlightRowInInventory", true,
                "Move the 1-8 key labels in the inventory grid onto the active row.");

            _harmony = new Harmony(Guid);
            _harmony.PatchAll(typeof(Plugin).Assembly);
            // ZInput may already exist (and have loaded saved bindings) before our ResetKBMButtons patch was applied.
            try { Bindings.EnsureRegistered(); }
            catch (System.Exception e) { Logger.LogWarning($"Could not register hotbar row keybinds: {e}"); }
            Logger.LogInfo($"{Name} {Version} loaded");
        }

        private void Update()
        {
            Player player = Player.m_localPlayer;
            HotbarState.TrackPlayer(player);

            bool allowed = player != null && InputGuard.CanCycle(player);
            float tapMax = TapMaxSeconds.Value;
            float window = DoubleTapWindowSeconds.Value;
            int dir = ReverseDirection.Value ? -1 : 1;

            if (_nextTrigger.Update(NextRowKeyOverride.Value, NextRowTrigger.Value, tapMax, window, allowed))
                HotbarState.Step(player, dir);
            if (_prevTrigger.Update(PreviousRowKeyOverride.Value, PreviousRowTrigger.Value, tapMax, window, allowed))
                HotbarState.Step(player, -dir);
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
            RowIndicator.DestroyAll();
        }

        internal static void Log(string msg) => Instance?.Logger.LogInfo(msg);
        internal static void Warn(string msg) => Instance?.Logger.LogWarning(msg);
    }
}

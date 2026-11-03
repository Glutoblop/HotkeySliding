using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HotkeySliding
{
    /// <summary>Decides whether a row switch is allowed right now (mirrors Player.TakeInput, minus the inventory check).</summary>
    internal static class InputGuard
    {
        internal static bool CanCycle(Player player)
        {
            if (player == null || player.IsDead() || player.InCutscene() || player.IsTeleporting())
                return false;
            if (Chat.instance != null && Chat.instance.HasFocus())
                return false;
            if (Console.IsVisible() || TextInput.IsVisible() || Menu.IsVisible() || StoreGui.IsVisible())
                return false;
            if (Settings.instance != null || ZInput.s_IsRebindActive)
                return false; // settings window open / rebinding a key
            if (TextViewer.instance != null && TextViewer.instance.IsVisible())
                return false;
            if (Minimap.IsOpen() || GameCamera.InFreeFly() || PlayerCustomizaton.IsBarberGuiVisible())
                return false;
            if (Hud.instance != null && Hud.instance.m_buildUi != null && Hud.instance.m_buildUi.SearchFieldFocused)
                return false;
            if (InventoryGui.IsVisible() && !Plugin.AllowWhileInventoryOpen.Value)
                return false;
            if (TextFieldFocused())
                return false;
            return true;
        }

        /// <summary>Catches any focused text box, including ones added by other mods (e.g. crafting search fields).</summary>
        private static bool TextFieldFocused()
        {
            EventSystem es = EventSystem.current;
            GameObject go = es != null ? es.currentSelectedGameObject : null;
            if (go == null)
                return false;
            TMP_InputField tmp = go.GetComponent<TMP_InputField>();
            if (tmp != null && tmp.isFocused)
                return true;
            UnityEngine.UI.InputField legacy = go.GetComponent<UnityEngine.UI.InputField>();
            return legacy != null && legacy.isFocused;
        }
    }
}

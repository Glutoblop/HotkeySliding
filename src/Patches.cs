using System.Collections.Generic;
using HarmonyLib;
using TMPro;

namespace HotkeySliding
{
    // Vanilla hard-codes row 0 as the hotbar in exactly these places (assembly_valheim):
    //   Player.UseHotbarItem       -> m_inventory.GetItemAt(index - 1, 0)   (keys 1-8, gamepad hotbar use, HUD tap)
    //   Inventory.GetBoundItems    -> items with m_gridPos.y == 0            (HotkeyBar.UpdateIcons, the HUD bar)
    //   Inventory.GetHotbar        -> items with m_gridPos.y == 0            (gamepad radial "hotbar" group)
    //   InventoryGrid.UpdateGui    -> "1".."8" binding labels on row 0 only (cosmetic)
    // Row remapping applies only to the local player's own inventory.

    [HarmonyPatch(typeof(Player), nameof(Player.UseHotbarItem))]
    internal static class UseHotbarItemPatch
    {
        private static bool Prefix(Player __instance, int index)
        {
            if (__instance != Player.m_localPlayer)
                return true;
            if (InputGuard.IsHotbarScreenOpen())
                return false;
            int row = HotbarState.ActiveRow(__instance);
            if (row == 0)
                return true;

            ItemDrop.ItemData item = __instance.m_inventory.GetItemAt(index - 1, row);
            if (item != null)
                __instance.UseItem(null, item, fromInventoryGui: false);
            return false;
        }
    }

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.GetBoundItems))]
    internal static class GetBoundItemsPatch
    {
        private static bool Prefix(Inventory __instance, List<ItemDrop.ItemData> bound)
        {
            if (!HotbarState.TryGetRowFor(__instance, out int row) || row == 0)
                return true;

            bound.Clear();
            foreach (ItemDrop.ItemData item in __instance.m_inventory)
            {
                if (item.m_gridPos.y == row)
                    bound.Add(item);
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.GetHotbar))]
    internal static class GetHotbarPatch
    {
        private static bool Prefix(Inventory __instance, bool includeEmpty, ref List<ItemDrop.ItemData> __result)
        {
            if (!HotbarState.TryGetRowFor(__instance, out int row) || row == 0)
                return true;

            List<ItemDrop.ItemData> rowItems = new List<ItemDrop.ItemData>();
            foreach (ItemDrop.ItemData item in __instance.m_inventory)
            {
                if (item.m_gridPos.y == row)
                    rowItems.Add(item);
            }

            if (!includeEmpty)
            {
                rowItems.Sort((a, b) => a.m_gridPos.x.CompareTo(b.m_gridPos.x));
                __result = rowItems;
                return false;
            }

            // Same shape as vanilla: exactly 8 entries, null for empty slots.
            List<ItemDrop.ItemData> slots = new List<ItemDrop.ItemData>(8);
            for (int x = 0; x < 8; x++)
                slots.Add(rowItems.Find(i => i.m_gridPos.x == x));
            __result = slots;
            return false;
        }
    }

    [HarmonyPatch(typeof(HotkeyBar), nameof(HotkeyBar.Update))]
    internal static class HotkeyBarUpdatePatch
    {
        private static void Postfix(HotkeyBar __instance) => HotbarHud.Attach(__instance);
    }

    [HarmonyPatch(typeof(Player), nameof(Player.OnDeath))]
    internal static class PlayerOnDeathPatch
    {
        private static void Postfix(Player __instance)
        {
            if (__instance == Player.m_localPlayer)
                HotbarState.OnLocalPlayerDied();
        }
    }

    /// <summary>Moves the inventory grid's "1".."8" key labels onto the active row.</summary>
    [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.UpdateGui))]
    internal static class InventoryGridLabelsPatch
    {
        private static void Postfix(InventoryGrid __instance, Player player)
        {
            if (player == null || player != Player.m_localPlayer)
                return;
            if (!HotbarState.TryGetRowFor(__instance.m_inventory, out int row))
                return;
            if (!Plugin.HighlightRowInInventory.Value)
                row = 0; // vanilla layout (also restores it if the option is switched off mid-game)

            foreach (InventoryElement element in __instance.m_elements)
            {
                if (element == null)
                    continue;
                TMP_Text label = BindingLabel(element);
                if (label == null)
                    continue;

                Vector2i pos = element.Position;
                bool onRow = pos.y == row; // vanilla labels every column of row 0
                if (onRow)
                {
                    string text = (pos.x + 1).ToString();
                    if (label.text != text)
                        label.text = text;
                }
                if (label.enabled != onRow)
                    label.enabled = onRow;
            }
        }

        private static readonly Dictionary<InventoryElement, TMP_Text> Labels = new Dictionary<InventoryElement, TMP_Text>();

        private static TMP_Text BindingLabel(InventoryElement element)
        {
            if (Labels.TryGetValue(element, out TMP_Text label) && label != null)
                return label;
            if (Labels.Count > 256)
                Labels.Clear(); // grid was rebuilt; drop stale (destroyed) keys
            UnityEngine.Transform t = element.transform.Find("binding");
            label = t != null ? t.GetComponent<TMP_Text>() : null;
            Labels[element] = label;
            return label;
        }
    }
}

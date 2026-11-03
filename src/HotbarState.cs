namespace HotkeySliding
{
    /// <summary>Holds the active hotbar row. Purely local, never saved or sent anywhere.</summary>
    internal static class HotbarState
    {
        private static int _row;
        private static object _lastPlayer;

        /// <summary>Number of rows in the player's inventory (read live, so inventory-expanding mods are respected).</summary>
        internal static int RowCount(Player player)
        {
            Inventory inv = player != null ? player.GetInventory() : null;
            return inv != null ? System.Math.Max(1, inv.GetHeight()) : 1;
        }

        /// <summary>The active row, clamped to the inventory's current height.</summary>
        internal static int ActiveRow(Player player)
        {
            int rows = RowCount(player);
            if (_row >= rows) _row = rows - 1;
            if (_row < 0) _row = 0;
            return _row;
        }

        /// <summary>
        /// True (with the row) when <paramref name="inventory"/> is the local player's inventory.
        /// Container and other players' inventories are never affected.
        /// </summary>
        internal static bool TryGetRowFor(Inventory inventory, out int row)
        {
            row = 0;
            Player player = Player.m_localPlayer;
            if (player == null || inventory == null || !ReferenceEquals(inventory, player.m_inventory))
                return false;
            row = ActiveRow(player);
            return true;
        }

        internal static void Step(Player player, int dir)
        {
            int rows = RowCount(player);
            _row = ((ActiveRow(player) + dir) % rows + rows) % rows;
        }

        /// <summary>Detects death/respawn and logout/character switch (both give a new Player instance).</summary>
        internal static void TrackPlayer(Player player)
        {
            if (player == null)
                return;
            if (ReferenceEquals(player, _lastPlayer))
                return;
            if (_lastPlayer != null && Plugin.ResetOnDeathOrLogout.Value)
                _row = 0;
            _lastPlayer = player;
        }

        internal static void OnLocalPlayerDied()
        {
            if (Plugin.ResetOnDeathOrLogout.Value)
                _row = 0;
        }
    }
}

using HarmonyLib;
using UnityEngine.InputSystem;

namespace HotkeySliding
{
    /// <summary>
    /// Registers two extra rebindable ZInput buttons so they behave like vanilla keys:
    /// rebind in Settings -> Controls, saved by vanilla ZInput.Save() to PlatformPrefs as
    /// "kbmBinding_&lt;name&gt;", restored by ZInput.Load(), and reset by the Controls tab's reset button.
    /// </summary>
    internal static class Bindings
    {
        internal const string NextRow = "HotkeySliding_NextRow";
        internal const string PrevRow = "HotkeySliding_PrevRow";

        private const string PrefsPrefix = "kbmBinding_";

        /// <summary>Default path for "next row" (LeftControl) - same thing vanilla uses for Crouch.</summary>
        internal static string NextRowDefaultPath => ZInput.KeyToPath(Key.LeftCtrl);

        /// <summary>Adds our buttons if missing. Must run inside ZInput's (re)build, i.e. right after ResetKBMButtons.</summary>
        internal static void AddButtons(ZInput input)
        {
            if (input == null)
                return;
            // "" = unbound, exactly like vanilla's HotbarNAlt buttons.
            if (!input.m_buttons.ContainsKey(NextRow))
                input.AddButton(NextRow, NextRowDefaultPath, altKey: false, showHints: false, rebindable: true);
            if (!input.m_buttons.ContainsKey(PrevRow))
                input.AddButton(PrevRow, "", altKey: false, showHints: false, rebindable: true);
        }

        /// <summary>
        /// Safety net for when ZInput was already initialised (and Load() already ran) before our patch existed:
        /// add the buttons and apply any saved bindings ourselves.
        /// </summary>
        internal static void EnsureRegistered()
        {
            ZInput input = ZInput.instance;
            if (input == null)
                return;
            bool hadNext = input.m_buttons.ContainsKey(NextRow);
            bool hadPrev = input.m_buttons.ContainsKey(PrevRow);
            if (hadNext && hadPrev)
                return;

            AddButtons(input);
            if (!hadNext) ApplySaved(input, NextRow);
            if (!hadPrev) ApplySaved(input, PrevRow);
        }

        private static void ApplySaved(ZInput input, string name)
        {
            string key = PrefsPrefix + name;
            if (PlatformPrefs.HasKey(key) && input.m_buttons.TryGetValue(name, out ZInput.ButtonDef def))
                def.Rebind(PlatformPrefs.GetString(key));
        }

        internal static ZInput.ButtonDef Get(string name)
        {
            ZInput input = ZInput.instance;
            if (input == null)
                return null;
            input.m_buttons.TryGetValue(name, out ZInput.ButtonDef def);
            return def;
        }
    }

    /// <summary>
    /// ZInput.Reset() (called by Load(), i.e. at startup and when backing out of Settings) clears every button and
    /// re-adds the vanilla keyboard ones via ResetKBMButtons; re-add ours right after so Load() then restores the
    /// saved binding for them like for any vanilla key.
    /// </summary>
    [HarmonyPatch(typeof(ZInput), nameof(ZInput.ResetKBMButtons))]
    internal static class ZInputResetKbmButtonsPatch
    {
        private static void Postfix(ZInput __instance)
        {
            try
            {
                Bindings.AddButtons(__instance);
            }
            catch (System.Exception e)
            {
                Plugin.Warn($"Could not register hotbar row keybinds: {e}");
            }
        }
    }
}

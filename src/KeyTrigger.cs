using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace HotkeySliding
{
    /// <summary>
    /// Press / Tap / DoubleTap detector on top of a ZInput button (rebindable in Settings -> Controls),
    /// or on a fixed KeyCode when the cfg override is set.
    /// </summary>
    internal sealed class KeyTrigger
    {
        private readonly string _buttonName;
        private readonly List<InputControl> _ownControls = new List<InputControl>();

        private bool _held;              // key currently held and being timed as a tap
        private bool _clean;             // no timeout / other input since the key went down
        private float _downTime;
        private float _lastTapRelease = float.NegativeInfinity;
        private bool _isSecondTap;

        internal KeyTrigger(string buttonName)
        {
            _buttonName = buttonName;
        }

        internal bool Update(KeyCode overrideKey, TriggerMode mode, float tapMax, float doubleWindow, bool allowed)
        {
            if (!allowed || !ReadKey(overrideKey, out bool pressed, out bool down))
            {
                Reset();
                return false;
            }

            float now = Time.unscaledTime;

            if (mode == TriggerMode.Press)
            {
                Reset();
                return pressed;
            }

            if (pressed)
            {
                _isSecondTap = mode == TriggerMode.DoubleTap && now - _lastTapRelease <= doubleWindow;
                _held = true;
                _clean = true;
                _downTime = now;
                return false;
            }

            bool watching = _held || (mode == TriggerMode.DoubleTap && now - _lastTapRelease <= doubleWindow);
            if (watching && OtherInputPressed())
            {
                _clean = false;
                _lastTapRelease = float.NegativeInfinity;
            }

            if (!_held)
                return false;

            if (now - _downTime > tapMax)
                _clean = false;

            if (down)
                return false;

            // Released.
            _held = false;
            if (!_clean)
            {
                _lastTapRelease = float.NegativeInfinity;
                return false;
            }

            if (mode == TriggerMode.Tap)
                return true;

            if (_isSecondTap)
            {
                _isSecondTap = false;
                _lastTapRelease = float.NegativeInfinity;
                return true;
            }

            _lastTapRelease = now;
            return false;
        }

        /// <summary>Reads pressed-this-frame / held, and records which physical controls belong to this key.</summary>
        private bool ReadKey(KeyCode overrideKey, out bool pressed, out bool down)
        {
            pressed = down = false;
            _ownControls.Clear();

            if (overrideKey != KeyCode.None)
            {
                if (!ZInput.IsKeyCodeValid(overrideKey))
                    return false;
                pressed = ZInput.GetKeyDown(overrideKey, false);
                down = ZInput.GetKey(overrideKey, false);
                if (Keyboard.current != null && ZInput.TryKeyCodeToKey(overrideKey, out Key key))
                    _ownControls.Add(Keyboard.current[key]);
                return true;
            }

            ZInput.ButtonDef def = Bindings.Get(_buttonName);
            if (def == null || def.ButtonAction == null)
                return false;
            foreach (InputControl c in def.ButtonAction.controls)
                _ownControls.Add(c);
            if (_ownControls.Count == 0)
                return false; // unbound
            pressed = def.Pressed;
            down = def.Held;
            return true;
        }

        private void Reset()
        {
            _held = false;
            _clean = false;
            _isSecondTap = false;
            _lastTapRelease = float.NegativeInfinity;
        }

        /// <summary>Any keyboard key or mouse button (other than this key's own control) pressed this frame.</summary>
        private bool OtherInputPressed()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                foreach (KeyControl key in kb.allKeys)
                {
                    if (key != null && key.wasPressedThisFrame && !_ownControls.Contains(key))
                        return true;
                }
            }

            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                if (Other(mouse.leftButton) || Other(mouse.rightButton) || Other(mouse.middleButton) ||
                    Other(mouse.backButton) || Other(mouse.forwardButton))
                    return true;
            }
            return false;
        }

        private bool Other(ButtonControl b) => b != null && b.wasPressedThisFrame && !_ownControls.Contains(b);
    }
}

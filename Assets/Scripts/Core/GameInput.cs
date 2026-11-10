using UnityEngine;
using UnityEngine.InputSystem;

namespace AR.Core
{
    /// <summary>
    /// Code-defined input actions (no asset wiring): keyboard/gamepad move, pointer drag (mouse or touch),
    /// and an "advance" button for the VN. Same bindings on PC and mobile.
    /// </summary>
    public static class GameInput
    {
        public static InputAction Move { get; private set; }
        public static InputAction PointerPosition { get; private set; }
        public static InputAction PointerPress { get; private set; }
        public static InputAction Advance { get; private set; }

        private static bool _initialized;

        public static void Init()
        {
            if (_initialized) return;
            _initialized = true;

            Move = new InputAction("Move", InputActionType.Value);
            Move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            Move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            Move.AddBinding("<Gamepad>/leftStick");

            PointerPosition = new InputAction("PointerPosition", InputActionType.PassThrough, "<Pointer>/position");
            PointerPress = new InputAction("PointerPress", InputActionType.Button, "<Pointer>/press");

            Advance = new InputAction("Advance", InputActionType.Button);
            Advance.AddBinding("<Keyboard>/space");
            Advance.AddBinding("<Keyboard>/enter");
            Advance.AddBinding("<Gamepad>/buttonSouth");

            Move.Enable();
            PointerPosition.Enable();
            PointerPress.Enable();
            Advance.Enable();
        }

        public static Vector2 PointerScreen => PointerPosition.ReadValue<Vector2>();
        public static bool IsPointerDown => PointerPress.IsPressed();
    }
}

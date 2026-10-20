using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// 新旧どちらの Input System でも動く入力ヘルパー
public static class GameInput
{
    // WASD / 矢印キー
    public static Vector2 Move
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return Vector2.zero;
            float x = 0f, y = 0f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) y -= 1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) y += 1f;
            return new Vector2(x, y).normalized;
#else
            return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;
#endif
        }
    }

    // マウスの移動量(旧 Input の Mouse X/Y と同じくらいの大きさに揃えている)
    public static Vector2 Look
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current == null) return Vector2.zero;
            return Mouse.current.delta.ReadValue() * 0.1f;
#else
            return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
#endif
        }
    }

    // R キー(リスタート)
    public static bool RestartPressed
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.R);
#endif
        }
    }
}
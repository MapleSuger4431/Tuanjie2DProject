using UnityEngine;
using UnityEngine.InputSystem;

namespace GameRuntimeTest
{
    /// <summary>
    /// 测试场景全部输入读取的唯一入口。本工程为仅 Input System（activeInputHandler=2），
    /// 直接使用 UnityEngine.InputSystem API，不做新旧输入兼容。
    /// </summary>
    public static class RuntimeTestInput
    {
        /// <summary>WASD / 方向键移动轴。</summary>
        public static Vector2 Move
        {
            get
            {
                var kb = Keyboard.current;
                if (kb == null) return Vector2.zero;
                var v = Vector2.zero;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) v.x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) v.x += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v.y -= 1f;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v.y += 1f;
                return v;
            }
        }

        /// <summary>空格：开火/得分。</summary>
        public static bool FirePressed
        {
            get
            {
                var kb = Keyboard.current;
                return kb != null && kb.spaceKey.wasPressedThisFrame;
            }
        }

        /// <summary>Esc：暂停/恢复。</summary>
        public static bool PausePressed
        {
            get
            {
                var kb = Keyboard.current;
                return kb != null && kb.escapeKey.wasPressedThisFrame;
            }
        }

        /// <summary>R：非 Playing 状态下重开。</summary>
        public static bool RestartPressed
        {
            get
            {
                var kb = Keyboard.current;
                return kb != null && kb.rKey.wasPressedThisFrame;
            }
        }
    }
}

using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Game.Input
{
    /// <summary>
    /// 全局输入单例：包装生成的 <see cref="GameInputActions"/>（Assets/Settings/GameInputActions.inputactions）。
    /// 首次访问 <see cref="Instance"/> 时创建并 Enable；随域重载自动重建。
    /// 双输入系统兼容：按当前 Active Input Handling 选择读取方式。
    /// </summary>
    public sealed class GameInput : IDisposable
    {
        static GameInput s_Instance;

        public static GameInput Instance => s_Instance ??= new GameInput();

#if ENABLE_INPUT_SYSTEM
        readonly GameInputActions m_Actions = new GameInputActions();

        GameInput()
        {
            m_Actions.Enable();
        }

        /// <summary>原始 Actions 包装类，供事件订阅（started/canceled）、重绑定等高级用法使用。</summary>
        public GameInputActions Actions => m_Actions;

        public GameInputActions.PlayerActions Player => m_Actions.Player;
        public GameInputActions.UIActions UI => m_Actions.UI;

        public Vector2 Move => m_Actions.Player.Move.ReadValue<Vector2>();
        public bool JumpHeld => m_Actions.Player.Jump.IsPressed();
        public bool JumpPressedThisFrame => m_Actions.Player.Jump.WasPressedThisFrame();

        public void Dispose()
        {
            m_Actions.Disable();
            m_Actions.Dispose();
            if (ReferenceEquals(s_Instance, this))
            {
                s_Instance = null;
            }
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        GameInput()
        {
        }

        // 旧输入管理器兜底：对齐旧默认轴 Horizontal/Vertical 与 Space 跳跃
        public Vector2 Move => new Vector2(UnityEngine.Input.GetAxisRaw("Horizontal"), UnityEngine.Input.GetAxisRaw("Vertical"));
        public bool JumpHeld => UnityEngine.Input.GetKey(KeyCode.Space);
        public bool JumpPressedThisFrame => UnityEngine.Input.GetKeyDown(KeyCode.Space);

        public void Dispose()
        {
            if (ReferenceEquals(s_Instance, this))
            {
                s_Instance = null;
            }
        }
#else
        GameInput()
        {
        }

        public Vector2 Move => Vector2.zero;
        public bool JumpHeld => false;
        public bool JumpPressedThisFrame => false;

        public void Dispose()
        {
            if (ReferenceEquals(s_Instance, this))
            {
                s_Instance = null;
            }
        }
#endif
    }
}

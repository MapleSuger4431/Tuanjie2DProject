#if UNITY_EDITOR // 示例仅编辑器内参与编译（运行时程序集保证场景可挂载，#if 保证构建剔除）
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Runestone.AesirModules.Samples.Events.KeyPress
{
    /// <summary>
    /// 按键事件发布者。按指定键发布 <see cref="KeyPressedEvent" /> 事件。
    /// </summary>
    [AddComponentMenu("")]
    public class EventSender : MonoBehaviour
    {
        [SerializeField]
        KeyCode triggerKey = KeyCode.Space;

        void Update()
        {
            // 双输入系统兼容：按当前 Active Input Handling 选择读取方式
#if ENABLE_INPUT_SYSTEM
            bool triggered = IsTriggerKeyDown();
#elif ENABLE_LEGACY_INPUT_MANAGER
            bool triggered = Input.GetKeyDown(triggerKey);
#else
            bool triggered = false;
#endif
            if (triggered)
            {
                new KeyPressedEvent { Key = triggerKey }.Invoke(this);
            }
        }

#if ENABLE_INPUT_SYSTEM
        /// <summary>
        /// 新输入系统下将旧 <see cref="KeyCode" /> 按名称映射为 <see cref="Key" />
        /// （Space、字母、方向键等常见键同名；鼠标/手柄键等无同名项时返回 false）。
        /// </summary>
        bool IsTriggerKeyDown()
        {
            if (Keyboard.current == null)
            {
                return false;
            }

            return System.Enum.TryParse(triggerKey.ToString(), out Key key)
                && Keyboard.current[key].wasPressedThisFrame;
        }
#endif
    }
}
#endif

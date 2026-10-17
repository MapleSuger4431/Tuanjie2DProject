#if UNITY_EDITOR // 示例仅编辑器内参与编译（运行时程序集保证场景可挂载，#if 保证构建剔除）
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Runestone.AesirModules.Samples.Events.Filters
{
    /// <summary>
    /// 家族命令发布者。按 R 发布 <see cref="SquadOrderEvent" />，用 <see cref="OnlySelf" /> 过滤器
    /// 只通知本物体自身/子树/父级链上的订阅者。对照：场景中另一组同构小队（无发布者）
    /// 同样订阅了该事件，但永远收不到——直观呈现"只投递给同一家族"。
    /// </summary>
    [AddComponentMenu("")]
    public class SquadOrderPublisher : MonoBehaviour
    {
        void Update()
        {
            // 双输入系统兼容：按当前 Active Input Handling 选择读取方式
#if ENABLE_INPUT_SYSTEM
            bool triggered = Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            bool triggered = Input.GetKeyDown(KeyCode.R);
#else
            bool triggered = false;
#endif
            if (triggered)
            {
                PublishSquadOrder();
            }
        }

        /// <summary>
        /// 发布带 OnlySelf 过滤器的家族命令事件。
        /// </summary>
        public void PublishSquadOrder()
        {
            new SquadOrderEvent { Order = "就地集合" }.WithFilter(new OnlySelf()).Invoke(this);
        }
    }
}
#endif

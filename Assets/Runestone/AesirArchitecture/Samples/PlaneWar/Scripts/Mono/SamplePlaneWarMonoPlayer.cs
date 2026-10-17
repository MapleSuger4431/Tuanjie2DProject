#if UNITY_EDITOR // 示例仅编辑器内参与编译（运行时程序集保证场景可挂载，#if 保证构建剔除）
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Runestone.AesirArchitecture.Samples.PlaneWarMono
{
    /// <summary>
    /// 飞机大战原生版示例 —— 玩家飞机。
    /// </summary>
    /// <remarks>
    /// 原生写法：一个组件同时负责读输入、移动、开火三件事；移动边界以序列化字段配置在组件上。
    /// <para>
    /// 操作：方向键 / WASD 移动，按住 Space 连发（受 <see cref="fireCooldown" /> 节流）。
    /// 本示例中玩家位于画面下方朝上射击，敌机自上方往下飞。
    /// </para>
    /// <para>移动与开火只在游戏进行中生效；失败后由 <see cref="SamplePlaneWarMonoGameManager.SetGameOver" /> 接管。</para>
    /// </remarks>
    public class SamplePlaneWarMonoPlayer : MonoBehaviour
    {
        /// <summary>
        /// 移动速度（世界单位 / 秒）。
        /// </summary>
        [SerializeField]
        float moveSpeed = 8f;

        /// <summary>
        /// 开火最小间隔（秒）；按住开火键时按此间隔连发。
        /// </summary>
        [SerializeField]
        float fireCooldown = 0.15f;

        /// <summary>
        /// 移动边界（x / y 各自的半宽半高），玩家被限制在背景区域内。
        /// </summary>
        [SerializeField]
        Vector2 moveBounds = new Vector2(2.5f, 5.5f);

        /// <summary>
        /// 子弹预制体；在机头上方生成。
        /// </summary>
        [SerializeField]
        SamplePlaneWarMonoBullet bulletPrefab;

        /// <summary>
        /// 枪口相对机身的垂直偏移（机头朝上，为正值）。
        /// </summary>
        [SerializeField]
        float muzzleOffsetY = 0.6f;

        float _fireTimer;

        void Update()
        {
            if (SamplePlaneWarMonoGameManager.Instance.IsGameOver)
            {
                return;
            }

            Move();
            Fire();
        }

        void Move()
        {
            // 归一化保证斜向移动不快于正向
#if ENABLE_INPUT_SYSTEM
            var direction = ReadMoveDirection();
#elif ENABLE_LEGACY_INPUT_MANAGER
            var direction = new Vector3(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"), 0f)
                .normalized;
#else
            var direction = Vector3.zero;
#endif

            var position = transform.position + direction * (moveSpeed * Time.deltaTime);
            position.x = Mathf.Clamp(position.x, -moveBounds.x, moveBounds.x);
            position.y = Mathf.Clamp(position.y, -moveBounds.y, moveBounds.y);
            transform.position = position;
        }

        void Fire()
        {
            _fireTimer += Time.deltaTime;
#if ENABLE_INPUT_SYSTEM
            bool fireHeld = Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            bool fireHeld = Input.GetKey(KeyCode.Space);
#else
            bool fireHeld = false;
#endif
            if (fireHeld && _fireTimer >= fireCooldown)
            {
                _fireTimer = 0f;
                Instantiate(bulletPrefab, transform.position + Vector3.up * muzzleOffsetY,
                    Quaternion.identity);
            }
        }

#if ENABLE_INPUT_SYSTEM
        /// <summary>
        /// 新输入系统下读取方向键 / WASD，按键映射与旧输入管理器默认 Horizontal / Vertical 轴一致。
        /// </summary>
        static Vector3 ReadMoveDirection()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return Vector3.zero;
            }

            var x = (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed ? 1f : 0f)
                - (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed ? 1f : 0f);
            var y = (keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed ? 1f : 0f)
                - (keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed ? 1f : 0f);
            return new Vector3(x, y, 0f).normalized;
        }
#endif
    }
}
#endif

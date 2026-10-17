using UnityEngine;

namespace GameRuntimeTest
{
    /// <summary>测试用玩家：WASD/方向键移动，仅 Playing 状态响应输入。</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class RuntimePlayerMover : MonoBehaviour
    {
        [SerializeField] float moveSpeed = 5f;
        [SerializeField] Vector2 moveRange = new Vector2(7.5f, 4f);

        RuntimeFlowModel _flow;

        void Start()
        {
            _flow = GameRuntimeTestContext.Instance.GetModel<RuntimeFlowModel>();
        }

        void Update()
        {
            if (_flow == null || !_flow.IsPlaying) return;

            var pos = transform.position + (Vector3)(RuntimeTestInput.Move * (moveSpeed * Time.deltaTime));
            pos.x = Mathf.Clamp(pos.x, -moveRange.x, moveRange.x);
            pos.y = Mathf.Clamp(pos.y, -moveRange.y, moveRange.y);
            transform.position = pos;
        }
    }
}

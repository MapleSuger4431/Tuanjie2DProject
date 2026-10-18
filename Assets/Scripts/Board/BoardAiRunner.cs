using UnityEngine;
using Runestone.AesirArchitecture;

namespace Game.Board
{
    /// <summary>
    /// AI 的表现与驱动：在棋盘上渲染一个醒目的角色（出生在左下角起点），
    /// 并在运行阶段按固定间隔驱动 <see cref="BoardRunModel.Step" />（每步只查四邻 1 格），格间做补间移动。
    /// 挂在 BoardGridView 同物体上，角色物体是本组件自建的子物体（不进 Tiles 容器，避免重建棋盘时被清掉）。
    /// </summary>
    public sealed class BoardAiRunner : MonoViewController<BoardContext>
    {
        [Tooltip("每格行走耗时（秒），同时是 AI 的思考间隔")]
        [SerializeField, Range(0.05f, 2f)] float stepInterval = 0.2f;

        [Tooltip("角色预制体（带 SpriteRenderer + Animator，如 Assets/Art/Character/Character.prefab）；为空时退回简单贴图方案")]
        [SerializeField] GameObject aiPrefab;

        [Tooltip("角色贴图；既无预制体又无贴图时复用棋盘的格子贴图")]
        [SerializeField] Sprite aiSprite;

        [SerializeField] Color aiColor = new Color(1f, 0.31f, 0.85f, 1f);

        [Tooltip("相对格子尺寸的缩放")]
        [SerializeField, Range(0.2f, 1.5f)] float aiScale = 0.72f;

        [Tooltip("使用预制体时，角色高度相当于几个格子（原 1.6 格缩小 0.7 倍 = 1.12）")]
        [SerializeField, Range(0.2f, 3f)] float actorHeightInCells = 1.12f;

        [Tooltip("角色相对格心向下偏移多少个格子高度（正值 = 往下）：脚踩格子下半部，不再正贴格心")]
        [SerializeField, Range(-1f, 1f)] float actorOffsetInCells = 0.2f;

        [Tooltip("渲染层级，需高于棋盘格才能在格子上可见")]
        [SerializeField] int sortingOrder = 5;

        [Header("元素防护罩（RB-14：AI 外观同步切换）")]
        [Tooltip("防护罩贴图（白色径向光罩，运行时按元素属性染色：火=暖红、冰=亮蓝）；为空则不显示")]
        [SerializeField] Sprite shieldSprite;

        [Tooltip("火属性防护罩染色")]
        [SerializeField] Color shieldColorFire = new Color(0.98f, 0.47f, 0.28f, 0.55f);

        [Tooltip("冰属性防护罩染色")]
        [SerializeField] Color shieldColorIce = new Color(0.42f, 0.76f, 0.98f, 0.55f);

        [Tooltip("防护罩直径相对角色贴图高度的比例（0.95：比角色略大一点，不再整个人罩满）")]
        [SerializeField, Range(0.5f, 2f)] float shieldHeightRatio = 0.95f;

        static readonly int MovingHash = Animator.StringToHash("Moving");
        static readonly int DirHash = Animator.StringToHash("Dir");

        BoardRunModel _run;
        BoardModel _board;
        BoardGridView _grid;
        Transform _actor;
        SpriteRenderer _renderer;
        Animator _animator;
        SpriteRenderer _shield;
        Vector3 _shieldBaseScale = Vector3.one;

        /// <summary>防护罩当前已显示的元素；初始取非法值，保证首帧一定按当前属性初始化一次。</summary>
        ElementState _shownElement = (ElementState)(-1);

        /// <summary>属性切换后的放大脉冲余量（1 → 0）。</summary>
        float _shieldPulse;

        float _timer;
        bool _moving;
        float _lerp;
        Vector3 _fromPosition;
        Vector3 _toPosition;

        void Start()
        {
            _run = this.GetModel<BoardRunModel>();
            _board = this.GetModel<BoardModel>();
            _grid = GetComponent<BoardGridView>();

            BuildActor();
            BuildShield();

            _run.AiMoved.AddListener(OnAiMoved)
                .RemoveListenerWhenGameObjectOnDestroyed(gameObject);

            SnapTo(_run.AiCell.Value);
        }

        void Update()
        {
            if (_run == null || _grid == null) return;

            if (_run.IsRunning)
            {
                _timer += Time.deltaTime;
                if (_timer >= stepInterval)
                {
                    _timer = 0f;
                    _run.Step();
                }
            }
            else
            {
                _timer = 0f;
            }

            AdvanceMoveAnimation();
            ApplyAnimator();
            UpdateShield(Time.deltaTime);
        }

        void AdvanceMoveAnimation()
        {
            if (!_moving || _actor == null) return;

            _lerp += stepInterval > 0f ? Time.deltaTime / stepInterval : 1f;
            if (_lerp >= 1f)
            {
                _lerp = 1f;
                _moving = false;
            }

            _actor.localPosition = Vector3.Lerp(_fromPosition, _toPosition, _lerp);
        }

        void OnAiMoved()
        {
            if (_run == null) return;

            if (_animator != null && _run.LastDirection.HasValue)
                _animator.SetInteger(DirHash, (int)_run.LastDirection.Value);

            var target = CellToLocal(_run.AiCell.Value);
            if (_actor == null)
            {
                SnapTo(_run.AiCell.Value);
                return;
            }

            // 出发 / 重置时直接吸附，行走时才补间
            if (!_run.IsRunning || Vector3.Distance(_actor.localPosition, target) > _grid.Step * 1.5f)
            {
                SnapTo(_run.AiCell.Value);
                return;
            }

            _fromPosition = _actor.localPosition;
            _toPosition = target;
            _lerp = 0f;
            _moving = true;
        }

        void SnapTo(Vector2Int cell)
        {
            if (_actor == null) return;

            _toPosition = CellToLocal(cell);
            _fromPosition = _toPosition;
            _actor.localPosition = _toPosition;
            _moving = false;
            _lerp = 1f;
        }

        Vector3 CellToLocal(Vector2Int cell)
        {
            if (_grid == null) return Vector3.zero;

            // 锚点自格心下移：偏移按格子高度取，跟着格子尺寸一起缩放。
            // 吸附与补间都走这里，两种移动方式的落点才不会差半格。
            var local = _grid.CellToLocal(cell);
            local.y -= _grid.CellSize * actorOffsetInCells;
            return local;
        }

        void BuildActor()
        {
            if (_actor != null) return;

            if (aiPrefab != null)
            {
                BuildActorFromPrefab();
                return;
            }

            var sprite = aiSprite != null ? aiSprite : (_grid != null ? _grid.CellSprite : null);
            var unit = sprite != null ? sprite.bounds.size.x : 1f;

            var go = new GameObject("AI");
            go.transform.SetParent(transform, false);

            _renderer = go.AddComponent<SpriteRenderer>();
            _renderer.sprite = sprite;
            _renderer.color = aiColor;
            _renderer.sortingOrder = sortingOrder;

            var size = _grid != null ? _grid.CellSize * aiScale : aiScale;
            go.transform.localScale = Vector3.one * (size / (unit > 0f ? unit : 1f));

            _actor = go.transform;
        }

        /// <summary>用角色预制体（带 SpriteRenderer + Animator）当 AI。</summary>
        void BuildActorFromPrefab()
        {
            var instance = Instantiate(aiPrefab, transform, false);
            instance.name = "AI";

            _renderer = instance.GetComponentInChildren<SpriteRenderer>();
            _animator = instance.GetComponentInChildren<Animator>();

            if (_renderer != null)
            {
                _renderer.sortingOrder = sortingOrder;

                var height = _renderer.sprite != null ? _renderer.sprite.bounds.size.y : 0f;
                if (height <= 0f) height = 1f;

                var target = _grid != null ? _grid.CellSize * actorHeightInCells : actorHeightInCells;
                instance.transform.localScale = Vector3.one * (target / height);
            }

            _actor = instance.transform;

            // 出生时正面朝镜头（下），走起来后由 AiMoved 按实际方向更新
            if (_animator != null)
            {
                _animator.SetInteger(DirHash, (int)MoveDirection.Down);
                _animator.SetBool(MovingHash, false);
            }
        }

        /// <summary>
        /// 元素防护罩：包在角色外的一层半透明光罩，按当前元素属性染色（火=暖红 / 冰=亮蓝），
        /// 属性反转的瞬间做一次放大脉冲，让「外观同步切换」一眼可辨。贴图未配置时整体跳过。
        /// </summary>
        void BuildShield()
        {
            if (shieldSprite == null || _actor == null) return;

            var height = _renderer != null && _renderer.sprite != null ? _renderer.sprite.bounds.size.y : 1f;
            var target = height * shieldHeightRatio;
            var spriteSize = shieldSprite.bounds.size.x;
            var parentScale = _actor.localScale.x;

            var shieldObject = new GameObject("ElementShield");
            shieldObject.transform.SetParent(_actor, false);
            // 角色锚点在脚底（BottomCenter），光罩中心抬到半身高处才能把角色包住
            shieldObject.transform.localPosition = Vector3.up * (height * 0.5f);

            _shield = shieldObject.AddComponent<SpriteRenderer>();
            _shield.sprite = shieldSprite;
            _shield.sortingOrder = sortingOrder + 1;

            _shieldBaseScale = Vector3.one * (
                target / (spriteSize > 0f ? spriteSize : 1f) / (parentScale > 0f ? parentScale : 1f));
            _shield.transform.localScale = _shieldBaseScale;
        }

        /// <summary>每帧跟随当前元素属性染色；检测到变化时触发一次放大脉冲。</summary>
        void UpdateShield(float delta)
        {
            if (_shield == null || _run == null) return;

            var element = _run.Element.Value;
            if (element != _shownElement)
            {
                _shownElement = element;
                _shieldPulse = 1f;
            }

            _shieldPulse = Mathf.Max(0f, _shieldPulse - delta * 2.5f);

            _shield.color = element == ElementState.Fire ? shieldColorFire : shieldColorIce;
            _shield.transform.localScale = _shieldBaseScale * (1f + 0.22f * _shieldPulse);
        }

        /// <summary>
        /// 驱动动画：Moving 只跟运行阶段挂钩（不走「补间是否在跑」），
        /// 否则每格补间结束到下一步之间的那一帧会闪回待机、看着像卡顿。
        /// </summary>
        void ApplyAnimator()
        {
            if (_animator == null || _run == null) return;

            _animator.SetBool(MovingHash, _run.IsRunning);
        }
    }
}

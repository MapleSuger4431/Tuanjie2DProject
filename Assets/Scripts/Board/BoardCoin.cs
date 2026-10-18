using System.Collections;
using System.Collections.Generic;
using Runestone.AesirArchitecture;
using UnityEngine;

namespace Game.Board
{
    /// <summary>
    /// 关卡金币：挂在棋盘根（<see cref="BoardGridView" /> 同物体）下，**固定摆在一个格子上**，
    /// 位置在 Inspector 里配（每关的固定点，不随机刷新）。一个关卡可以摆多枚（第 3~5 关各 2 枚），
    /// 每枚是一份本组件；AI 走过该格即拾取，拾取数写进 <see cref="BoardRunModel.CoinsCollected" />
    /// （重开由运行模型调 <see cref="ResetAll" /> 复位）。
    /// <para>
    /// 金币不参与通行判定——它只是一枚叠在格子上的拾取物，铺路时该格照常铺。
    /// 有金币的格子初始通常是空地，玩家得把它铺成可行走的路才能让 AI 走过去捡。
    /// </para>
    /// <para>
    /// 多枚注册：静态表 <see cref="All" /> 由每枚自己在 OnEnable/OnDisable 里登记，
    /// 运行模型只问「这一格上有没有没被捡的金币」与「一共几枚 / 已捡几枚」，不持具体引用。
    /// </para>
    /// </summary>
    [ExecuteAlways]
    public sealed class BoardCoin : MonoViewController<BoardContext>
    {
        const string IconName = "CoinIcon";

        [SerializeField]
        [Tooltip("金币所在格：关卡设计固定，运行时不变（重开也回同一格）")]
        Vector2Int cell = new Vector2Int(3, 6);

        [SerializeField]
        [Tooltip("金币图标（项目美术：Assets/Art/Board/Items/Item_Coin.png）")]
        Sprite coinSprite;

        [SerializeField, Tooltip("金币占格子的比例")]
        float sizeInCells = 0.7f;

        [SerializeField, Tooltip("拾取动画时长（秒）")]
        float collectDuration = 0.4f;

        [SerializeField, Tooltip("拾取时向上浮起的高度（格）")]
        float riseInCells = 0.8f;

        SpriteRenderer _renderer;
        BoardGridView _grid;
        Coroutine _animation;

        static readonly List<BoardCoin> All = new List<BoardCoin>();

        /// <summary>本关摆了几枚金币（三星制的第二、三颗星判据分母）。</summary>
        public static int TotalCount => All.Count;

        /// <summary>本局已捡到几枚。</summary>
        public static int CollectedCount
        {
            get
            {
                var count = 0;
                for (var i = 0; i < All.Count; i++)
                    if (All[i] != null && All[i].IsCollected) count++;

                return count;
            }
        }

        /// <summary>本关的金币是否已被全部捡到（没摆金币的关卡恒为 false，由三星规则另判）。</summary>
        public static bool AllCollected => All.Count > 0 && CollectedCount >= All.Count;

        /// <summary>本枚是否已被捡走（重开由 <see cref="ResetAll" /> 复位）。</summary>
        public bool IsCollected { get; private set; }

        public Vector2Int Cell => cell;

        // ---------- 多枚注册 ----------

        void OnEnable()
        {
            if (!All.Contains(this)) All.Add(this);
        }

        void OnDisable() => All.Remove(this);

        /// <summary>域重载会留下上一场的静态表，进入播放前清空由 OnEnable 重新登记。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRegistry() => All.Clear();

        /// <summary>找出该格上还没被捡走的金币（同一格理论上只摆一枚）。</summary>
        public static BoardCoin FindUncollected(Vector2Int cell)
        {
            for (var i = 0; i < All.Count; i++)
            {
                var coin = All[i];
                if (coin != null && !coin.IsCollected && coin.cell == cell) return coin;
            }

            return null;
        }

        /// <summary>全部恢复为未拾取（关卡重开）。</summary>
        public static void ResetAll()
        {
            for (var i = 0; i < All.Count; i++) All[i]?.Restore();
        }

        // ---------- 生命周期 ----------

        void Start()
        {
            _grid = GetComponentInParent<BoardGridView>();
            EnsureRenderer();
            ResetVisual();
        }

        void OnValidate()
        {
            if (_grid == null) _grid = GetComponentInParent<BoardGridView>();

            PlaceIcon();
        }

        // ---------- 拾取 ----------

        /// <summary>标记本枚已拾取并播表现；数据层的计数由 <see cref="BoardRunModel" /> 负责累加。</summary>
        public void MarkCollected()
        {
            if (IsCollected) return;

            IsCollected = true;

            if (_renderer == null) EnsureRenderer();
            if (_renderer == null) return;

            if (_animation != null)
            {
                StopCoroutine(_animation);
                _animation = null;
            }

            _animation = StartCoroutine(PlayCollect());
        }

        /// <summary>恢复成未拾取的样子并重新摆位（不叫 Reset，那是 Unity 的编辑器消息名）。</summary>
        void Restore()
        {
            IsCollected = false;

            if (_animation != null)
            {
                StopCoroutine(_animation);
                _animation = null;
            }

            ResetVisual();
        }

        // ---------- 表现 ----------

        /// <summary>拾取表现：上浮 + 放大 + 淡出，播完隐藏（数据层状态不变，重开由 ResetAll 走回这里）。</summary>
        IEnumerator PlayCollect()
        {
            var renderer = _renderer;
            var from = renderer.transform.localPosition;
            var to = from + Vector3.up * RiseDistance;
            var baseScale = renderer.transform.localScale;
            var color = renderer.color;
            var duration = Mathf.Max(0.016f, collectDuration);

            renderer.enabled = true;

            var time = 0f;
            while (time < duration)
            {
                // 未暂停的缩放动画用 unscaledDeltaTime：结算面板弹出时 timeScale 可能被改动
                time += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(time / duration);
                renderer.transform.localPosition = Vector3.Lerp(from, to, t);
                renderer.transform.localScale = baseScale * (1f + 0.45f * t);
                renderer.color = new Color(color.r, color.g, color.b, 1f - t);
                yield return null;
            }

            renderer.enabled = false;
            _animation = null;
        }

        void ResetVisual()
        {
            var renderer = _renderer;
            if (renderer == null) return;

            renderer.color = Color.white;
            renderer.enabled = coinSprite != null;
            PlaceIcon();
        }

        void PlaceIcon()
        {
            var renderer = EnsureRenderer();
            if (renderer == null) return;

            renderer.sprite = coinSprite;
            renderer.transform.localPosition =
                _grid != null ? (Vector3)_grid.CellToLocal(cell) : new Vector3(cell.x, cell.y, 0f);
            renderer.transform.localScale = Vector3.one * ScaleForIcon(renderer.sprite);
        }

        /// <summary>图标世界尺寸 = sizeInCells 个格子：父级已按棋盘缩放过，这里按世界尺寸反推本地缩放。</summary>
        float ScaleForIcon(Sprite sprite)
        {
            if (_grid == null) return 1f;

            var parentScale = transform.lossyScale.x;
            var unit = sprite != null ? sprite.bounds.size.x : 1f;
            if (parentScale <= 1e-4f || unit <= 1e-4f) return 1f;

            return _grid.CellSize * sizeInCells / (unit * parentScale);
        }

        float RiseDistance => _grid != null ? _grid.CellSize * riseInCells : riseInCells;

        SpriteRenderer EnsureRenderer()
        {
            if (_renderer != null) return _renderer;

            var existing = transform.Find(IconName);
            if (existing != null) _renderer = existing.GetComponent<SpriteRenderer>();

            if (_renderer == null)
            {
                var go = new GameObject(IconName);
                go.transform.SetParent(transform, false);
                go.transform.localPosition = Vector3.zero;

                _renderer = go.AddComponent<SpriteRenderer>();
            }

            // 压在格子与效果图标之上：格子 0、效果图标 +1，这里 +2
            _renderer.sortingOrder = TileSortingOrder + 2;
            return _renderer;
        }

        int TileSortingOrder
        {
            get
            {
                if (_grid == null) return 0;

                var tile = _grid.TileAt(cell);
                var renderer = tile != null ? tile.GetComponent<SpriteRenderer>() : null;
                return renderer != null ? renderer.sortingOrder : 0;
            }
        }
    }
}

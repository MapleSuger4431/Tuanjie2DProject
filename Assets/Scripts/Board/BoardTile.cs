using UnityEngine;

namespace Game.Board
{
    /// <summary>
    /// 单个棋盘格：坐标 + 当前状态 + 渲染 + 附带效果（两层：通行状态 <see cref="TileType" /> 与效果层 <see cref="TileEffect" />）。
    /// 关卡预设写在序列化的 <see cref="type" /> 与 <see cref="effect" /> 上（Inspector 里逐格配）；
    /// 运行期铺设走 <see cref="SetRuntimeType" /> 覆盖显示，不写回序列化值，Play 退出后预设原样保留。
    /// 效果层不改变通行性——带传送门的格子仍然是普通可走路。
    /// </summary>
    [ExecuteAlways]
    public sealed class BoardTile : MonoBehaviour
    {
        const string EffectIconName = "EffectIcon";

        /// <summary>效果已消耗时的置灰色（传送门用过即变灰）。</summary>
        static readonly Color ConsumedColor = new Color(0.4f, 0.4f, 0.4f, 0.6f);

        [SerializeField] Vector2Int cell;

        [SerializeField] TileType type = TileType.Void;

        [Tooltip("关卡预置的效果资产（多态：同一效果不同参数就是不同的 .asset）。为空即本格无效果")]
        [SerializeField] TileEffect effect;

        [Tooltip("勾选后：方块铺到本格也不覆盖这个预置效果（数据层的值被忽略）")]
        [SerializeField] bool pinEffect;

        [SerializeField] SpriteRenderer tileRenderer;

        [Tooltip("效果图标的渲染器；为空时按需在子物体上创建")]
        [SerializeField] SpriteRenderer effectRenderer;

        TileType? _runtimeType;

        public Vector2Int Cell => cell;

        /// <summary>关卡预设状态（编辑器配置，运行期不被改写）。</summary>
        public TileType PresetType => type;

        /// <summary>关卡预置效果（编辑器配置，运行期不被改写）。</summary>
        public TileEffect PresetEffect => effect;

        /// <summary>该格的预置效果是否被钉死（方块覆盖不掉）。</summary>
        public bool PinEffect => pinEffect;

        /// <summary>写入附带效果（关卡画笔与编辑器工具用）。</summary>
        public void SetEffect(TileEffect value) => effect = value;

        /// <summary>设置是否钉死预置效果。</summary>
        public void SetPinned(bool value) => pinEffect = value;

        /// <summary>当前生效状态：运行期覆盖优先，否则回落预设。</summary>
        public TileType CurrentType => _runtimeType ?? type;

        /// <summary>写入坐标与预设状态并立即刷新外观（生成工具用）。</summary>
        public void Configure(Vector2Int cellCoord, TileType preset)
        {
            cell = cellCoord;
            type = preset;
            _runtimeType = null;
            ApplyVisual();
        }

        /// <summary>运行期覆盖显示；传 null 恢复预设。</summary>
        public void SetRuntimeType(TileType? runtime)
        {
            _runtimeType = runtime;
            ApplyVisual();
        }

        public void ResetToPreset() => SetRuntimeType(null);

        /// <summary>按当前生效状态刷新渲染：贴图由所属棋盘视图提供，没配美术时回落纯色 + 分类色。</summary>
        public void ApplyVisual()
        {
            if (tileRenderer == null) tileRenderer = GetComponent<SpriteRenderer>();
            if (tileRenderer == null) return;

            var view = View;
            var sprite = view != null ? view.SpriteFor(CurrentType) : null;

            if (sprite != null) tileRenderer.sprite = sprite;
            tileRenderer.color = view != null && sprite != null
                ? view.TintFor(CurrentType)
                : TileTypeInfo.Color(CurrentType);
        }

        /// <summary>
        /// 刷新效果图标（由 <see cref="BoardRoot.Refresh" /> 逐格调用）；无效果则隐藏，已消耗则置灰。
        /// </summary>
        public void ApplyEffectVisual(TileEffect runtimeEffect, bool consumed)
        {
            if (runtimeEffect == null || runtimeEffect.Icon == null)
            {
                if (effectRenderer != null) effectRenderer.enabled = false;
                return;
            }

            var renderer = EnsureEffectRenderer();
            if (renderer == null) return;

            renderer.sprite = runtimeEffect.Icon;
            renderer.enabled = true;
            renderer.color = consumed ? ConsumedColor : Color.white;
        }

        SpriteRenderer EnsureEffectRenderer()
        {
            if (effectRenderer != null) return effectRenderer;

            var existing = transform.Find(EffectIconName);
            if (existing != null) effectRenderer = existing.GetComponent<SpriteRenderer>();
            if (effectRenderer != null) return effectRenderer;

            var go = new GameObject(EffectIconName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            // 父级已按格子尺寸缩过，这里只取相对比例
            go.transform.localScale = Vector3.one * 0.5f;

            if (tileRenderer == null) tileRenderer = GetComponent<SpriteRenderer>();

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = (tileRenderer != null ? tileRenderer.sortingOrder : 0) + 1;

            effectRenderer = renderer;
            return effectRenderer;
        }

        BoardGridView View =>
            _view != null ? _view : (_view = GetComponentInParent<BoardGridView>());

        BoardGridView _view;

        void OnValidate() => ApplyVisual();

        void Awake() => ApplyVisual();
    }
}

using UnityEngine;

namespace Game.Board
{
    /// <summary>
    /// 单个棋盘格：坐标 + 当前状态 + 渲染。
    /// 关卡预设写在序列化的 <see cref="type" /> 上（编辑器工具生成、Inspector 里逐格改）；
    /// 运行期铺设走 <see cref="SetRuntimeType" /> 覆盖显示，不写回序列化值，Play 退出后预设原样保留。
    /// </summary>
    [ExecuteAlways]
    public sealed class BoardTile : MonoBehaviour
    {
        [SerializeField] Vector2Int cell;
        [SerializeField] TileType type = TileType.Void;
        [SerializeField] CellEffect effect = CellEffect.None;
        [SerializeField] SpriteRenderer tileRenderer;

        TileType? _runtimeType;

        public Vector2Int Cell => cell;

        /// <summary>关卡预设状态（编辑器配置，运行期不被改写）。</summary>
        public TileType PresetType => type;

        /// <summary>格子附带的效果（Inspector 里逐格选，运行期不改写）。</summary>
        public CellEffect Effect => effect;

        /// <summary>写入附带效果；效果当前只做关卡配置，读法由后续玩法决定。</summary>
        public void SetEffect(CellEffect value) => effect = value;

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

        BoardGridView View =>
            _view != null ? _view : (_view = GetComponentInParent<BoardGridView>());

        BoardGridView _view;

        void OnValidate() => ApplyVisual();

        void Awake() => ApplyVisual();
    }
}

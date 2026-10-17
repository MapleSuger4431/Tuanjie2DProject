using UnityEngine;

namespace GameRuntimeTest.Board
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
        [SerializeField] SpriteRenderer tileRenderer;

        TileType? _runtimeType;

        public Vector2Int Cell => cell;

        /// <summary>关卡预设状态（编辑器配置，运行期不被改写）。</summary>
        public TileType PresetType => type;

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

        /// <summary>按当前生效状态刷新渲染。</summary>
        public void ApplyVisual()
        {
            if (tileRenderer == null) tileRenderer = GetComponent<SpriteRenderer>();
            if (tileRenderer != null) tileRenderer.color = TileTypeInfo.Color(CurrentType);
        }

        void OnValidate() => ApplyVisual();

        void Awake() => ApplyVisual();
    }
}

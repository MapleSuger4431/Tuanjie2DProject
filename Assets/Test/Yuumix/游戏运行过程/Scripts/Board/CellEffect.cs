using UnityEngine;

namespace GameRuntimeTest.Board
{
    /// <summary>
    /// 格子附带的效果：所有效果统一由本枚举管理，新增一个效果 = 新增一项枚举成员（不要另起枚举或用字符串）。
    /// 约定：①底层类型固定 int —— Unity 序列化只支持 int 系底层类型的枚举，
    /// 声明为 long 会让 BoardTile.effect 报 "Unsupported enum type" 且该字段根本存不进场景，故不能改回 long；
    /// ②每个成员都显式写 int 数值，禁止依赖自增，避免插入新效果时旧配置的数值漂移；
    /// ③每个成员都带 <see cref="InspectorName" /> 中文命名，Inspector 下拉直接显示中文。
    /// 首项 None（无）固定为 0，是默认占位：子物体没配效果时就是它。
    /// </summary>
    public enum CellEffect
    {
        /// <summary>无效果（默认占位）。</summary>
        [InspectorName("无")] None = 0,

        /// <summary>转化：踩上去后把自身属性转化成格子属性。</summary>
        [InspectorName("转化")] Convert = 1,

        /// <summary>传送门：成对传送，落到配对格继续走。</summary>
        [InspectorName("传送门")] Portal = 2,

        /// <summary>位移：把 AI 推向固定方向若干格。</summary>
        [InspectorName("位移")] Shift = 3,

        /// <summary>滑行：沿当前方向持续滑到不能再走为止。</summary>
        [InspectorName("滑行")] Slide = 4,
    }
}

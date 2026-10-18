using UnityEngine;

namespace Game.Board
{
    /// <summary>效果对外的唯一契约：AI 踏入该格时触发一次。</summary>
    public interface ITileEffect
    {
        /// <summary>展示名（日志、调试、未来 UI 提示用）。</summary>
        string DisplayName { get; }

        /// <summary>叠在格子上的图标；为空则只生效、不显示。</summary>
        Sprite Icon { get; }

        /// <summary>
        /// AI 进入本格时调用一次。只允许往 <see cref="TileEffectContext.Result" /> 里填结果，
        /// 由 <see cref="BoardRunModel" /> 统一应用——效果自己不许改棋盘、不许改 AI 位置。
        /// </summary>
        void OnEnter(TileEffectContext ctx);
    }

    /// <summary>
    /// 格子效果层：与通行状态（<see cref="TileType" />）并列的第二层，一层恒为 0 或 1 个效果，
    /// 且**不改变通行性**——带转向箭头的格子仍然是普通可走格。
    /// <para>
    /// 效果以 ScriptableObject 资产实现多态：字段即参数，同类型不同参数就是多份 .asset
    ///（如强制转向的四个方向）。新增效果 = 新增子类 + 建资产，不改动棋盘与运行模型。
    /// </para>
    /// <para>
    /// 红线：运行期状态（传送门已消耗等）一律不许写进本类字段——SO 全局共享，
    /// 会跨格、跨关、跨 Play 会话污染，且退出 Play 不还原。运行期状态存在 <see cref="BoardModel" /> 的数据层。
    /// </para>
    /// </summary>
    public abstract class TileEffect : ScriptableObject, ITileEffect
    {
        [SerializeField, Tooltip("中文名，日志与调试用；留空则回落资产名")]
        string displayName;

        [SerializeField, Tooltip("叠在格子上的图标；为空则只生效、不显示")]
        Sprite icon;

        [SerializeField, Tooltip("棋盘格上的方向提示图标（按格子尺寸满格显示，转向箭头用金色数字款）；为空则回落 icon")]
        Sprite boardIcon;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;

        public Sprite Icon => icon;

        /// <summary>棋盘格上实际显示的提示图标：优先棋盘提示款，未配则回落 icon。</summary>
        public Sprite BoardIcon => boardIcon != null ? boardIcon : icon;

        /// <summary>
        /// 是否为传送门：棋盘根找配对门时按此判定。
        /// 用虚属性而不是在查询侧写 <c>is PortalEffect</c>，是为了让查询代码不依赖具体效果类——新增效果零改棋盘根。
        /// </summary>
        public virtual bool IsPortal => false;

        public abstract void OnEnter(TileEffectContext ctx);
    }
}

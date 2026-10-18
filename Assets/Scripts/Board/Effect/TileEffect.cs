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

        /// <summary>
        /// 随方块顺时针旋转 90° 后本格应该变成的效果；默认返回自身——大多数效果与朝向无关
        ///（传送门、属性反转转前转后是同一个作用）。有朝向的效果（强制转向箭头）必须重写，
        /// 否则方块转过来了、箭头还指着原来的方向。
        /// <para>
        /// 必须返回**另一份资产**而不是就地改字段：效果资产是全局共享的 ScriptableObject，
        /// 就地改方向会污染所有引用它的格子、关卡与后续 Play 会话。
        /// </para>
        /// </summary>
        public virtual TileEffect RotatedCW() => this;

        public abstract void OnEnter(TileEffectContext ctx);
    }
}

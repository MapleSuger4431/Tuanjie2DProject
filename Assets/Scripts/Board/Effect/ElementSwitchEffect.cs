using UnityEngine;

namespace Game.Board
{
    /// <summary>
    /// 属性反转（元素转换效果，06 篇 RB-14）：AI 踏入本格后，元素属性在 火 ↔ 冰 之间**直接反转**，
    /// 反转立即影响后续每一步的通行判定——原本走不了的火 / 冰路就此变为可走。
    /// <para>
    /// 反转式语义（用户 2026-10-18 拍板）：全场共用一份资产即可——效果读 <see cref="AiPawnState.Element" />
    /// 只读快照算出目标属性、填进 <see cref="TileEffectResult.SetElement" />，由 <see cref="BoardRunModel" />
    /// 统一改写 AI 属性，效果自己不持有任何状态。
    /// </para>
    /// <para>
    /// 不消耗：同一格每次被踏上都会再次生效（与转向箭头一致；不同于传送门的「配对后两端失效」）。
    /// 效果层不改变通行性：带反转标记的格子本身仍按通行层（路 / 火 / 冰）正常判定。
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Board/效果/属性反转", fileName = "Element_Invert")]
    public sealed class ElementSwitchEffect : TileEffect
    {
        public override void OnEnter(TileEffectContext ctx) =>
            ctx.Result.SetElement = ElementStateInfo.Other(ctx.Pawn.Element);

        /// <summary>资产统一放 Resources 的这个路径下，方块发牌时取用。</summary>
        public const string ResourcesPath = "Board/Effects/Element_Invert";

        static ElementSwitchEffect _cached;

        /// <summary>
        /// 全场共用的那份反转资产（无参数，发牌侧直接取用）；Resources 下缺失时返回 null，
        /// 调用方应跳过本次反转格——不现造实例，避免丢图标。
        /// </summary>
        public static ElementSwitchEffect Asset
        {
            get
            {
                if (_cached == null) _cached = Resources.Load<ElementSwitchEffect>(ResourcesPath);
                return _cached;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() => _cached = null;
    }
}

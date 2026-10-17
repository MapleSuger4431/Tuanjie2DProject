using UnityEngine;

namespace Game.Board
{
    /// <summary>
    /// 传送门：成对出现，AI 踏入其中一端即被送到「离它最近的另一端」，两端随后同时失效。
    /// <para>
    /// 无参数——全场共用一个资产即可（同类型不同参数才需要多份 .asset）。
    /// 「已消耗」这类运行期状态存在 <see cref="BoardModel" /> 的数据层，不写进资产，
    /// 否则会跨格、跨关、跨 Play 会话污染，且退出 Play 不还原。
    /// </para>
    /// <para>
    /// 边界口径：①找不到未消耗的配对门时，等同普通路格（不传送、不判负）；
    /// ②传送后前进方向不变、来路清空（<see cref="TileEffectResult.ClearIncoming" />），落点不再结算效果
    /// ——一步最多一个效果，天然防止 A↔B 互相传送的死循环。
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Board/效果/传送门", fileName = "Portal")]
    public sealed class PortalEffect : TileEffect
    {
        public override bool IsPortal => true;

        public override void OnEnter(TileEffectContext ctx)
        {
            var board = ctx.Board;

            // 没有可配对的门（或未配对的已用完）：本格就当普通路格
            if (board == null || !board.TryFindNearestPortal(ctx.Cell, out var target)) return;

            ctx.Result.TeleportTo = target;
            ctx.Result.ClearIncoming = true;

            // 配对后两端同时失效：入口在这一步就用掉了，出口也不再接受传送
            board.Consume(ctx.Cell);
            board.Consume(target);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Game.Board
{
    /// <summary>
    /// 强制转向（箭头格）：AI 踏入本格后，下一步无条件朝 <see cref="Direction" />——
    /// 优先级高于「直行」与「出生方向」，且允许破例走回来路格（<see cref="TileEffectResult.ForcedNext" /> 的既定语义）。
    /// 效果本身不改变通行性：带箭头的格子仍然是普通可走格。
    /// <para>
    /// 不消耗：同一格每次被踏上都会生效。所以发牌侧禁止一块里出现互为反向的两个箭头（不对冲），
    /// 关卡与摆法上也要避开两个互指的箭头把 AI 锁成死循环——运行侧另有步数上限兜底。
    /// </para>
    /// 四个方向各一份 .asset（Force_Up / Force_Down / Force_Left / Force_Right），图标各自朝向对应方向
    ///（同类型不同参数就是多份资产，不靠旋转，渲染侧因此无需知道方向）。
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Board/效果/强制转向", fileName = "Force_Up")]
    public sealed class ForcedTurnEffect : TileEffect
    {
        /// <summary>四份资产统一放 Resources 的这个目录下，发牌时按方向取用。</summary>
        public const string ResourcesFolder = "Board/Effects";

        [SerializeField] MoveDirection direction = MoveDirection.Up;

        static readonly Dictionary<MoveDirection, ForcedTurnEffect> Cache =
            new Dictionary<MoveDirection, ForcedTurnEffect>();

        public MoveDirection Direction => direction;

        public override void OnEnter(TileEffectContext ctx) => ctx.Result.ForcedNext = direction;

        /// <summary>
        /// 方块顺时针转 90° 时箭头跟着转：本资产本身**不改**，换成旋转后那方向对应的资产（上→右→下→左）。
        /// 缺资产时 <see cref="For" /> 给出同方向的运行期实例，转向逻辑照样成立，只是没图标。
        /// </summary>
        public override TileEffect RotatedCW() => For(MoveDirectionInfo.RotateCW(direction));

        /// <summary>取该方向的资产；Resources 下缺失时回落到运行期现造的实例（无图标，转向逻辑仍生效）。</summary>
        public static ForcedTurnEffect For(MoveDirection direction)
        {
            if (Cache.Count == 0) RefreshCache();

            ForcedTurnEffect found;
            if (Cache.TryGetValue(direction, out found) && found != null) return found;

            var fallback = CreateInstance<ForcedTurnEffect>();
            fallback.direction = direction;
            fallback.hideFlags = HideFlags.DontSave;
            return fallback;
        }

        /// <summary>四个方向各一份（缺失的方向补运行期实例），方块发牌时从这里抽。</summary>
        public static ForcedTurnEffect[] LoadAll()
        {
            if (Cache.Count == 0) RefreshCache();

            var result = new ForcedTurnEffect[MoveDirectionInfo.Count];
            for (var i = 0; i < result.Length; i++) result[i] = For(MoveDirectionInfo.All[i]);

            return result;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() => Cache.Clear();

        static void RefreshCache()
        {
            Cache.Clear();

            var loaded = Resources.LoadAll<ForcedTurnEffect>(ResourcesFolder);
            for (var i = 0; i < loaded.Length; i++)
            {
                var effect = loaded[i];
                if (effect == null) continue;

                Cache[effect.direction] = effect;
            }

            if (loaded.Length == 0)
                Debug.LogWarning(
                    $"[ForcedTurnEffect] Resources/{ResourcesFolder} 下没有强制转向资产，转向箭头只生效、不显示图标");
        }
    }
}

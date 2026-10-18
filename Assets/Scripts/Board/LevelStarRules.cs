namespace Game.Board
{
    /// <summary>
    /// 三星评定规则（唯一口径，UI / 存档 / 服务都调这里）：
    /// <list type="number">
    /// <item>通关即 1 星（失败 0 星）；</item>
    /// <item>本关金币**全部**捡到 +1 星（第 3~5 关各有 2 枚，只捡一枚不算）；</item>
    /// <item>金币全捡到 **且** 在关卡时限内通关 +1 星。</item>
    /// </list>
    /// 时限来自 <see cref="LevelStarConfig" />（逐关配置，难度曲线 30→70 秒，每关 +10 秒）；
    /// 超时**不判负**——照样能通关，只是拿不到第三颗星。
    /// 金币是本局运行态（<see cref="BoardRunModel.CoinsCollected" /> / <see cref="BoardRunModel.CoinTotal" />），重开即复位。
    /// </summary>
    public static class LevelStarRules
    {
        /// <summary>满星数。</summary>
        public const int MaxStars = 3;

        /// <summary>
        /// 评定本局星级。
        /// </summary>
        /// <param name="cleared">本局是否通关（未通关恒 0 星）。</param>
        /// <param name="coinsCollected">本局捡到的金币枚数。</param>
        /// <param name="coinTotal">本关摆了几枚金币；&lt;=0 视为本关没有金币要求，金币条件自动满足。</param>
        /// <param name="seconds">本局用时（秒），从进入关卡铺路起算。</param>
        /// <param name="timeLimitSeconds">第三星时限（秒）；&lt;=0 视为不限时，金币全拿到即给第 3 星。</param>
        public static int Evaluate(bool cleared, int coinsCollected, int coinTotal, float seconds, float timeLimitSeconds)
        {
            if (!cleared) return 0;

            var stars = 1;

            // 第二颗星：本关所有金币都要捡到
            if (coinTotal > 0 && coinsCollected < coinTotal) return stars;

            stars += 1;
            if (timeLimitSeconds <= 0f || seconds <= timeLimitSeconds) stars += 1;

            return stars;
        }
    }
}

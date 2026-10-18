using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Model
{
    /// <summary>成就条件比较方式：数值达到阈值即解锁（04 篇 AC-02 / AC-03）。</summary>
    public enum AchievementCompare
    {
        /// <summary>上报值 ≥ 阈值解锁（步数、累计次数这类「越多越好」）。</summary>
        GreaterOrEqual = 0,

        /// <summary>上报值 ≤ 阈值解锁（用时、用块数这类「越少越好」）。</summary>
        LessOrEqual = 1,
    }

    /// <summary>关卡与玩法侧上报的事件名常量（成就配置表按这些键挂条件）。</summary>
    public static class AchievementEvents
    {
        /// <summary>本关 AI 已走步数（每步上报当前值）。</summary>
        public const string StepsInLevel = "StepsInLevel";

        /// <summary>通关一次（值恒为 1）。</summary>
        public const string LevelCleared = "LevelCleared";

        /// <summary>本关发生过元素属性反转（值恒为 1）。</summary>
        public const string ElementSwitched = "ElementSwitched";

        /// <summary>历史累计通关次数（由关卡进度记录汇总）。</summary>
        public const string TotalClears = "TotalClears";
    }

    /// <summary>
    /// 单条成就定义（ScriptableObject 资产，数据驱动：新增成就 = 加一个 .asset，不改代码）。
    /// 文本全部走本地化键名（<see cref="TitleKey" /> / <see cref="DescKey" />），
    /// 预制体与面板里不写死任何文案。
    /// </summary>
    public sealed class GameAchievement : ScriptableObject
    {
        [Tooltip("唯一标识（如 steps_30）；入库后不可改名，改了就等于换一条成就")]
        [SerializeField] string id = string.Empty;

        [Tooltip("成就名的本地化键名（GameText 表条目）")]
        [SerializeField] string titleKey = string.Empty;

        [Tooltip("解锁条件描述的本地化键名（对玩家可见）")]
        [SerializeField] string descKey = string.Empty;

        [Tooltip("成就图标；未解锁时置灰显示")]
        [SerializeField] Sprite icon;

        [Tooltip("监听的事件名（AchievementEvents 常量）")]
        [SerializeField] string eventKey = string.Empty;

        [Tooltip("阈值：配合 Compare 判定")]
        [SerializeField] int threshold = 1;

        [SerializeField] AchievementCompare compare = AchievementCompare.GreaterOrEqual;

        [Tooltip("隐藏成就：解锁前在图鉴里只显示 ???")]
        [SerializeField] bool hidden;

        public string Id => id;
        public string TitleKey => titleKey;
        public string DescKey => descKey;
        public Sprite Icon => icon;
        public string EventKey => eventKey;
        public int Threshold => threshold;
        public bool Hidden => hidden;

        /// <summary>上报值是否满足条件（唯一判定入口，别在别处复制这套比较）。</summary>
        public bool IsSatisfied(int value) =>
            compare == AchievementCompare.LessOrEqual ? value <= threshold : value >= threshold;
    }
}

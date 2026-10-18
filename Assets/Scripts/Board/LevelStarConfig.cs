using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Board
{
    /// <summary>单关对第三颗星时限的覆盖项（没列的关卡用全局默认值）。</summary>
    [Serializable]
    public sealed class LevelStarEntry
    {
        [Tooltip("关卡场景名（与 LevelSelectModel 的 sceneName 一致，如 Level_01）")]
        public string sceneName;

        [Tooltip("本关第三颗星的时限（秒）：捡齐所有金币且在这个时间内通关才给第 3 星。<=0 表示沿用全局默认值")]
        public float timeStarSeconds = 30f;
    }

    /// <summary>
    /// 三星时限配置（唯一真源，Inspector 里改，无需改代码）：
    /// 全局默认 30 秒 + 逐关覆盖（覆盖项只填需要特殊时限的关卡）。
    /// <para>
    /// 三星规则本身见 <see cref="LevelStarRules" />——通关 1 星、金币全部捡到 +1 星、
    /// 金币全部捡到且在这个时间内通关 +1 星；超时不判负，只是拿不到第三颗星。本资产只提供「时限」这一个可配量。
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Board/关卡三星配置", fileName = "LevelStarConfig")]
    public sealed class LevelStarConfig : ScriptableObject
    {
        /// <summary>Resources 加载路径（项目约定：配置资产走 Resources，本地化资源才走 Addressables）。</summary>
        public const string ResourcesPath = "Board/LevelStarConfig";

        [SerializeField]
        [Tooltip("第三颗星的全局默认时限（秒）：所有没在下面列表里单列的关卡都用它")]
        float defaultTimeStarSeconds = 30f;

        [SerializeField]
        [Tooltip("逐关覆盖：只列时限与全局不同的关卡，其余关卡不用填")]
        List<LevelStarEntry> levels = new List<LevelStarEntry>();

        static LevelStarConfig _cached;

        public float DefaultTimeStarSeconds => defaultTimeStarSeconds;

        public IReadOnlyList<LevelStarEntry> Levels => levels;

        /// <summary>缓存加载：域重载会清空静态字段，故每次判空再 Load。</summary>
        public static LevelStarConfig Asset
        {
            get
            {
                if (_cached == null) _cached = Resources.Load<LevelStarConfig>(ResourcesPath);
                return _cached;
            }
        }

        /// <summary>该关的第三星时限（秒）；没配过就返回全局默认。</summary>
        public float TimeLimitFor(string sceneName)
        {
            if (!string.IsNullOrEmpty(sceneName) && levels != null)
            {
                foreach (var entry in levels)
                {
                    if (entry == null || entry.sceneName != sceneName) continue;
                    if (entry.timeStarSeconds > 0f) return entry.timeStarSeconds;
                }
            }

            return defaultTimeStarSeconds;
        }
    }
}

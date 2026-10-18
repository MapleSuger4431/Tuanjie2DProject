using System.Collections.Generic;
using UnityEngine;

namespace Game.Model
{
    /// <summary>
    /// 成就配置库：一列 <see cref="GameAchievement" /> 资产（顺序即图鉴展示顺序）。
    /// 运行时由 <see cref="Game.Service.AchievementService" /> 从 Resources 加载，路径见 <see cref="ResourcesPath" />。
    /// <para>
    /// 独立成文件是 Unity 的硬要求：ScriptableObject 类名必须与 .cs 文件名一致，
    /// 否则写盘时 m_Script 引用丢失（加载回来为 null，报 "No script asset for ..."）。
    /// </para>
    /// </summary>
    public sealed class AchievementLibrary : ScriptableObject
    {
        /// <summary>Resources 加载路径（不含扩展名）。</summary>
        public const string ResourcesPath = "Achievement/AchievementLibrary";

        [Tooltip("全部成就定义；顺序即图鉴里的展示顺序")]
        [SerializeField] List<GameAchievement> items = new List<GameAchievement>();

        public IReadOnlyList<GameAchievement> Items => items;
    }
}

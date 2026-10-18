using System;
using System.Collections.Generic;
using Game.Model;
using Runestone.AesirArchitecture;
using UnityEngine;

namespace Game.Service
{
    /// <summary>
    /// 成就服务：配置表（<see cref="AchievementLibrary" />）与状态（<see cref="AchievementModel" />）之间的判定层。
    /// <para>
    /// 玩法侧只管「上报发生了什么」：<c>Report(AchievementEvents.StepsInLevel, 30)</c>；
    /// 判定、防重复、落盘（经 Model 的 Changed → SaveService）全在这里，新增成就不需要改本类。
    /// </para>
    /// <para>
    /// 解锁只抛 <see cref="Unlocked" /> 事件，**不碰 UI**：弹右下角小窗是关卡侧订阅者的职责
    /// （见 <c>Game.Board.BoardAchievementReporter</c>）——服务层不反向依赖玩法与界面。
    /// </para>
    /// </summary>
    public sealed class AchievementService : AbstractService
    {
        AchievementModel _model;
        AchievementLibrary _library;

        /// <summary>某条成就刚刚解锁（含成就定义）→ 通知 / 图鉴刷新。</summary>
        public MiniEvent<GameAchievement> Unlocked { get; } = new MiniEvent<GameAchievement>();

        /// <summary>全部成就定义（顺序即图鉴展示顺序）；库缺失时为空列表。</summary>
        public IReadOnlyList<GameAchievement> Achievements =>
            _library != null ? _library.Items : Array.Empty<GameAchievement>();

        public int TotalCount => Achievements.Count;

        public int UnlockedCount => _model != null ? _model.UnlockedCount : 0;

        public bool IsUnlocked(string id) => _model != null && _model.IsUnlocked(id);

        public int GetProgress(string id) => _model != null ? _model.GetProgress(id) : 0;

        public string GetUnlockTime(string id) => _model != null ? _model.GetUnlockTime(id) : string.Empty;

        protected override void OnInitialize()
        {
            _model = this.GetModel<AchievementModel>();
            _library = Resources.Load<AchievementLibrary>(AchievementLibrary.ResourcesPath);

            if (_library == null)
                Debug.LogWarning($"[成就] 未找到成就配置库 Resources/{AchievementLibrary.ResourcesPath}，成就判定暂时全部跳过");
            else
                Debug.Log($"[成就] 已加载 {_library.Items.Count} 条成就定义");
        }

        /// <summary>
        /// 上报一次事件数值：遍历所有监听该事件的成就，推进进度并在满足条件时解锁。
        /// 已解锁的成就不再判定（AC-06）。
        /// </summary>
        public void Report(string eventKey, int value)
        {
            if (_model == null || _library == null || string.IsNullOrEmpty(eventKey)) return;

            foreach (var achievement in _library.Items)
            {
                if (achievement == null || achievement.EventKey != eventKey) continue;
                if (_model.IsUnlocked(achievement.Id)) continue;

                _model.SetProgress(achievement.Id, value);
                if (!achievement.IsSatisfied(value)) continue;

                // Unlock 已防同存档重复解锁（AC-06）；MarkNotified 再把「弹提示」收敛为
                // 每存档一次（notified 随存档持久化），两条都通过才抛 Unlocked。
                if (!_model.Unlock(achievement.Id)) continue;
                if (_model.MarkNotified(achievement.Id)) Unlocked.Invoke(achievement);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using Game.Save;
using Runestone.AesirArchitecture;
using UnityEngine;

namespace Game.Model
{
    /// <summary>
    /// 成就解锁状态的唯一真源（随存档持久化）：每条成就一条 <see cref="AchievementRecordData" />，
    /// 记是否解锁、解锁时间与计数型进度。
    /// <para>
    /// 判定规则不在这里——本 Model 只管状态读写（<see cref="Unlock" /> / <see cref="SetProgress" />），
    /// 「上报值是否满足条件」由 <see cref="Game.Service.AchievementService" /> 拿配置表比对。
    /// </para>
    /// <para>
    /// 变更通知用 <see cref="MiniEvent" />：进度更新即使没解锁也要落盘，ObservableValue 的「同值不触发」不适用。
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class AchievementModel : AbstractModel, ISavableModel<AchievementData>
    {
        readonly Dictionary<string, AchievementRecordData> _records = new Dictionary<string, AchievementRecordData>();

        /// <summary>任意成就状态变化（解锁 / 进度推进 / 读档回填）→ SaveService 落盘。</summary>
        public MiniEvent Changed { get; } = new MiniEvent();

        public bool IsUnlocked(string id) => TryGet(id, out var record) && record.unlocked;

        /// <summary>计数型成就的当前进度；事件型恒为 0 或阈值。</summary>
        public int GetProgress(string id) => TryGet(id, out var record) ? record.progress : 0;

        /// <summary>解锁时间（ISO 8601 UTC）；未解锁为空串。</summary>
        public string GetUnlockTime(string id) => TryGet(id, out var record) ? record.unlockTimeUtc : string.Empty;

        /// <summary>已解锁条数（图鉴完成度用）。</summary>
        public int UnlockedCount
        {
            get
            {
                var count = 0;
                foreach (var record in _records.Values)
                    if (record.unlocked) count += 1;

                return count;
            }
        }

        /// <summary>
        /// 推进进度（**只增不减**：计数回退不冲掉已有进度）。返回本次是否真的变化。
        /// </summary>
        public bool SetProgress(string id, int value)
        {
            if (string.IsNullOrEmpty(id)) return false;

            var record = GetOrCreate(id);
            if (value <= record.progress) return false;

            record.progress = value;
            Changed.Invoke();
            return true;
        }

        /// <summary>
        /// 解锁一条成就：已解锁的直接返回 false（04 篇 AC-06 防重复解锁），
        /// 解锁成功则写时间并把进度顶到阈值以上，然后通知落盘。
        /// </summary>
        public bool Unlock(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;

            var record = GetOrCreate(id);
            if (record.unlocked) return false;

            record.unlocked = true;
            record.unlockTimeUtc = DateTime.UtcNow.ToString("o");
            Changed.Invoke();
            return true;
        }

        /// <summary>
        /// 标记「解锁通知已发」（随存档持久化）：返回标记前是否尚未通知。
        /// 服务层用它把「弹提示」收敛为每存档一次——解锁状态只增不回退，
        /// 通知标志同样只增，重开 / 重进 / 读档都不会再次触发。
        /// </summary>
        public bool MarkNotified(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;

            var record = GetOrCreate(id);
            if (record.notified) return false;

            record.notified = true;
            Changed.Invoke();
            return true;
        }

        public void WriteTo(AchievementData data)
        {
            data.records = new List<AchievementRecordData>(_records.Count);
            foreach (var record in _records.Values)
                data.records.Add(new AchievementRecordData
                {
                    id = record.id,
                    unlocked = record.unlocked,
                    unlockTimeUtc = record.unlockTimeUtc,
                    progress = record.progress,
                    notified = record.notified,
                });
        }

        public void ReadFrom(AchievementData data)
        {
            _records.Clear();
            if (data.records == null) return;

            foreach (var record in data.records)
            {
                if (record == null || string.IsNullOrEmpty(record.id)) continue;

                _records[record.id] = new AchievementRecordData
                {
                    id = record.id,
                    unlocked = record.unlocked,
                    unlockTimeUtc = record.unlockTimeUtc,
                    progress = record.progress,
                    notified = record.notified,
                };
            }

            Changed.Invoke();
        }

        bool TryGet(string id, out AchievementRecordData record)
        {
            record = null;
            return !string.IsNullOrEmpty(id) && _records.TryGetValue(id, out record);
        }

        AchievementRecordData GetOrCreate(string id)
        {
            if (_records.TryGetValue(id, out var existing)) return existing;

            var created = new AchievementRecordData { id = id };
            _records[id] = created;
            return created;
        }
    }
}

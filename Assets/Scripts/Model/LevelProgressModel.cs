using System;
using System.Collections.Generic;
using Game.Save;
using Runestone.AesirArchitecture;
using UnityEngine;

namespace Game.Model
{
    /// <summary>
    /// 关卡通关记录（唯一真源，随存档持久化）：每关一条 <see cref="LevelRecordData" />，
    /// 记最短用时、上次用时、最少步数与通关次数。
    /// <para>
    /// 记录**只在通关（Win）时写入**，失败不写；最短用时取历史最小值，由
    /// <see cref="RecordClear" /> 比较后决定（返回值即「本次是否破纪录」）。
    /// </para>
    /// <para>
    /// 变更通知刻意用 <see cref="MiniEvent" /> 而不是 ObservableValue：ObservableValue 赋同值不触发监听，
    /// 而「通关次数 +1 但没破纪录」也必须落盘，事件必须无条件触发。
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class LevelProgressModel : AbstractModel, ISavableModel<LevelProgressData>
    {
        readonly List<LevelRecordData> _records = new List<LevelRecordData>();

        /// <summary>任意一关的记录发生变化（通关写入 / 读档回填）后触发 → SaveService 落盘。</summary>
        public MiniEvent Changed { get; } = new MiniEvent();

        public IReadOnlyList<LevelRecordData> Records => _records;

        /// <summary>取该关记录；从未通关时返回一条空记录（bestTime 为负），不写回列表。</summary>
        public LevelRecordData Get(int levelId)
        {
            foreach (var record in _records)
                if (record.levelId == levelId) return record;

            return new LevelRecordData(levelId);
        }

        public bool HasRecord(int levelId) => Get(levelId).HasRecord;

        /// <summary>最短用时（秒）；<see cref="LevelRecordData.NoTime" /> 表示未通关。</summary>
        public float GetBestTime(int levelId) => Get(levelId).bestTime;

        /// <summary>最近一次通关用时（秒）；未通关为 <see cref="LevelRecordData.NoTime" />。</summary>
        public float GetLastTime(int levelId) => Get(levelId).lastTime;

        public int GetClearCount(int levelId) => Get(levelId).clearCount;

        /// <summary>
        /// 历史最好星级（0~3）：三星制 = 通关 1 星 + 捡到金币 1 星 + 金币且时限内通关 1 星。
        /// 取历史最大值（通关过但星级缺失的旧存档按 1 星兜底）。
        /// </summary>
        public int GetBestStars(int levelId)
        {
            var record = Get(levelId);
            return record.HasRecord ? Mathf.Max(1, record.bestStars) : 0;
        }

        /// <summary>
        /// 记录一次通关：通关次数 +1、刷新上次用时，更短则刷新最短用时与最少步数，
        /// 星级取历史最大值（本局拿得少不会把之前的成绩冲掉）。
        /// </summary>
        /// <param name="stars">本局星级，由 <see cref="Game.Board.LevelStarRules" /> 评定。</param>
        /// <returns>本次是否刷新了最短用时纪录。</returns>
        public bool RecordClear(int levelId, float seconds, int steps, int stars)
        {
            var record = GetOrCreate(levelId);
            var isNewRecord = !record.HasRecord || seconds < record.bestTime;

            record.clearCount += 1;
            record.lastTime = seconds;
            if (isNewRecord) record.bestTime = seconds;
            if (record.bestSteps < 0 || steps < record.bestSteps) record.bestSteps = steps;
            if (stars > record.bestStars) record.bestStars = stars;

            Changed.Invoke();

            return isNewRecord;
        }

        public void WriteTo(LevelProgressData data)
        {
            data.records = new List<LevelRecordData>(_records.Count);
            foreach (var record in _records) data.records.Add(record.Clone());
        }

        public void ReadFrom(LevelProgressData data)
        {
            _records.Clear();
            if (data.records == null) return;

            foreach (var record in data.records)
            {
                if (record != null) _records.Add(record.Clone());
            }

            Changed.Invoke();
        }

        LevelRecordData GetOrCreate(int levelId)
        {
            foreach (var record in _records)
                if (record.levelId == levelId) return record;

            var created = new LevelRecordData(levelId);
            _records.Add(created);
            return created;
        }
    }
}

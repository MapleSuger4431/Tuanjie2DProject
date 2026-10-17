using System;
using System.Collections.Generic;
using Game.Save;
using Runestone.AesirArchitecture;
using UnityEngine;

namespace Game.Model
{
    /// <summary>
    /// 玩法存档占位 Model（后续替换为真实玩法数据）：只有一部分内容需要存档
    /// （playerName / level / score / clearedLevels），其余是运行时临时状态（isPaused / combo / sessionId），
    /// 读档后不保留。通过实现 ISavableModel&lt;PlayData&gt; 与存档流程解耦：流程只认接口，不认具体字段。
    /// </summary>
    [Serializable]
    public sealed class PlayModel : AbstractModel, ISavableModel<PlayData>
    {
        // —— 存档区 ——
        [SerializeField] ObservableValue<string> playerName = new ObservableValue<string>("Player");
        [SerializeField] ObservableValue<int> level = new ObservableValue<int>(1);
        [SerializeField] ObservableValue<int> score = new ObservableValue<int>(0);

        readonly List<int> _clearedLevels = new List<int>();

        // —— 运行时临时区（不存档）——
        bool _isPaused;
        int _combo;
        string _sessionId = string.Empty;

        public ObservableValue<string> PlayerName => playerName;
        public ObservableValue<int> Level => level;
        public ObservableValue<int> Score => score;

        public IReadOnlyList<int> ClearedLevels => _clearedLevels;

        public bool IsPaused
        {
            get => _isPaused;
            set => _isPaused = value;
        }

        public int Combo
        {
            get => _combo;
            set => _combo = value;
        }

        public string SessionId => _sessionId;

        /// <summary>开始一次会话：会话级临时状态，重开后不保留。</summary>
        public void BeginSession(string sessionId)
        {
            _sessionId = sessionId;
            _combo = 0;
            _isPaused = false;
        }

        public void AddScore(int delta)
        {
            score.Value += delta;
            _combo += 1;
        }

        public void ClearLevel(int levelId)
        {
            if (!_clearedLevels.Contains(levelId)) _clearedLevels.Add(levelId);
        }

        public void WriteTo(PlayData data)
        {
            data.playerName = playerName.Value;
            data.level = level.Value;
            data.score = score.Value;
            data.clearedLevels = new List<int>(_clearedLevels);
        }

        public void ReadFrom(PlayData data)
        {
            playerName.Value = data.playerName;
            level.Value = data.level;
            score.Value = data.score;

            _clearedLevels.Clear();
            if (data.clearedLevels != null) _clearedLevels.AddRange(data.clearedLevels);
        }
    }
}

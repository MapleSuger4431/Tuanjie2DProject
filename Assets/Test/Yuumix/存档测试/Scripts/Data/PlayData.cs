using System;
using System.Collections.Generic;

namespace SaveTest
{
    /// <summary>PlayModel 对应的存档类：只放需要落盘的字段，运行时临时状态不进来。</summary>
    [Serializable]
    public sealed class PlayData
    {
        public string playerName = "Player";
        public int level = 1;
        public int score;
        public List<int> clearedLevels = new List<int>();
    }
}

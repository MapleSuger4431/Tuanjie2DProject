using System;
using UnityEngine;
using Runestone.AesirArchitecture;

namespace GameRuntimeTest
{
    /// <summary>得分数据（Quick 档：可写 ObservableValue 直接暴露）。</summary>
    [Serializable]
    public sealed class RuntimeScoreModel : AbstractModel
    {
        [SerializeField] ObservableValue<int> score = new ObservableValue<int>(0);

        public ObservableValue<int> Score => score;
    }
}

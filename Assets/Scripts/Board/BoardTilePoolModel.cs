using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Runestone.AesirArchitecture;

namespace Game.Board
{
    /// <summary>
    /// 本关的随机表：进入关卡时从 Resources 读一次 <see cref="BoardTilePoolConfig" />，按当前场景名取出该关那一份。
    /// 必须最先注册——<see cref="PieceModel" /> 初始化抽第一块时就要向它要逐格状态。
    /// </summary>
    [Serializable]
    public sealed class BoardTilePoolModel : AbstractModel
    {
        BoardTilePoolConfig _config;
        BoardTilePoolConfig.LevelPool _pool;
        string _sceneName;

        public BoardTilePoolConfig Config => _config;

        /// <summary>当前解析到的关卡场景名。</summary>
        public string SceneName => _sceneName;

        /// <summary>配置资产是否读到了（读不到会走内置默认表）。</summary>
        public bool HasConfig => _config != null;

        /// <summary>本关是否配了随机表（没配走默认表）。</summary>
        public bool HasLevelPool => _pool != null;

        protected override void OnInitialize() => ApplyScene(SceneManager.GetActiveScene().name);

        /// <summary>按场景名重新解析（关卡切换、调试时手动指定都用这里）。</summary>
        public void ApplyScene(string sceneName)
        {
            _sceneName = sceneName;
            _config = Resources.Load<BoardTilePoolConfig>(BoardTilePoolConfig.ResourcesPath);

            if (_config == null)
            {
                Debug.LogWarning($"[BoardTilePoolModel] Resources 下找不到 {BoardTilePoolConfig.ResourcesPath}，方块状态走内置默认表");
                _pool = null;
                return;
            }

            _pool = _config.PoolFor(sceneName);
            Debug.Log($"[BoardTilePoolModel] 关卡 {sceneName} 随机表：{BoardTilePoolConfig.Describe(_pool)}｜效果：{BoardTilePoolConfig.DescribeEffects(_pool)}");
        }

        /// <summary>抽一个状态。</summary>
        public TileType RandomType() => RollTypes(1)[0];

        /// <summary>一口气抽 count 个（方块每一格独立抽，块内可混出多种状态）。</summary>
        public TileType[] RollTypes(int count) =>
            _config != null
                ? _config.RandomTypes(_pool, count)
                : BoardTilePoolConfig.DefaultTypes(count);

        /// <summary>
        /// 本关有没有配「方块可附带的效果」表。没配时调用方应走自己的发牌规则（如转向箭头的必带规则），
        /// 而不是退化成「每格都无效果」。
        /// </summary>
        public bool HasEffectPool => BoardTilePoolConfig.HasAnyEffect(_pool);

        /// <summary>本关一个方块里最多能出现几格血色深渊（<see cref="TileType.Mountain" />）；0 = 不限制。</summary>
        public int MaxAbyssPerPiece => _pool != null ? Mathf.Max(0, _pool.maxAbyssPerPiece) : 0;

        /// <summary>本关整局最多能刷出几格血色深渊；0 = 完全刷不出（只有场地预置的那几个）。</summary>
        public int MaxAbyssPerRun => _pool != null ? Mathf.Max(0, _pool.maxAbyssPerRun) : 0;

        /// <summary>
        /// 本关方块附带属性反转牌的概率；负数 = 没配，调用方应沿用 <see cref="PieceModel" /> 的默认值。
        /// </summary>
        public float InvertChance => _pool != null ? _pool.invertChance : -1f;

        /// <summary>
        /// 一口气抽 count 个效果（方块每一格独立抽）。没配效果表时返回全 null。
        /// </summary>
        public TileEffect[] RollEffects(int count) =>
            _config != null
                ? _config.RandomEffects(_pool, count)
                : new TileEffect[Mathf.Max(0, count)];

        /// <summary>本关随机表的文字描述，供日志与调试面板使用。</summary>
        public string Describe() => BoardTilePoolConfig.Describe(_pool);
    }
}

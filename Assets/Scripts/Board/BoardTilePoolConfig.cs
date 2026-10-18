using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Board
{
    /// <summary>
    /// 关卡「可随机出现的格子状态」配置。整个游戏只此一份资产，放在 Resources 下，进入关卡时按场景名取出本关那一条。
    /// 设计要点：
    /// ①只有列进列表的状态才可能被抽到——第一关不放山地，就绝不会刷出不可覆盖地面；
    /// ②每关一份 <see cref="LevelPool" />，条目带权重，可以配成「路多、火少」这类难度曲线；
    /// ③起点 <see cref="TileType.Start" /> 与终点 <see cref="TileType.Goal" /> 由棋盘固定写入，即使写进列表也不参与随机。
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Board/关卡格子随机表", fileName = "BoardTilePoolConfig")]
    public sealed class BoardTilePoolConfig : ScriptableObject
    {
        /// <summary>Resources 下的加载路径（不含扩展名）。</summary>
        public const string ResourcesPath = "Board/BoardTilePoolConfig";

        /// <summary>一条可随机状态：状态 + 抽取权重。</summary>
        [Serializable]
        public sealed class WeightedType
        {
            [Tooltip("可随机出现的格子状态")]
            public TileType type = TileType.Land;

            [Tooltip("抽取权重（相对值，越大越容易抽到）；0 或不填即不参与随机")]
            [Min(0)] public int weight = 1;
        }

        /// <summary>
        /// 一条可随机效果：效果资产 + 抽取权重。
        /// <see cref="effect" /> 留空即「无效果」兜底项——没有它，块内每一格都会被塞上效果。
        /// </summary>
        [Serializable]
        public sealed class WeightedEffect
        {
            [Tooltip("可随机附带的效果；留空 = 本格无效果")]
            public TileEffect effect;

            [Tooltip("抽取权重（相对值，越大越容易抽到）；0 或不填即不参与随机")]
            [Min(0)] public int weight = 1;
        }

        /// <summary>单个关卡的随机表。</summary>
        [Serializable]
        public sealed class LevelPool
        {
            [Tooltip("关卡场景名，与 LevelSelectModel 的 sceneName 一致（例如 Level_01）")]
            public string sceneName = "Level_01";

            [Tooltip("该关可随机出现的状态集合；为空则回落到默认表")]
            public List<WeightedType> types = new List<WeightedType>();

            [Tooltip("该关方块可随机附带的效果（叠在格子上，不改变通行性）；留空则该关不发效果")]
            public List<WeightedEffect> effects = new List<WeightedEffect>();

            [Header("血色深渊（TileType.Mountain）刷新上限")]
            [Tooltip("一个方块里最多能出现几格血色深渊（0 = 不限制，按权重自然抽）。难度曲线统一按「每块最多 1 格」设计")]
            [Min(0)] public int maxAbyssPerPiece = 1;

            [Tooltip("整局最多能刷出几个血色深渊格（0 = 完全刷不出，只有场地预置的；难度曲线：第 1/2 关 0、第 3 关 1、第 4/5 关 3）。" +
                     "刷够之后方块里的深渊格一律回落成普通路，重开本关才复位")]
            [Min(0)] public int maxAbyssPerRun = 0;

            [Tooltip("本关方块附带 1 个「属性反转牌」的概率（第 4 关才引入：0 = 不出反转牌，第 1~3 关一律 0）。" +
                     "留负数表示沿用 PieceModel.invertChance 的默认值")]
            [Range(-1f, 1f)] public float invertChance = -1f;
        }

        [SerializeField] List<LevelPool> levels = new List<LevelPool>();

        [Tooltip("列表里没配到的关卡用这份")]
        [SerializeField] LevelPool fallback = new LevelPool { sceneName = "Fallback" };

        public IReadOnlyList<LevelPool> Levels => levels;

        public LevelPool Fallback => fallback;

        /// <summary>按场景名取本关随机表；没配到（或整表权重都为 0）返回 null，调用方应回落到 <see cref="DefaultTypes" />。</summary>
        public LevelPool PoolFor(string sceneName)
        {
            if (levels != null && !string.IsNullOrEmpty(sceneName))
            {
                for (int i = 0; i < levels.Count; i++)
                {
                    var pool = levels[i];
                    if (pool != null && pool.sceneName == sceneName && IsUsable(pool)) return pool;
                }
            }

            return IsUsable(fallback) ? fallback : null;
        }

        /// <summary>表里至少有一条权重为正、且不是起点/终点的状态才算可用。</summary>
        public static bool IsUsable(LevelPool pool)
        {
            if (pool == null || pool.types == null) return false;

            for (int i = 0; i < pool.types.Count; i++)
            {
                if (WeightOf(pool.types[i]) > 0) return true;
            }

            return false;
        }

        static int WeightOf(WeightedType entry)
        {
            if (entry == null) return 0;

            // 起点与终点是棋盘固定格，不参与随机
            if (entry.type == TileType.Start || entry.type == TileType.Goal) return 0;
            return Mathf.Max(0, entry.weight);
        }

        /// <summary>按权重抽一个状态。</summary>
        public static TileType RandomType(LevelPool pool)
        {
            var list = pool != null ? pool.types : null;
            if (list == null) return TileType.Land;

            var total = 0;
            for (int i = 0; i < list.Count; i++) total += WeightOf(list[i]);
            if (total <= 0) return TileType.Land;

            var roll = UnityEngine.Random.Range(0, total);
            for (int i = 0; i < list.Count; i++)
            {
                var weight = WeightOf(list[i]);
                if (weight <= 0) continue;
                if (roll < weight) return list[i].type;
                roll -= weight;
            }

            return TileType.Land;
        }

        /// <summary>一次抽 count 个（方块的每一格独立抽，故块内可以混出多种状态）。</summary>
        public TileType[] RandomTypes(LevelPool pool, int count)
        {
            if (!IsUsable(pool)) return DefaultTypes(count);

            var result = new TileType[Mathf.Max(0, count)];
            for (int i = 0; i < result.Length; i++) result[i] = RandomType(pool);
            return result;
        }

        /// <summary>配置缺失或该关没配时的兜底表：路 / 冰 / 火 / 空，不含山地。</summary>
        public static TileType[] DefaultTypes(int count)
        {
            var pool = new LevelPool
            {
                types = new List<WeightedType>
                {
                    new WeightedType { type = TileType.Land, weight = 6 },
                    new WeightedType { type = TileType.Ice, weight = 3 },
                    new WeightedType { type = TileType.Fire, weight = 3 },
                    new WeightedType { type = TileType.Void, weight = 2 },
                }
            };

            var result = new TileType[Mathf.Max(0, count)];
            for (int i = 0; i < result.Length; i++) result[i] = RandomType(pool);
            return result;
        }

        // ---------- 效果层随机（方块附带） ----------

        /// <summary>效果条目的有效权重；条目为 null 算 0。</summary>
        static int WeightOf(WeightedEffect entry) => entry == null ? 0 : Mathf.Max(0, entry.weight);

        /// <summary>本关有没有配可随机的效果（整表权重都是 0 也算没配）。</summary>
        public static bool HasAnyEffect(LevelPool pool)
        {
            if (pool == null || pool.effects == null) return false;

            for (int i = 0; i < pool.effects.Count; i++)
            {
                if (WeightOf(pool.effects[i]) > 0) return true;
            }

            return false;
        }

        /// <summary>按权重抽一个效果；没配或整表为 0 时返回 null（即本格无效果）。</summary>
        public static TileEffect RandomEffect(LevelPool pool)
        {
            var list = pool != null ? pool.effects : null;
            if (list == null) return null;

            var total = 0;
            for (int i = 0; i < list.Count; i++) total += WeightOf(list[i]);
            if (total <= 0) return null;

            var roll = UnityEngine.Random.Range(0, total);
            for (int i = 0; i < list.Count; i++)
            {
                var weight = WeightOf(list[i]);
                if (weight <= 0) continue;
                if (roll < weight) return list[i].effect;
                roll -= weight;
            }

            return null;
        }

        /// <summary>一次抽 count 个（方块每一格独立抽）；没配效果表时返回全 null，不报错。</summary>
        public TileEffect[] RandomEffects(LevelPool pool, int count)
        {
            var result = new TileEffect[Mathf.Max(0, count)];
            if (!HasAnyEffect(pool)) return result;

            for (int i = 0; i < result.Length; i++) result[i] = RandomEffect(pool);

            return result;
        }

        /// <summary>把效果表描述成「强制转向·右×2 无×9」这样的文本，用于启动日志自检。</summary>
        public static string DescribeEffects(LevelPool pool)
        {
            if (pool == null || pool.effects == null) return "（空）";

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < pool.effects.Count; i++)
            {
                var entry = pool.effects[i];
                if (entry == null || WeightOf(entry) <= 0) continue;

                if (sb.Length > 0) sb.Append(" ");
                sb.Append(entry.effect != null ? entry.effect.DisplayName : "无").Append('×').Append(WeightOf(entry));
            }

            return sb.Length > 0 ? sb.ToString() : "（空）";
        }

        /// <summary>把一份表描述成「路×6 冰×3」这样的文本，用于启动日志自检。</summary>
        public static string Describe(LevelPool pool)
        {
            if (pool == null || pool.types == null) return "（空）";

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < pool.types.Count; i++)
            {
                var entry = pool.types[i];
                if (entry == null || WeightOf(entry) <= 0) continue;
                if (sb.Length > 0) sb.Append(" ");
                sb.Append(TileTypeInfo.DisplayName(entry.type)).Append('×').Append(entry.weight);
            }

            return sb.Length > 0 ? sb.ToString() : "（空）";
        }
    }
}

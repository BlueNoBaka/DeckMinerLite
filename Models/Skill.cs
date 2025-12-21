using System.Collections.Concurrent;
using DeckMiner.Data;

namespace DeckMiner.Models
{
    public readonly struct SkillConditionUnit(
        SkillConditionType type,
        SkillComparisonOperator op,
        int val)
    {
        public readonly SkillConditionType Type = type;
        public readonly SkillComparisonOperator Operator = op;
        public readonly int Value = val;
    }

    public readonly struct SkillEffectUnit(
        SkillEffectType type,
        int usageCount,
        int val
        )
    {
        public readonly SkillEffectType Type = type;
        public readonly int UsageCount = usageCount;
        public readonly int Value = val;
    }

    // ===============================================
    // 1. Skill 类 (对应 RHYTHM GAME 技能)
    // ===============================================
    public class Skill
    {
        // 属性定义
        public string SkillId { get; }
        public int Cost { get; }
        public readonly SkillConditionUnit[][] Condition;
        public readonly SkillEffectUnit[] Effect;

        /// <summary>
        /// Skill 构造函数：初始化卡牌的激活技能。
        /// </summary>
        /// <param name="db">技能数据库</param>
        /// <param name="seriesId">技能系列ID</param>
        /// <param name="lv">技能等级，默认为 14</param>
        public Skill(string seriesId, int lv = 14)
        {
            // 对应 self.skill_id = str(series_id * 100 + lv)
            SkillId = $"{seriesId}{lv}";

            var db = SkillDataManager.SkillDatabase;

            // 检查 SkillId 是否存在于数据库中
            if (!db.TryGetValue(SkillId, out SkillDbData skillData))
            {
                // 抛出异常，因为这是核心逻辑，数据必须存在
                throw new KeyNotFoundException($"SkillId {SkillId} not found in Skill DB.");
            }

            // 对应 self.cost: int = db[self.skill_id]["ConsumeAP"]
            Cost = skillData.ConsumeAP;

            // 对应 self.condition: list[list[str]] = [condition.split(",") for condition in ...]
            // C# 复杂列表处理和类型转换
            var rawConditionIds = skillData.RhythmGameSkillConditionIds;
            var rawEffectIds = skillData.RhythmGameSkillEffectId;
            Condition = new SkillConditionUnit[rawConditionIds.Count][];
            Effect = new SkillEffectUnit[rawEffectIds.Count];
            
            for (int i = 0; i < rawConditionIds.Count; i++)
            {
                string[] ids = rawConditionIds[i].Split(',');
                Condition[i] = new SkillConditionUnit[ids.Length];
                Effect[i] = ParseSkillEffectId(rawEffectIds[i]);
                for (int j = 0; j < ids.Length; j++)
                {
                    Condition[i][j] = ParseSkillConditionId(ids[j]);
                }
            }
        }

        private static readonly ConcurrentDictionary<string, SkillConditionUnit> SkillConditionCache = new();

        /// <summary>
        /// 解析技能条件ID。对应 Python 的 parse_condition_id。
        /// </summary>
        private static SkillConditionUnit ParseSkillConditionId(string conditionId)
        {
            // 使用 GetOrAdd 尝试获取或添加结果。
            // 如果 conditionId 在缓存中，立即返回缓存值 (线程安全)。
            // 如果不在，则执行后面的 Lambda 表达式 (工厂函数)，然后原子地添加结果。

            // (SkillConditionType Type, SkillComparisonOperator Operator, int Value) 是结果的元组类型
            return SkillConditionCache.GetOrAdd(
                conditionId,
                // 工厂函数: 只有在缓存未命中时才执行
                (key) =>
                {
                    // 原始的解析逻辑
                    if (key.Length != 7 ||
                        !int.TryParse(key.AsSpan(0, 1), out int typeValue) ||
                        !int.TryParse(key.AsSpan(1, 1), out int opValue) ||
                        !int.TryParse(key.AsSpan(2), out int valueData) ||
                        !Enum.IsDefined(typeof(SkillConditionType), typeValue) ||
                        !Enum.IsDefined(typeof(SkillComparisonOperator), opValue))
                    {
                        // 如果解析失败，缓存一个错误/默认结果，避免再次尝试解析
                        // (根据你的需求，你可能需要缓存 null 或抛出异常)
                        // 这里选择缓存一个默认值 (0, 0, 0)
                        // logger.Error("错误: 无法解析条件ID。");
                        return new SkillConditionUnit(0, 0, 0);
                    }

                    var result = new SkillConditionUnit((SkillConditionType)typeValue, (SkillComparisonOperator)opValue, valueData);

                    // GetOrAdd 会自动将 result 添加到 SkillConditionCache 中
                    return result;
                }
            );
        }

        private static readonly ConcurrentDictionary<int, SkillEffectUnit> SkillEffectCache = new();

        /// <summary>
        /// 解析技能效果ID。对应 Python 的 parse_effect_id。
        /// </summary>
        private static SkillEffectUnit ParseSkillEffectId(int effectId)
        {
            // 使用 GetOrAdd 尝试获取缓存值。如果键不存在，执行后面的 Lambda 表达式。
            return SkillEffectCache.GetOrAdd(
                effectId,
                // factory 函数: 只有在缓存未命中时才执行
                (key) =>
                {
                    // --- 原始的解析逻辑 ---
                    string idStr = key.ToString();
                    // 长度检查
                    if (idStr.Length != 9) return new SkillEffectUnit(0, 0, 0);

                    // 基本字段解析
                    if (!int.TryParse(idStr.AsSpan(0, 1), out int typeValue) ||
                        !int.TryParse(idStr.AsSpan(1, 1), out int directionValue) ||
                        !Enum.IsDefined(typeof(SkillEffectType), typeValue)) 
                        return new SkillEffectUnit(0, 0, 0);

                    SkillEffectType effectType = (SkillEffectType)typeValue;
                    int usageCount;
                    int valueData;

                    // 根据类型进行特殊处理
                    if (effectType == SkillEffectType.NextAPGainRateChange || effectType == SkillEffectType.NextVoltageGainRateChange)
                    {
                        if (!int.TryParse(idStr.AsSpan(2, 1), out usageCount) ||
                            !int.TryParse(idStr.AsSpan(3), out valueData)) 
                            return new SkillEffectUnit(0, 0, 0);
                    }
                    else
                    {
                        usageCount = 1;
                        if (!int.TryParse(idStr.AsSpan(2), out valueData))
                        return new SkillEffectUnit(0, 0, 0);
                    }
                    valueData *= (directionValue == 0) ? 1 : -1;
                    // --- 返回结果 (GetOrAdd 会自动将结果添加到缓存) ---
                    return new SkillEffectUnit(effectType, usageCount, valueData);
                }
            );
        }
    }
}
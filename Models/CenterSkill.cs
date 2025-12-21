using System.Collections.Concurrent;
using DeckMiner.Data;
using DeckMiner.Services;

namespace DeckMiner.Models
{
    public readonly struct CenterSkillConditionUnit(
        CenterSkillConditionType type,
        SkillComparisonOperator op,
        int val)
    {
        public readonly CenterSkillConditionType Type = type;
        public readonly SkillComparisonOperator Operator = op;
        public readonly int Value = val;
    }

    public readonly struct CenterSkillEffectUnit(CenterSkillEffectType type, int value)
    {
        public readonly CenterSkillEffectType Type = type;
        public readonly int Value = value;
    }

    public class CenterSkill
    {
        // 属性定义
        public string SkillId { get; set; }
        public readonly CenterSkillConditionUnit[][] Condition;
        public readonly CenterSkillEffectUnit[] Effect;

        /// <summary>
        /// CenterSkill 构造函数。
        /// </summary>
        /// <param name="db">技能数据库</param>
        /// <param name="seriesId">技能系列ID</param>
        /// <param name="lv">技能等级，默认为 14</param>
        public CenterSkill(int seriesId, int lv = 14)
        {
            // 对应 Python 的初始定义和检查
            SkillId = "0"; // 默认值

            if (seriesId == 0)
            {
                // 如果 series_id == 0，则执行 return; C# 构造函数只需执行到最后。
                Condition = [];
                Effect = [];
                return;
            }

            // 对应 self.skill_id = str(series_id * 100 + lv)
            SkillId = (seriesId * 100 + lv).ToString();

            var db = SkillDataManager.CenterSkillDatabase;

            if (db.TryGetValue(SkillId, out CenterSkillDbData skillData))
            {

                // 对应 self.condition: list[str] = db[self.skill_id]["CenterSkillConditionIds"]
                var rawConditionIds = skillData.CenterSkillConditionIds;
                var rawEffectIds = skillData.CenterSkillEffectId;
                Condition = new CenterSkillConditionUnit[rawConditionIds.Count][];
                Effect = new CenterSkillEffectUnit[rawEffectIds.Count];

                for (int i = 0; i < rawConditionIds.Count; i++)
                {
                    string[] ids = rawConditionIds[i].Split(',');
                    Condition[i] = new CenterSkillConditionUnit[ids.Length];
                    Effect[i] = ParseCenterSkillEffectId(rawEffectIds[i]);
                    for (int j = 0; j < ids.Length; j++)
                    {
                        Condition[i][j] = ParseCenterSkillConditionId(ids[j]);
                    }
                }
            }
            else
            {
                // 如果找不到，初始化空列表或抛出异常，这里初始化空列表以匹配 seriesId=0 的情况
                Condition = [];
                Effect = [];
            }
        }

        private static readonly ConcurrentDictionary<string, CenterSkillConditionUnit> CenterSkillConditionCache = new();

        /// <summary>
        /// 解析C位技能条件ID。
        /// </summary>
        private static CenterSkillConditionUnit ParseCenterSkillConditionId(string conditionId)
        {
            // 使用 GetOrAdd 尝试获取缓存值。如果键不存在，执行后面的 Lambda 表达式。
            return CenterSkillConditionCache.GetOrAdd(
                conditionId,
                // 工厂函数 (Func<string, TValue>): 只有在缓存未命中时才执行
                (key) =>
                {
                    // --- 原始的解析逻辑 ---
                    if (key.Length != 7 ||
                        !int.TryParse(key.AsSpan(0, 1), out int typeValue) ||
                        !int.TryParse(key.AsSpan(1, 1), out int opValue) ||
                        !int.TryParse(key.AsSpan(2), out int valueData) ||
                        !Enum.IsDefined(typeof(CenterSkillConditionType), typeValue) ||
                        !Enum.IsDefined(typeof(SkillComparisonOperator), opValue))
                    {
                        // 解析失败或格式不符，返回默认值 (该默认值也会被缓存)
                        return new CenterSkillConditionUnit(0, 0, 0);
                    }

                    // 构造解析结果
                    var result = new CenterSkillConditionUnit(
                        (CenterSkillConditionType)typeValue,
                        (SkillComparisonOperator)opValue,
                        valueData
                        );

                    // GetOrAdd 会自动将 result 添加到缓存中，无需手动调用 Add
                    return result;
                }
            );
        }

        private static readonly ConcurrentDictionary<int, CenterSkillEffectUnit> CenterSkillEffectCache = new();

        /// <summary>
        /// 解析技能效果ID。对应 Python 的 parse_effect_id。
        /// </summary>
        private static CenterSkillEffectUnit ParseCenterSkillEffectId(int effectId)
        {
            // 使用 GetOrAdd 尝试获取缓存值。如果键不存在，执行后面的 Lambda 表达式。
            return CenterSkillEffectCache.GetOrAdd(
                effectId,
                // 工厂函数 (Func<int, TValue>): 只有在缓存未命中时才执行
                (key) =>
                {
                    // --- 原始的解析逻辑 ---
                    string idStr = key.ToString();

                    // 长度检查
                    if (idStr.Length != 9) return new CenterSkillEffectUnit(0, 0);

                    // 基本字段解析
                    if (!int.TryParse(idStr.AsSpan(0, 1), out int typeValue) ||
                        !int.TryParse(idStr.AsSpan(1, 1), out int directionValue) ||
                        !Enum.IsDefined(typeof(CenterSkillEffectType), typeValue)) 
                        return new CenterSkillEffectUnit(0, 0);

                    if (!int.TryParse(idStr.AsSpan(2), out int valueData)) 
                        return new CenterSkillEffectUnit(0, 0);

                    valueData *= (directionValue == 0) ? 1 : -1;
                    // GetOrAdd 会自动将 result 添加到缓存中，无需手动调用 Add
                    return new CenterSkillEffectUnit((CenterSkillEffectType)typeValue, valueData);
                }
            );
        }
    }
}
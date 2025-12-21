using DeckMiner.Data;
using DeckMiner.Services;

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

    // ===============================================
    // 1. Skill 类 (对应 RHYTHM GAME 技能)
    // ===============================================
    public class Skill
    {
        // 属性定义
        public string SkillId { get; }
        public int Cost { get; }
        public SkillConditionUnit[][] Condition { get; set; } // List of List of strings
        public int[] Effect { get; set; }

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
            Condition = new SkillConditionUnit[rawConditionIds.Count][];
            
            for (int i = 0; i < rawConditionIds.Count; i++)
            {
                string[] ids = rawConditionIds[i].Split(',');
                Condition[i] = new SkillConditionUnit[ids.Length];
                for (int j = 0; j < ids.Length; j++)
                {
                    Condition[i][j] = SkillResolver.ParseSkillConditionId(ids[j]);
                }
            }
            // 对应 self.effect: list[int] = db[self.skill_id]["RhythmGameSkillEffectId"]
            Effect = skillData.RhythmGameSkillEffectId.ToArray();
        }
    }
}
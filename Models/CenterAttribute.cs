using DeckMiner.Data;
using DeckMiner.Services;

namespace DeckMiner.Models
{
    public readonly struct TargetUnit(TargetType type, int value)
    {
        public readonly TargetType Type = type;
        public readonly int Value = value;
    }

    public readonly struct CenterAttributeEffectUnit(CenterAttributeEffectType type, int value)
    {
        public readonly CenterAttributeEffectType Type = type;
        public readonly int Value = value;
    }

    public class CenterAttribute
    {
        // 属性定义
        public string SkillId { get; }
        public readonly TargetUnit[][] Target;
        public readonly CenterAttributeEffectUnit[] Effect;

        /// <summary>
        /// CenterAttribute 构造函数。
        /// </summary>
        /// <param name="db">技能数据库</param>
        /// <param name="seriesId">系列ID</param>
        public CenterAttribute(int seriesId)
        {
            if (seriesId == 0)
            {
                Target = [];
                Effect = [];
                return;
            }

            SkillId = (seriesId + 1).ToString();

            var db = SkillDataManager.CenterAttributeDatabase;

            if (db.TryGetValue(SkillId, out CenterAttributeDbData skillData))
            {
                var targetIds = skillData.TargetIds;
                var effectIds = skillData.CenterAttributeEffectId;

                if (targetIds != null)
                {
                    // 对应 [target.split(",") for target in ...]
                    Target = new TargetUnit[skillData.TargetIds.Count][];
                    Effect = new CenterAttributeEffectUnit[effectIds.Count];
                    for (int i = 0; i < skillData.TargetIds.Count; i++)
                    {
                        string[] splitIds = skillData.TargetIds[i].Split(',');
                        Target[i] = new TargetUnit[splitIds.Length];
                        Effect[i] = ParseCenterAttributeEffect(effectIds[i]);
                        for (int j = 0; j < splitIds.Length; j++)
                        {
                            Target[i][j] = ParseTargetId(splitIds[j]);
                        }
                    }
                }
            }
            else
            {
                Target = [];
                Effect = [];
            }
        }

        private static TargetUnit ParseTargetId(string targetId)
        {
            if (targetId.Length == 5 &&
                int.TryParse(targetId.AsSpan(0, 1), out int type) &&
                int.TryParse(targetId.AsSpan(1), out int val))
            {
                return new TargetUnit((TargetType)type, val);
            }
            return new TargetUnit(TargetType.All, 0); // 默认降级方案
        }

        private static CenterAttributeEffectUnit ParseCenterAttributeEffect(int effectId)
        {
            int enumBaseValue, changeDirection, valueData;

            // 假设 effectId 为 8位 (10000000-99999999) 或 9位 (100000000-999999999)
            if (effectId >= 10000000 && effectId <= 99999999) // 8位
            {
                enumBaseValue = effectId / 10000000;
                changeDirection = effectId / 1000000 % 10;
                valueData = effectId % 1000000;
            }
            else if (effectId >= 100000000 && effectId <= 999999999) // 9位
            {
                enumBaseValue = effectId / 10000000; // 此时解析出前两位
                changeDirection = effectId / 1000000 % 10;
                valueData = effectId % 1000000;
            }
            else return new CenterAttributeEffectUnit(0, 0);
            valueData *= (changeDirection == 0) ? 1 : -1;
            return new CenterAttributeEffectUnit((CenterAttributeEffectType)enumBaseValue, valueData);
        }
    }
}
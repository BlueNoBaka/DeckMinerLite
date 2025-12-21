using DeckMiner.Data;
using DeckMiner.Services;

namespace DeckMiner.Models
{
    public readonly struct TargetUnit(TargetType type, int value)
    {
        public readonly TargetType Type = type;
        public readonly int Value = value;
    }

    public class CenterAttribute
    {
        // 属性定义
        public string SkillId { get; }
        public readonly TargetUnit[][] Target; // List of List of strings
        public readonly int[] Effect;

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

            // 对应 self.skill_id = str(series_id + 1)
            SkillId = (seriesId + 1).ToString();

            var db = SkillDataManager.CenterAttributeDatabase;

            if (db.TryGetValue(SkillId, out CenterAttributeDbData skillData))
            {

                // --- 处理 Target ---
                var targetIds = skillData.TargetIds;

                if (targetIds != null)
                {
                    // 对应 [target.split(",") for target in ...]
                    Target = new TargetUnit[skillData.TargetIds.Count][];
                    for (int i = 0; i < skillData.TargetIds.Count; i++)
                    {
                        string[] splitIds = skillData.TargetIds[i].Split(',');
                        Target[i] = new TargetUnit[splitIds.Length];
                        for (int j = 0; j < splitIds.Length; j++)
                        {
                            Target[i][j] = SkillResolver.ParseTargetId(splitIds[j]);
                        }
                    }
                }
                else
                {
                    Target = [];
                }

                // --- 处理 Effect ---
                // 对应 db[self.skill_id].get("CenterAttributeEffectId", None)
                var effectIds = skillData.CenterAttributeEffectId.ToArray();

                if (effectIds != null)
                {
                    Effect = effectIds;
                }
                else
                {
                    Effect = [];
                }
            }
            else
            {
                Target = [];
                Effect = [];
            }
        }
    }
}
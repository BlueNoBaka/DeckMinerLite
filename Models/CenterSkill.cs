using System.Globalization;
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

    
    
    public class CenterSkill
    {
        // 属性定义
        public string SkillId { get; set; }
        public CenterSkillConditionUnit[][] Condition { get; set; }
        public int[] Effect { get; set; }

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
                Condition = new CenterSkillConditionUnit[rawConditionIds.Count][];
            
                for (int i = 0; i < rawConditionIds.Count; i++)
                {
                    string[] ids = rawConditionIds[i].Split(',');
                    Condition[i] = new CenterSkillConditionUnit[ids.Length];
                    for (int j = 0; j < ids.Length; j++)
                    {
                        Condition[i][j] = SkillResolver.ParseCenterSkillConditionId(ids[j]);
                    }
                }
                // 对应 self.effect: list[int] = db[self.skill_id]["CenterSkillEffectId"]
                Effect = skillData.CenterSkillEffectId.ToArray();
            }
            else
            {
                // 如果找不到，初始化空列表或抛出异常，这里初始化空列表以匹配 seriesId=0 的情况
                Condition = [];
                Effect = [];
            }
        }
    }
}
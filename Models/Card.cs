using System;
using System.Collections.Generic;
using System.Linq;
using static System.Math;
using DeckMiner.Data;
// 假设这些辅助类已经存在于此命名空间或已引用
// public class CenterAttribute { ... }
// public class CenterSkill { ... }
// public class Skill { ... }

namespace DeckMiner.Models
{
    // ----------------------------------------------------
    // C# 缓存管理 (替代 Python 的 @cardobj_cache)
    // ----------------------------------------------------
    
    // 静态缓存，存储已创建的 Card 实例（只存储不可变的基础数据）
    // key: card_id
    /// <summary>
    /// 卡牌类，实现 ICloneable 以支持深拷贝 (替代 Python 的 __copy__)
    /// </summary>
    public class Card : ICloneable
    {
        private static readonly Dictionary<string, Card> CardCache = new Dictionary<string, Card>();

        // ----------------- 属性 -----------------
        public string CardId { get; private set; }
        public string FullName { get; private set; }
        public int CharactersId { get; private set; }
        public int CardLevel { get; private set; }
        
        // 卡牌状态值
        public int Smile { get; private set; }
        public int Pure { get; private set; }
        public int Cool { get; private set; }
        public int Mental { get; private set; }

        // 技能和效果 (假设这些类已定义)
        public CenterAttribute CenterAttribute { get; private set; } 
        public CenterSkill CenterSkill { get; private set; }
        public Skill SkillUnit { get; private set; }
        
        public int Cost { get; private set; }
        public int ActiveCount { get; set; } = 0;
        public bool IsExcept { get; set; } = false; // 是否被除外

        // ----------------- 构造函数和缓存 -----------------
        public Card(string seriesId, 
                    List<int> lvList = null)
        {
            // 确保 lvList 不为空，设置默认值
            lvList ??= new List<int> { 140, 14, 14 };

            // 访问全局数据库
            var dbCard = CardDataManager.CardDatabase;

            // 2. 初始化基本属性
            CardId = seriesId;
            CardLevel = lvList[0];
            
            // 数据库查找

            if (!dbCard.TryGetValue(CardId, out var cardDb))
            {
                 throw new KeyNotFoundException($"Card ID {CardId} not found in CardDbData.");
            }

            FullName = $"[{cardDb.Name}] {cardDb.Description}".Replace('\u00A0', ' '); 
            CharactersId = cardDb.CharactersId;
            
            // 3. 计算状态并获取进化等级
            int evo = _initStatus();
            
            // 4. 初始化技能对象
            string centerAttrId = cardDb.CenterAttributeSeriesId.ToString();
            string centerSkillId = cardDb.CenterSkillSeriesId.ToString();

            string skillIdPart = CardId.Length > 1 ? CardId.Substring(1) : CardId;
            string skillIdStr = $"3{skillIdPart}{evo}";
            SkillUnit = new Skill(int.Parse(skillIdStr), lvList[2]);
            Cost = SkillUnit.Cost;
        }
        // ----------------------------------------------------
    // ✅ 静态工厂方法：用于管理缓存和实例创建
    // ----------------------------------------------------
    public static Card GetInstance(
        string seriesId, 
        List<int> lvList = null)
    {
        // 1. 检查缓存
        if (CardCache.TryGetValue(seriesId, out Card cachedCard))
        {
            // 如果存在，返回它的深拷贝
            var newCard = (Card)cachedCard.Clone();
            
            // 2. 更新等级和状态 (仅针对动态参数)
            if (lvList != null)
            {
                newCard.CardLevel = lvList[0];
                // 假设 Skill 类有 SetLevel 方法
                // newCard.SkillUnit.SetLevel(lvList[2]); 
                // newCard.CenterSkill.SetLevel(lvList[1]); 
                
                // 重新计算状态 (注意：现在 _initStatus 不接受 dbCard 参数)
                newCard._initStatus(); 
            }
            
            return newCard; // ✅ 现在可以在方法中返回对象了
        }

        // 3. 如果缓存不存在，创建新实例
        // 构造函数需要与 GetInstance 接收相同的参数 (除去 dbCard)
        var newInstance = new Card(seriesId, lvList);

        // 4. 将新创建的对象加入缓存 (缓存只存储干净的实例)
        CardCache[seriesId] = (Card)newInstance.Clone(); 

        return newInstance;
    }
        // ----------------- 方法 -----------------

        /// <summary>
        /// 初始化卡牌状态值。
        /// 对应 Python 的 _init_status
        /// </summary>
        private int _initStatus()
        {
            var cardDb = CardDataManager.CardDatabase[CardId];
            
            // 1. 获取稀有度
            Rarity rarity = (Rarity)cardDb.Rarity;
            
            // 2. 获取状态百分比和进化等级
            var (statusNorm, hpNorm, evo) = CurveCalculator.GetCardStatus(rarity, CardLevel);
            
            // 3. 计算最终状态值 (MaxStatus * norm / 100)
            // Python: ceil(MaxSmile[-3] * status_norm / 100)
            // 假设 MaxSmile 等属性是 List<int>，取倒数第三个元素作为基础值
            
            // ⚠️ 模拟 Python 的 List[-3] 索引: 假设 MaxSmile 是 List<int>
            int maxSmileBase = cardDb.MaxSmile[^3]; 
            int maxPureBase = cardDb.MaxPure[^3];
            int maxCoolBase = cardDb.MaxCool[^3];
            int maxMentalBase = cardDb.MaxMental[^3];

            Smile = (int)Ceiling(maxSmileBase * statusNorm / 100.0);
            Pure = (int)Ceiling(maxPureBase * statusNorm / 100.0);
            Cool = (int)Ceiling(maxCoolBase * statusNorm / 100.0);
            Mental = (int)Ceiling(maxMentalBase * hpNorm / 100.0);
            
            return evo;
        }

        public (List<List<string>> Condition, List<int> Effect) GetSkill()
        {
            ActiveCount++;
            return (SkillUnit.Condition, SkillUnit.Effect);
        }

        public IEnumerable<(List<string> Target, int Effect)> GetCenterAttribute()
        {
            return CenterAttribute.Target.Zip(CenterAttribute.Effect, (t, e) => (t, e));
        }

        public IEnumerable<(string Condition, int Effect)> GetCenterSkill()
        {
            return CenterSkill.Condition.Zip(CenterSkill.Effect, (c, e) => (c, e));
        }

        public void CostChange(int value)
        {
            Cost = Max(0, Cost + value);
        }
        
        // ----------------- 拷贝和输出 -----------------

        // 实现 ICloneable 接口 (替代 Python 的 __copy__)
        public object Clone()
        {
            // 浅拷贝当前对象
            var newCard = (Card)MemberwiseClone(); 
            
            // 深拷贝引用类型 (关键：技能对象必须是独立的，否则修改一个卡牌的技能会影响所有卡牌)
            // 假设 CenterAttribute, CenterSkill, Skill 也实现了 ICloneable
            // newCard.CenterAttribute = (CenterAttribute)CenterAttribute.Clone();
            // newCard.CenterSkill = (CenterSkill)CenterSkill.Clone();
            // newCard.SkillUnit = (Skill)SkillUnit.Clone();
            
            // 确保可变状态被重置或正确复制
            newCard.ActiveCount = 0; // 重置计数器
            newCard.IsExcept = false;
            
            return newCard;
        }

        public override string ToString()
        {
            return $"Name: {FullName}";
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using static System.Math;
using System.Collections.Concurrent;
using DeckMiner.Data;
using DeckMiner.Config;

namespace DeckMiner.Models
{
    public enum AttributeType
    {
        None,
        Smile,
        Pure,
        Cool,
        Mental,
        Cost
    }

    // ----------------------------------------------------
    // C# 缓存管理 (替代 Python 的 @cardobj_cache)
    // ----------------------------------------------------

    // 静态缓存，存储已创建的 Card 实例（只存储不可变的基础数据）
    // key: card_id
    /// <summary>
    /// 卡牌类，实现 ICloneable 以支持拷贝
    /// </summary>
    public partial class Card : ICloneable
    {
        private static readonly ConcurrentDictionary<int, Card> CardCache = new();

        private static readonly ConcurrentDictionary<int, Card> FriendCache = new();

        // ----------------- 属性 -----------------
        public int CardId { get; private set; }
        public string FullName { get; private set; }
        public int CharactersId { get; private set; }
        public int CardLevel { get; private set; }

        // 卡牌状态值
        public int Smile { get; private set; }
        public int Pure { get; private set; }
        public int Cool { get; private set; }
        public int Mental { get; private set; }


        public CenterAttribute CenterAttribute { get; private set; }
        public CenterSkill CenterSkill { get; private set; }
        public Skill SkillUnit { get; private set; }

        public int Cost { get; private set; }
        public int ActiveCount { get; set; } = 0;
        public bool IsExcept { get; set; } = false; // 是否被除外
        public double AfkThreshold { get; set; } = -1.0;

        // ----------------- 构造函数和缓存 -----------------
        private Card() { }

        public Card(int seriesId,
                    CardLevels levels)
        {
            // 访问全局数据库
            var dbCard = CardDataManager.CardDatabase;

            // 2. 初始化基本属性
            CardId = seriesId;
            string CardIdStr = CardId.ToString();
            CardLevel = levels.CardLevel;

            // 数据库查找

            if (!dbCard.TryGetValue(CardIdStr, out var cardDb))
            {
                throw new KeyNotFoundException($"Card ID {CardIdStr} not found in CardDbData.");
            }

            FullName = $"[{cardDb.Name}] {cardDb.Description}".Replace('\u00A0', ' ');
            CharactersId = cardDb.CharactersId;

            // 3. 计算状态并获取进化等级
            int evo = InitStatus();

            // 4. 初始化技能对象
            int centerAttrId = cardDb.CenterAttributeSeriesId;
            CenterAttribute = new(centerAttrId);

            int centerSkillId = cardDb.CenterSkillSeriesId;
            CenterSkill = new(centerSkillId, levels.CenterSkillLevel);

            string skillIdPart = CardIdStr.Length > 1 ? CardIdStr.Substring(1) : CardIdStr;
            string skillIdStr = $"3{skillIdPart}{evo}";
            SkillUnit = new Skill(skillIdStr, levels.SkillLevel);
            Cost = SkillUnit.Cost;

            if (ConfigLoader.Config.DeathNote.TryGetValue(CardId, out var afkMental))
            {
                AfkThreshold = afkMental;
            }
        }

        public static void Initialize()
        {
            var config = ConfigLoader.Config;
            var allIds = TaskLoader.Task.CardPool;

            // 2. 预先创建所有卡牌实例
            foreach (var id in allIds)
            {
                // 直接从我们之前优化好的 FastLookup 数组拿等级
                var levels = config.CardCache[id];

                // 创建“干净”的实例并存入原型位
                // 这里创建新对象没关系，因为只运行一次
                CardCache[id] = new Card(id, levels);
            }
        }

        public static void InitFriends(IEnumerable<int> cardIds = null)
        {
            var defaultLevel = new CardLevels(140, 14, 14);
            var dummy = new Card();
            var config = ConfigLoader.Config;
            CardLevels levels;

            if (cardIds != null)
            {
                foreach (var id in cardIds)
                {
                    if (config.FriendCache.TryGetValue(id, out var customLevels))
                    {
                        levels = customLevels;
                    }
                    else
                    {
                        levels = defaultLevel;
                    }
                    FriendCache[id] = new Card(id, levels);
                }
            }

            dummy.CardId = 0;
            dummy.CardLevel = defaultLevel.CardLevel;
            dummy.FullName = "[Place Holder] Dummy";
            dummy.CharactersId = 0;
            dummy.Smile = 0;
            dummy.Pure = 0;
            dummy.Cool = 0;
            dummy.Mental = 0;
            dummy.Cost = 0;
            dummy.CenterAttribute = new CenterAttribute(0);
            dummy.CenterSkill = new CenterSkill(0);
            dummy.SkillUnit = new Skill();

            FriendCache[0] = dummy;
        }

        // ----------------------------------------------------
        // ✅ 静态工厂方法：用于管理缓存和实例创建
        // ----------------------------------------------------
        public static Card GetInstance(
            int seriesId)
        {
            var newCard = (Card)CardCache[seriesId].Clone();
            return newCard;
        }

        public static Card GetFriend(
            int seriesId)
        {
            var newCard = (Card)FriendCache[seriesId].Clone();
            return newCard;
        }

        // ----------------- 方法 -----------------

        /// <summary>
        /// 初始化卡牌状态值。
        /// 对应 Python 的 _init_status
        /// </summary>
        private int InitStatus()
        {
            var cardDb = CardDataManager.CardDatabase[CardId.ToString()];

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

        public (SkillConditionUnit[][] Condition, SkillEffectUnit[] Effect) GetSkill()
        {
            ActiveCount++;
            return (SkillUnit.Condition, SkillUnit.Effect);
        }

        // ----------------- 拷贝和输出 -----------------
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
            // newCard.ActiveCount = 0; // 重置计数器
            // newCard.IsExcept = false;

            return newCard;
        }

        public override string ToString()
        {
            return $"Name: {FullName}";
        }
    }

    public partial class Card
    {
        // ----------------- SkillResolver 用修改属性方法 -----------------
        public void CostChange(int value)
        {
            Cost = Max(0, Cost + value);
        }

        /// <summary>
        /// 应用指定属性的比率变化。
        /// </summary>
        public void ApplyAttributeRateChange(AttributeType attributeType, double multiplier)
        {
            switch (attributeType)
            {
                case AttributeType.Smile:
                    Smile = (int)Ceiling(Smile * multiplier);
                    break;
                case AttributeType.Pure:
                    Pure = (int)Ceiling(Pure * multiplier);
                    break;
                case AttributeType.Cool:
                    Cool = (int)Ceiling(Cool * multiplier);
                    break;
                case AttributeType.Mental:
                    Mental = (int)Ceiling(Mental * multiplier);
                    break;
                default:
                    // 抛出错误或记录日志
                    throw new ArgumentException($"不支持的属性类型: {attributeType}");
            }
        }

        /// <summary>
        /// 应用指定属性的固定值变化。
        /// </summary>
        public void ApplyAttributeValueChange(AttributeType attributeType, int value)
        {
            switch (attributeType)
            {
                case AttributeType.Smile:
                    Smile += value;
                    break;
                case AttributeType.Pure:
                    Pure += value;
                    break;
                case AttributeType.Cool:
                    Cool += value;
                    break;
                case AttributeType.Mental:
                    Mental += value;
                    break;
                case AttributeType.Cost:
                    Cost = Max(0, Cost + value);
                    break;
                default:
                    // 抛出错误或记录日志
                    throw new ArgumentException($"不支持的属性类型: {attributeType}");
            }
        }
    }
}
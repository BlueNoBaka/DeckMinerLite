using System.Collections.Generic;
using System.Linq; // 用于 Linq 扩展方法
using DeckMiner.Models;

namespace DeckMiner.Config
{
    // 完整的配置结构
    public class CardConfig
    {
        // 默认等级配置 (RarityId: Level)
        // 对应 default_card_level
        public Dictionary<int, int> DefaultCardLevels { get; set; } = new()
        {
            { 3, 80 },    // R
            { 4, 100 },   // SR
            { 5, 120 },   // UR
            { 7, 140 },   // LR
            { 8, 140 },   // DR
            { 9, 120 }    // BR
        };

        // 默认技能等级
        public int DefaultSkillLevel { get; set; } = 14; 
        public int DefaultCenterSkillLevel { get; set; } = 14;

        // 自定义卡牌等级配置 (CardId: CardOverride 对象)
        // 对应 CARD_CACHE
        public Dictionary<int, List<int>> CardCache { get; set; } = new();

        // 背水血线配置 (CardId: HPThreshold)
        // 对应 DEATH_NOTE
        public Dictionary<int, double> DeathNote { get; set; } = new();

        // --- 核心方法：获取卡牌等级 ---

        /// <summary>
        /// 获取指定卡牌ID的等级配置：[卡牌等级, C位技能等级, 普通技能等级]。
        /// 如果 CardCache 中不存在，则根据稀有度使用默认值，并将结果存入 CardCache。
        /// </summary>
        /// <param name="cardId">卡牌的唯一ID。</param>
        /// <returns>包含三个等级值的 List<int>。</returns>
        public static List<int> GetCardLevels(int cardId)
        {
            var config = ConfigLoader.Config;

            // 1. 尝试从 CardCache 中获取自定义/已缓存的等级
            if (config.CardCache.TryGetValue(cardId, out List<int> levels))
            {
                if (levels.Count >= 3)
                {
                    // **直接返回 List<int>，简化了数据处理**
                    return levels; 
                }
            }

            // 2. 如果缓存中没有，则计算默认等级
            
            // 提取稀有度数字：Python 代码中是 int(str(card_id)[4])
            // 假设卡牌ID总是7位，且稀有度信息在第5位 (索引 4)
            int rarityDigit = cardId / 100 % 10;
            int defaultLevel = config.DefaultCardLevels.GetValueOrDefault(
                rarityDigit, 
                config.DefaultCardLevels.Values.Max()
            );

            // 3. 将默认等级存入缓存 (CARD_CACHE)
            config.CardCache.Add(cardId, levels);

            return new List<int>
            {
                defaultLevel,
                config.DefaultCenterSkillLevel,
                config.DefaultSkillLevel
            };
        }

        /// <summary>
        /// 将卡牌ID列表转换为模拟器所需的格式：List<(int CardId, List<int> Levels)>
        /// </summary>
        public static List<CardDeckInfo> ConvertDeckToSimulatorFormat(
            List<int> deckCardIdsList)
        {
            // 使用 Linq 简洁地实现转换
            return deckCardIdsList
                .Select(cardId => 
                {
                    // 调用上面实现的静态配置方法获取等级
                    List<int> levels = GetCardLevels(cardId);
                    return new CardDeckInfo(cardId.ToString(), levels);
                })
                .ToList();
        }
    }
}
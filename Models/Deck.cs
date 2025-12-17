using System;
using System.Collections.Generic;
using System.Linq;
using static System.Math;

namespace DeckMiner.Models
{
    public readonly struct CardDeckInfo
    {
        public readonly int CardId;
        public readonly List<int> Levels;
        public CardDeckInfo(int id, List<int> levels)
        {
            CardId = id;
            Levels = levels;
        }
    }

    /// <summary>
    /// 卡组类，管理卡牌队列和总属性。
    /// 对应 Python 的 Deck
    /// </summary>
    public class Deck
    {
        public List<Card> Cards { get; private set; } = new List<Card>();
        public List<Card> Queue { get; private set; } = new List<Card>();
        public int Appeal { get; private set; } = 0;
        public List<string> CardLog { get; private set; } = new List<string>();

        // 构造函数
        public Deck(List<CardDeckInfo> cardInfo) // [Card ID, [LV, CSkillLV, SkillLV]]
        {
            foreach (var cardData in cardInfo)
            {
                int cardId = cardData.CardId;
                List<int> lvList = cardData.Levels;
                
                // 使用 Card 构造函数 (会自动处理缓存和拷贝)
                // ⚠️ 注意：如果 Card 构造函数是静态工厂方法，则调用方式不同
                Cards.Add(Card.GetInstance(cardId, lvList));
            }
            Reset();
        }

        // ----------------- 队列管理 -----------------

        /// <summary>
        /// 重置技能队列 (将所有非除外卡牌加入队列)。
        /// 对应 Python 的 reset
        /// </summary>
        public void Reset()
        {
            Queue.Clear();
            Queue.AddRange(Cards.Where(card => !card.IsExcept));
            
            // 卡组全部除外时的特殊处理
            if (!Queue.Any())
            {
                // 确保队列至少有一个占位符或特殊逻辑
                Queue.Add(null); 
            }
        }

        /// <summary>
        /// 获取队列顶部的卡牌，但不移除。
        /// 对应 Python 的 topcard
        /// </summary>
        public Card TopCard()
        {
            if (!Queue.Any())
            {
                Reset();
            }
            // 返回 Queue[0] (可能是 null, 如果卡组全被除外)
            return Queue.First();
        }

        /// <summary>
        /// 获取队列顶部的技能并移除该卡牌。
        /// 对应 Python 的 topskill
        /// </summary>
        public (List<List<string>> Condition, List<int> Effect) TopSkill()
        {
            if (!Queue.Any())
            {
                Reset();
            }
            
            var topCard = Queue.First();

            if (topCard == null)
            {
                // 队列中只有 null (卡组全被除外)
                // 抛出异常或返回默认值
                throw new InvalidOperationException("技能队列为空且卡组中所有卡牌均被除外。");
            }

            CardLog.Add(topCard.FullName);
            Queue.RemoveAt(0);
            
            return topCard.GetSkill();
        }
        
        // ----------------- 属性计算 -----------------

        /// <summary>
        /// 计算卡组总 Appeal 值。
        /// 对应 Python 的 appeal_calc
        /// </summary>
        public int AppealCalc(int musicType) // musicType: 1(Smile), 2(Pure), 3(Cool)
        {
            double result = 0.0;
            foreach (var card in Cards)
            {
                var appeals = new List<int> { card.Smile, card.Pure, card.Cool };
                
                if (musicType >= 1 && musicType <= 3)
                {
                    appeals[musicType - 1] *= 10;
                }
                
                result += appeals.Sum();
            }
            
            // Python: ceil(result / 10)
            int finalAppeal = (int)Ceiling(result / 10.0);
            Appeal = finalAppeal;
            return finalAppeal;
        }

        /// <summary>
        /// 计算卡组总 Mental 值。
        /// 对应 Python 的 mental_calc
        /// </summary>
        public int MentalCalc()
        {
            int result = 0;
            foreach (var card in Cards)
            {
                result += card.Mental;
            }
            return result;
        }

        /// <summary>
        /// 获取已使用的技能次数。
        /// 对应 Python 的 used_all_skill_calc
        /// </summary>
        public int UsedAllSkillCalc()
        {
            return CardLog.Count;
        }
    }
}
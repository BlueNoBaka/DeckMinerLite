using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Linq;
using static System.Math;
using DeckMiner.Config;

namespace DeckMiner.Models
{
    /// <summary>
    /// 卡组类，管理卡牌队列和总属性。
    /// 对应 Python 的 Deck
    /// </summary>
    public class Deck
    {
        public Card[] Cards = new Card[6];
        public List<Card> Queue { get; private set; } = new List<Card>(6);
        public int Appeal { get; private set; } = 0;
        public List<string> CardLog { get; private set; } = new List<string>();
        public Card TopCard;

        // 构造函数
        public Deck(IEnumerable<int> cardIds)
        {
            int i = 0;
            foreach (var cid in cardIds)
            {
                if (i >= 6) break;
                Cards[i] = Card.GetInstance(cid);
                i++;
            }
            Reset();
        }

        // ----------------- 队列管理 -----------------

        /// <summary>
        /// 重置技能队列 (将所有非除外卡牌加入队列)。
        /// 对应 Python 的 reset
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            Queue.Clear();
            Queue.AddRange(Cards.Where(card => !card.IsExcept));
            
            // 卡组全部除外时的特殊处理
            if (Queue.Count == 0)
            {
                // 确保队列至少有一个占位符或特殊逻辑
                Queue.Add(null); 
            }
            TopCard = Queue[0];
        }

        public void ExceptCard(Card card)
        {
            if (card == null) return;
            card.IsExcept = true;
            var index = Queue.IndexOf(card);
            if (index != -1)
            {
                Queue.RemoveAt(index);
                if (Queue.Count == 0)
                    Reset(); 
            }
        }

        /// <summary>
        /// 获取队列顶部的技能并移除该卡牌。
        /// 对应 Python 的 topskill
        /// </summary>
        public Skill TopSkill()
        {
            if (TopCard == null)
            {
                // 队列中只有 null (卡组全被除外)
                // 抛出异常或返回默认值
                throw new InvalidOperationException("技能队列为空且卡组中所有卡牌均被除外。");
            }

            CardLog.Add(TopCard.FullName);
            TopCard.ActiveCount++;
            var result = TopCard.SkillUnit;
            Queue.RemoveAt(0);
            if (Queue.Count == 0)
                Reset();
            TopCard = Queue[0];

            return result;
        }
        
        // ----------------- 属性计算 -----------------

        /// <summary>
        /// 计算卡组总 Appeal 值。
        /// 对应 Python 的 appeal_calc
        /// </summary>
        public int AppealCalc(int musicType) // musicType: 1(Smile), 2(Pure), 3(Cool)
        {
            int result = 0;
            switch (musicType)
            {
                case 1: // Smile
                    foreach (var card in Cards)
                        result += (card.Smile * 10) + card.Pure + card.Cool;
                    break;
                case 2: // Pure
                    foreach (var card in Cards)
                        result += card.Smile + (card.Pure * 10) + card.Cool;
                    break;
                case 3: // Cool
                    foreach (var card in Cards)
                        result += card.Smile + card.Pure + (card.Cool * 10);
                    break;
                default:
                    foreach (var card in Cards)
                        result += card.Smile + card.Pure + card.Cool;
                    break;
            }

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
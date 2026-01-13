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
        public readonly Card[] Cards = new Card[7];
        private int _currentIndex = -1;
        public int Appeal { get; private set; } = 0;
        public List<string> CardLog { get; private set; } = new List<string>();
        public Card TopCard { get; private set; }

        // 构造函数
        public Deck(IEnumerable<int> cardIds, int friendId = 0)
        {
            int i = 0;
            foreach (var cid in cardIds)
            {
                if (i >= 6) break;
                Cards[i] = Card.GetInstance(cid);
                i++;
            }
            Cards[6] = Card.GetFriend(friendId);
            Reset();
        }

        public Deck() { }

        public void UpdateCards(int[] cardIds, int friendId = 0)
        {
            for (int i = 0; i < 6; i++)
            {
                Cards[i] = Card.GetInstance(cardIds[i]);
            }
            Cards[6] = Card.GetFriend(friendId);
            // 更新后必须执行重置逻辑
            CardLog.Clear();
            Reset();
        }

        // ----------------- 队列管理 -----------------

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            // 将指针拨回到起点前一位，利用 MoveToNext 的逻辑寻找第一个有效卡牌
            _currentIndex = -1;
            MoveNext();
        }

        public void ExceptCard(Card card)
        {
            if (card == null) return;
            card.IsExcept = true;

            // 如果被除外的恰好是当前的 TopCard，立刻跳到下一个有效的
            if (card == TopCard)
            {
                MoveNext();
            }
        }

        /// <summary>
        /// 获取队列顶部卡牌的技能并移除该卡牌。
        /// </summary>
        public Skill TopSkill()
        {
            Card current = TopCard;
            if (current == null) throw new InvalidOperationException("...");

            // 1. 逻辑处理
            CardLog.Add(current.FullName);
            current.ActiveCount++;
            Skill result = current.SkillUnit;

            // 2. 索引偏移逻辑（替代 RemoveAt(0) 和 Reset）
            MoveNext();

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void MoveNext()
        {
            int start = _currentIndex;
            const int len = 6;

            while (true)
            {
                // 循环移动索引
                _currentIndex = (_currentIndex + 1) % len;

                // 如果回到了起点，说明转了一圈
                if (_currentIndex == start)
                {
                    // 检查起点这张卡是否也被除外了
                    if (Cards[_currentIndex].IsExcept)
                    {
                        TopCard = null; // 全部被除外
                        return;
                    }
                    break;
                }

                // 如果当前卡牌没被除外，这就是我们要的 TopCard
                if (!Cards[_currentIndex].IsExcept)
                    break;
            }

            TopCard = Cards[_currentIndex];
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
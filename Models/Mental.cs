using System;
using static System.Math;

namespace DeckMiner.Models
{
    public class Mental
    {
        // 属性
        public int CurrentHp { get; private set; }
        public int MaxHp { get; private set; }
        
        private int _badMinus;
        private int _missMinus;
        private int _traceMinus;

        public Mental()
        {
            CurrentHp = 100;
            MaxHp = 100;
            // 初始值 (Python 原始值)
            _badMinus = 30;
            _missMinus = 50;
            _traceMinus = 20;
        }

        public void SetHp(int hp)
        {
            MaxHp = hp;
            CurrentHp = MaxHp;
            
            // 计算扣血值：int(self.max_hp * 0.0X)
            _badMinus = 30 + (int)(MaxHp * 0.03);
            _missMinus = 50 + (int)(MaxHp * 0.05);
            _traceMinus = 20 + (int)(MaxHp * 0.02);
        }

        public void Sub(string judgement, string noteType = null)
        {
            int damage = judgement switch
            {
                "BAD" => _badMinus,
                "MISS" => noteType == "Trace" || noteType == "HoldMid" ? _traceMinus : _missMinus,
                _ => 0
            };

            if (damage > 0)
            {
                CurrentHp = Max(0, CurrentHp - damage);
            }
            
            if (CurrentHp == 0)
            {
                throw new MentalDownException();
            }
        }

        public void SkillAdd(double value) // value 是百分比 (e.g., 50 表示 50%)
        {
            // Python: max(1, self.current_hp + ceil(self.max_hp * value / 100))
            int healAmount = (int)Ceiling(MaxHp * value / 100.0);
            CurrentHp = Min(MaxHp, CurrentHp + healAmount);
            // 注意：Python 的 max(1, ...) 逻辑在 CurrentHp > 0 时通常是多余的。
            // 如果技能可以复活，需要保留。这里假设至少回复 1 点，但不能超过 MaxHp。
            CurrentHp = Max(1, CurrentHp);
        }

        public double GetRate() => MaxHp == 0 ? 0.0 : CurrentHp * 100.0 / MaxHp;

        public override string ToString() => 
            $"Mental: {CurrentHp} / {MaxHp} ({GetRate():F2}%)";
    }

    // 异常类可以直接继承自 Exception
    public class MentalDownException : Exception
    {
        public MentalDownException() : base("Mental HP 降至 0。") { }
    }
}
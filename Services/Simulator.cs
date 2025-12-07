using System.Collections.Generic;
using DeckMiner.Data;
using DeckMiner.Models;

namespace DeckMiner.Services
{
    public class Simulator(ChartData chart, int musicType, int masterLv = 50)
    {
        public ChartData Chart = chart;
        public int MusicType = musicType;
        public int MasterLv = masterLv;

        public void Run(Deck d, int centerCardId)
        {
            Card CenterCard = null;
            LiveStatus Player = new(MasterLv);
            Player.SetDeck(d);

            foreach (Card c in d.Cards)
            {
                int cid = int.Parse(c.CardId);
                if (cid == centerCardId) CenterCard = c;
            }

            if (CenterCard != null)
            {
                foreach (var (target, effect) in CenterCard.GetCenterAttribute())
                {
                    SkillResolver.ApplyCenterAttribute(Player, effect, target);
                }
            }
            d.AppealCalc(MusicType);
            Player.HpCalc();
            Player.BaseScoreCalc(Chart.AllNoteSize);

            var chartEvents = Chart.Events;
            var extraEvents = new PriorityQueue<LiveEventData, double>();
            extraEvents.Enqueue(
                new LiveEventData(Player.Cooldown, "CDavailable"),
                Player.Cooldown
                );

            int i_event = 0;
            Card cardNow = d.TopCard();

            while (i_event < Chart.Events.Count)
            {
                LiveEventData currentEvent;
                double nextChartTime = chartEvents[i_event].Time;

                // 获取下一个动态 Extra 事件的时间
                double nextExtraTime = (extraEvents.Count > 0)
                    ? extraEvents.Peek().Time // Peek() 获取优先级 (Time)
                    : double.MaxValue;
                if (nextChartTime <= nextExtraTime)
                {
                    // 1. 选择 Chart Event
                    currentEvent = chartEvents[i_event];
                    i_event++;
                }
                else
                {
                    currentEvent = extraEvents.Dequeue();
                }

                switch (currentEvent.Name)
                {
                    case "Single":
                    case "Hold":
                    case "HoldMid":
                    case "Flick":
                    case "Trace":
                        Player.ComboAdd("PERFECT+");
                        if (Player.CDAvailable && cardNow != null && Player.Ap >= cardNow.Cost)
                        {
                            Player.Ap -= cardNow.Cost;
                            var (condition, effects) = d.TopSkill();
                            SkillResolver.UseCardSkill(Player, effects, condition, cardNow);
                            Player.CDAvailable = false;
                            var nextCd = currentEvent.Time + Player.Cooldown;
                            extraEvents.Enqueue(
                                new LiveEventData(nextCd, "CDavailable"),
                                nextCd
                                );
                            cardNow = d.TopCard();
                        }
                        break;

                    case "CDavailable":
                        Player.CDAvailable = true;
                        if (cardNow != null && Player.Ap >= cardNow.Cost)
                        {
                            Player.Ap -= cardNow.Cost;
                            var (condition, effects) = d.TopSkill();
                            SkillResolver.UseCardSkill(Player, effects, condition, cardNow);
                            Player.CDAvailable = false;
                            var nextCd = currentEvent.Time + Player.Cooldown;
                            extraEvents.Enqueue(
                                new LiveEventData(nextCd, "CDavailable"),
                                nextCd
                                );
                            cardNow = d.TopCard();
                        }
                        break;

                    case string e when e.StartsWith('_'):
                        break;

                    case "FeverStart":
                    case "LiveStart":
                    case "LiveEnd":
                        if (currentEvent.Name == "FeverStart")
                        {
                            Player.Voltage.SetFever(true);
                        }
                        if (CenterCard != null)
                        {
                            foreach (var (condition, effect) in CenterCard.GetCenterSkill())
                            {
                                if (SkillResolver.CheckCenterSkillCondition(Player, condition, currentEvent.Name))
                                {
                                    SkillResolver.ApplyCenterSkillEffect(Player, effect);
                                }
                            }
                        }
                        break;
                    case "FeverEnd":
                        Player.Voltage.SetFever(false);
                        break;
                    default:
                        Console.WriteLine($"未处理的事件: {currentEvent.Name}");
                        break;

                }
            }

            Console.WriteLine($"{Player}");
        }
    }

}
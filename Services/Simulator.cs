using DeckMiner.Config;
using DeckMiner.Data;
using DeckMiner.Models;

namespace DeckMiner.Services
{
    public enum LiveEventType : byte
    {
        Unknown = 0,
        // 基础音符
        Single, Hold, HoldMid, Flick, Trace,
        // 系统事件
        CDavailable,
        // 生命周期
        LiveStart, LiveEnd,
        // Fever
        FeverStart, FeverEnd,
    }

    public readonly struct RuntimeEvent
    {
        public readonly double Time;
        public readonly LiveEventType Type;

        public RuntimeEvent(double time, LiveEventType type)
        {
            Time = time;
            Type = type;
        }
    }

    public static class ChartConverter
    {
        public static RuntimeEvent[] PrepareRuntimeEvents(ChartData chart)
        {
            var runtimeEvents = new List<RuntimeEvent>(chart.Events.Count);

            foreach (var ev in chart.Events)
            {
                var type = ev.Name switch
                {
                    "Single" => LiveEventType.Single,
                    "Hold" => LiveEventType.Hold,
                    "HoldMid" => LiveEventType.HoldMid,
                    "Flick" => LiveEventType.Flick,
                    "Trace" => LiveEventType.Trace,
                    "LiveStart" => LiveEventType.LiveStart,
                    "LiveEnd" => LiveEventType.LiveEnd,
                    "FeverStart" => LiveEventType.FeverStart,
                    "FeverEnd" => LiveEventType.FeverEnd,
                    _ => LiveEventType.Unknown
                };

                runtimeEvents.Add(new RuntimeEvent(ev.Time, type));
            }

            return runtimeEvents.ToArray(); // 转为数组，遍历速度最快
        }
    }

    public class SimulatorContext
    {
        public readonly LiveStatus Player;
        public SimulatorContext(LiveStatus player)
        {
            // 这里的 new 只在线程启动时执行一次
            Player = player;
        }
    }

    public class Simulator
    {
        public ChartData Chart;
        public RuntimeEvent[] ChartEvent;
        public MusicDbData Music;
        public int MasterLv;
        public CardConfig Config;
        private static readonly double[] MissTiming = new double[Enum.GetValues<LiveEventType>().Length];

        public Simulator(string musicId, string tier, int masterLv = 50)
        {
            Chart = ChartLoader.GetChart(musicId, tier);
            ChartEvent = ChartConverter.PrepareRuntimeEvents(Chart);
            Music = DataManager.Instance.GetMusicDatabase()[musicId];
            MasterLv = masterLv;
            Config = ConfigLoader.Config;
            MissTiming[(int)LiveEventType.Single] = 0.125;
            MissTiming[(int)LiveEventType.Hold] = 0.125;
            MissTiming[(int)LiveEventType.Flick] = 0.100;
            MissTiming[(int)LiveEventType.HoldMid] = 0.070;
            MissTiming[(int)LiveEventType.Trace] = 0.070;
        }


        public long Run(LiveStatus player, int centerCardId)
        {
            Card CenterCard = null;
            var Player = player;
            var d = Player.Deck;
            Player.Reset();

            double afkMental = 0.0;
            bool centerFriend = d.Cards[6].CharactersId == centerCardId / 1000;
            // 暗坑: 未指定C位卡牌时，助战卡的C位技能也无法生效

            for (int i = 0; i < 6; i++)
            {
                Card c = d.Cards[i];
                if (c.AfkThreshold > 0)
                {
                    if (afkMental > 0)
                        afkMental = Math.Min(afkMental, c.AfkThreshold);
                    else
                        afkMental = c.AfkThreshold;
                }

                if (c.CardId == centerCardId) CenterCard = c;
            }

            if (CenterCard != null)
            {
                var centerAttr = CenterCard.CenterAttribute;
                for (int i = 0; i < centerAttr.Effect.Length; i++)
                {
                    var target = centerAttr.Target[i];
                    var effect = centerAttr.Effect[i];
                    SkillResolver.ApplyCenterAttribute(Player, effect, target);
                }
            }
            d.AppealCalc(Music.MusicType);
            Player.HpCalc();
            Player.BaseScoreCalc(Chart.AllNoteSize);

            var chartEvents = ChartEvent;
            var nextCd = Player.Cooldown;

            int i_event = 0;
            Card cardNow = d.TopCard;

            while (i_event < chartEvents.Length)
            {
                RuntimeEvent currentEvent;
                double nextChartTime = chartEvents[i_event].Time;

                // 获取下一个动态 Extra 事件的时间
                if (nextChartTime <= nextCd)
                {
                    currentEvent = chartEvents[i_event];
                    i_event++;

                    switch (currentEvent.Type)
                    {
                        case LiveEventType.Single:
                        case LiveEventType.Hold:
                        case LiveEventType.HoldMid:
                        case LiveEventType.Flick:
                        case LiveEventType.Trace:
                            if (afkMental != 0 && Player.Mental.Rate > afkMental)
                            {
                                Player.ComboAdd(NoteJudgement.Miss, currentEvent.Type);
                                if (Player.Mental.CurrentHp == 0)
                                {
                                    return Player.Score;
                                }
                            }
                            else
                            {
                                Player.ComboAdd(NoteJudgement.PerfectPlus);
                                if (Player.CDAvailable)
                                    TryUseSkill(Player, d, ref cardNow, currentEvent.Time, ref nextCd);
                            }
                            break;
                        case LiveEventType.LiveStart:
                        case LiveEventType.LiveEnd:
                        case LiveEventType.FeverStart:
                            if (currentEvent.Type == LiveEventType.FeverStart)
                            {
                                Player.Voltage.SetFever(true);
                            }
                            if (CenterCard != null)
                            {
                                var centerSkill = CenterCard.CenterSkill;
                                for (int i = 0; i < centerSkill.Effect.Length; i++)
                                {
                                    var condition = centerSkill.Condition[i];
                                    var effect = centerSkill.Effect[i];
                                    if (SkillResolver.CheckMultiCenterSkillCondition(Player, condition, currentEvent.Type))
                                    {
                                        SkillResolver.ApplyCenterSkillEffect(Player, effect);
                                    }
                                }
                            }
                            if (centerFriend)
                            {
                                var centerSkill = d.Cards[6].CenterSkill;
                                for (int i = 0; i < centerSkill.Effect.Length; i++)
                                {
                                    var condition = centerSkill.Condition[i];
                                    var effect = centerSkill.Effect[i];
                                    if (SkillResolver.CheckMultiCenterSkillCondition(Player, condition, currentEvent.Type))
                                    {
                                        SkillResolver.ApplyCenterSkillEffect(Player, effect);
                                    }
                                }
                            }

                            break;
                        case LiveEventType.FeverEnd:
                            Player.Voltage.SetFever(false);
                            break;
                        default:
                            Console.WriteLine($"未处理的事件: {currentEvent.Time}, {currentEvent.Type}");
                            break;
                    }
                }
                else
                {
                    Player.CDAvailable = true;
                    TryUseSkill(Player, d, ref cardNow, nextCd, ref nextCd);
                    if (Player.CDAvailable)
                        nextCd = 600.0;
                }
            }

            // Console.WriteLine($"{Player}");
            return Player.Score;

            static void TryUseSkill(
                LiveStatus p,
                Deck d,
                ref Card cardNow,
                double currentTime,
                ref double nextCd
                )
            {
                if (cardNow != null && p.Ap >= cardNow.Cost)
                {
                    p.Ap -= cardNow.Cost;
                    var skill = d.TopSkill();
                    SkillResolver.UseCardSkill(p, skill, cardNow);
                    p.CDAvailable = false;
                    nextCd = currentTime + p.Cooldown;
                    cardNow = d.TopCard;
                }
            }
        }

        public long RunPrecise(SimulatorContext context, int centerCardId)
        {
            Card CenterCard = null;
            bool hasHanabiGinko = false;
            var Player = context.Player;
            var d = context.Player.Deck;
            Player.Reset();

            double afkMental = 0.0;
            bool centerFriend = d.Cards[6].CharactersId == centerCardId / 1000;

            for (int i = 0; i < 6; i++)
            {
                Card c = d.Cards[i];
                if (c.AfkThreshold > 0)
                {
                    if (afkMental > 0)
                        afkMental = Math.Min(afkMental, c.AfkThreshold);
                    else
                        afkMental = c.AfkThreshold;
                }

                if (c.CardId == centerCardId) CenterCard = c;
                if (c.CardId == 1041517) hasHanabiGinko = true;
            }

            if (CenterCard != null)
            {
                var centerAttr = CenterCard.CenterAttribute;
                for (int i = 0; i < centerAttr.Effect.Length; i++)
                {
                    var target = centerAttr.Target[i];
                    var effect = centerAttr.Effect[i];
                    SkillResolver.ApplyCenterAttribute(Player, effect, target);
                }
            }
            d.AppealCalc(Music.MusicType);
            Player.HpCalc();
            Player.BaseScoreCalc(Chart.AllNoteSize);

            var chartEvents = ChartEvent;
            var extraEvents = Player.ExtraEvents;
            extraEvents.Enqueue(
                new RuntimeEvent(Player.Cooldown, LiveEventType.CDavailable),
                Player.Cooldown
            );

            int i_event = 0;
            Card cardNow = d.TopCard;

            while (i_event < chartEvents.Length)
            {
                RuntimeEvent currentEvent;
                double nextChartTime = chartEvents[i_event].Time;

                // 获取下一个动态 Extra 事件的时间
                double nextExtraTime = (extraEvents.Count > 0)
                    ? extraEvents.Peek().Time // Peek() 获取优先级 (Time)
                    : double.MaxValue;
                if (nextChartTime <= nextExtraTime)
                {
                    currentEvent = chartEvents[i_event];
                    i_event++;

                    switch (currentEvent.Type)
                    {
                        case LiveEventType.Single:
                        case LiveEventType.Hold:
                        case LiveEventType.HoldMid:
                        case LiveEventType.Flick:
                        case LiveEventType.Trace:
                            if (afkMental != 0 && Player.Mental.Rate > afkMental)
                            {
                                if (hasHanabiGinko)
                                {
                                    var delayedTime = currentEvent.Time + MissTiming[(int)currentEvent.Type];
                                    extraEvents.Enqueue(
                                        new RuntimeEvent(delayedTime, currentEvent.Type),
                                        delayedTime
                                    );
                                }
                                else
                                {
                                    Player.ComboAdd(NoteJudgement.Miss, currentEvent.Type);
                                    if (Player.Mental.CurrentHp == 0)
                                    {
                                        return Player.Score;
                                    }
                                }
                            }
                            else
                            {
                                Player.ComboAdd(NoteJudgement.PerfectPlus);
                                if (Player.CDAvailable)
                                    TryUseSkill(Player, d, ref cardNow, currentEvent.Time, extraEvents);
                            }
                            break;
                        case LiveEventType.LiveStart:
                        case LiveEventType.LiveEnd:
                        case LiveEventType.FeverStart:
                            if (currentEvent.Type == LiveEventType.FeverStart)
                            {
                                Player.Voltage.SetFever(true);
                            }
                            if (CenterCard != null)
                            {
                                var centerSkill = CenterCard.CenterSkill;
                                for (int i = 0; i < centerSkill.Effect.Length; i++)
                                {
                                    var condition = centerSkill.Condition[i];
                                    var effect = centerSkill.Effect[i];
                                    if (SkillResolver.CheckMultiCenterSkillCondition(Player, condition, currentEvent.Type))
                                    {
                                        SkillResolver.ApplyCenterSkillEffect(Player, effect);
                                    }
                                }
                            }
                            if (centerFriend)
                            {
                                var centerSkill = d.Cards[6].CenterSkill;
                                for (int i = 0; i < centerSkill.Effect.Length; i++)
                                {
                                    var condition = centerSkill.Condition[i];
                                    var effect = centerSkill.Effect[i];
                                    if (SkillResolver.CheckMultiCenterSkillCondition(Player, condition, currentEvent.Type))
                                    {
                                        SkillResolver.ApplyCenterSkillEffect(Player, effect);
                                    }
                                }
                            }
                            break;
                        case LiveEventType.FeverEnd:
                            Player.Voltage.SetFever(false);
                            break;
                        default:
                            Console.WriteLine($"未处理的事件: {currentEvent.Time}, {currentEvent.Type}");
                            break;
                    }
                }
                else
                {
                    currentEvent = extraEvents.Dequeue();
                    switch (currentEvent.Type)
                    {
                        // 处理 MISS 延迟的 Note
                        case LiveEventType.Single:
                        case LiveEventType.Hold:
                        case LiveEventType.HoldMid:
                        case LiveEventType.Flick:
                        case LiveEventType.Trace:
                            if (Player.Mental.Rate > afkMental)
                            {
                                Player.ComboAdd(NoteJudgement.Miss, currentEvent.Type);
                                if (Player.Mental.CurrentHp == 0)
                                {
                                    return Player.Score;
                                }
                            }
                            else
                            {
                                Player.ComboAdd(NoteJudgement.PerfectPlus);
                                if (Player.CDAvailable)
                                    TryUseSkill(Player, d, ref cardNow, currentEvent.Time, extraEvents);
                            }
                            break;
                        case LiveEventType.CDavailable:
                            Player.CDAvailable = true;
                            TryUseSkill(Player, d, ref cardNow, currentEvent.Time, extraEvents);
                            break;

                        default:
                            Console.WriteLine($"未处理的事件: {currentEvent.Time}, {currentEvent.Type}");
                            break;
                    }
                }
            }

            // Console.WriteLine($"{Player}");
            return Player.Score;

            static void TryUseSkill(
                LiveStatus p,
                Deck d,
                ref Card cardNow,
                double currentTime,
                PriorityQueue<RuntimeEvent, double> extraEvents
                )
            {
                if (cardNow != null && p.Ap >= cardNow.Cost)
                {
                    p.Ap -= cardNow.Cost;
                    var skill = d.TopSkill();
                    SkillResolver.UseCardSkill(p, skill, cardNow);
                    p.CDAvailable = false;
                    var nextCd = currentTime + p.Cooldown;
                    extraEvents.Enqueue(
                        new RuntimeEvent(nextCd, LiveEventType.CDavailable),
                        nextCd
                        );
                    cardNow = d.TopCard;
                }
            }
        }
    }

}
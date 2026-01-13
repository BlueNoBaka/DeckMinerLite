using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using TqdmSharp;

using DeckMiner.Config;
using DeckMiner.Data;
using DeckMiner.Models;
using DeckMiner.Services;
using System.Runtime.InteropServices; // DataManager 所在的命名空间

// 注意：如果 Card 类中的 _initStatus 方法依赖 CardDataManager.CardDatabase，
// 那么必须确保 CardDataManager 在 Deck 初始化之前被初始化。

class Program
{

    static void Main(string[] args)
    {
        Console.WriteLine("--- SukuShow Deck Miner Lite ---");

        string taskPathFromArgs = null;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--task" && i + 1 < args.Length)
            {
                taskPathFromArgs = args[i + 1];
                break;
            }
        }

        // 如果用户传了参数，就初始化，这会覆盖默认的 task.json
        if (taskPathFromArgs != null)
        {
            TaskLoader.Initialize(taskPathFromArgs);
        }

        // ------------------------------------------------------------------
        // 步骤 1: 加载数据库文件
        // ------------------------------------------------------------------
        Console.WriteLine("正在加载数据库...");
        DataManager dataManager = DataManager.Instance;

        var cardDb = dataManager.GetCardDatabase();
        var skillDb = dataManager.GetSkillDatabase();
        var centerAttrDb = dataManager.GetCenterAttributeDatabase();
        var centerSkillDb = dataManager.GetCenterSkillDatabase();
        var musicDb = dataManager.GetMusicDatabase();

        CardDataManager.Initialize(cardDb);
        SkillDataManager.Initialize(skillDb, centerAttrDb, centerSkillDb);
        Card.Initialize();


        // ------------------------------------------------------------------
        // 步骤 2: 读取模拟任务
        // ------------------------------------------------------------------
        // Simulator SimTest = new("405129", "02");
        // Deck d = new([1041513, 1033531, 1021701, 1022702, 1023702, 1031533], 1031519);
        // LiveStatus p = new(50);
        // p.SetDeck(d);
        // var c = new SimulatorContext(p);

        // SimTest.Run(c, 1031533);
        // Console.WriteLine(p);
        // return;
        var taskConfig = TaskLoader.Task;
        List<int> globalCardPool = taskConfig.CardPool;
        foreach (var task in taskConfig.Task)
        {
            string MusicId = task.MusicId;
            string Tier = task.Tier;

            Console.WriteLine($"\n--- 歌曲: {musicDb[MusicId].Title} ({Tier}) ---");
            Console.WriteLine("[卡池配置]");
            List<int> excludeCards = task.ExcludeCards;
            List<int> secondaryCenter = task.SecondaryCenter;
            List<List<int>> mustcards = [task.MustCards.All, task.MustCards.Any, task.MustSkills];

            int centerChar = musicDb[MusicId].CenterCharacterId;

            HashSet<int> cardIdsSet = new(globalCardPool);
            cardIdsSet.ExceptWith(excludeCards);
            var cardPool = cardIdsSet.ToList();
            HashSet<int> primaryCenter = new();
            HashSet<int> otherCenter = new();

            foreach (int card in cardIdsSet)
                if (card / 1000 == centerChar)
                {
                    var rarity = card / 100 % 10;
                    if (rarity == 7 || rarity == 8)
                        primaryCenter.Add(card);
                    else
                        otherCenter.Add(card);
                }

            foreach (int card in secondaryCenter)
                if (card / 1000 == centerChar && cardIdsSet.Contains(card))
                    primaryCenter.Add(card);

            HashSet<int> availableCenter;
            if (primaryCenter.Count > 0)
                availableCenter = primaryCenter;
            else
                availableCenter = otherCenter;

            if (availableCenter.Count > 0)
                Console.WriteLine($"可用C位卡牌 ({availableCenter.Count}): [{string.Join(", ", availableCenter)}]");
            else
            {
                Console.WriteLine("无可用的C位卡牌");
            }

            HashSet<int> availableFriend = new(task.Friend);
            if (availableFriend.Count > 0)
            {
                Console.WriteLine($"可用助战卡牌 ({availableFriend.Count}): [{string.Join(", ", availableFriend)}]");
                Card.InitFriends(task.Friend);
            }
            else
            {
                Console.WriteLine("无可用的助战卡牌");
                availableFriend = null;
                Card.InitFriends();
            }

            Console.WriteLine($"共计 {cardPool.Count} 张备选卡牌，正在计算卡组数量...");
            Stopwatch sw = new();
            sw.Start();
            string logPath = Path.Combine(
                    AppContext.BaseDirectory,
                    "log",
                    $"simulation_results_{MusicId}_{Tier}.json"
                );
            DeckGenerator deckgen = new DeckGenerator(cardPool, mustcards, centerChar, availableCenter, availableFriend, logPath);
            sw.Stop();
            Console.WriteLine($"  卡组数量: {deckgen.TotalDecks}");
            Console.WriteLine($"  计算用时: {sw.ElapsedTicks / (decimal)Stopwatch.Frequency:F2}s");

            if (deckgen.TotalDecks == 0) continue;

            Simulator sim2 = new(MusicId, Tier, task.MLv);

            Console.WriteLine($"[开始模拟]");
            Stopwatch sw2 = new();
            long bestScore = -1;
            int[] bestDeck = new int[6];
            int? bestCenter = 0;
            List<string> bestLog = new();
            Exception fatalError = null;
            string errorContextInfo = string.Empty;
            object lockObject = new();

            SimulationBuffer buffer = new(
                musicId: MusicId,
                tier: Tier,
                batchSize: 10000000
            );

            sw2.Start();
            Parallel.ForEach(Tqdm.Wrap(deckgen, total: deckgen.TotalDecks, printsPerSecond: 5),
            () =>
            {
                var deck = new Deck();
                var player = new LiveStatus(task.MLv);
                player.SetDeck(deck);
                return new SimulatorContext(player);
            },
            (deckTuple, state, context) =>
            {
                if (state.ShouldExitCurrentIteration || fatalError != null) return context;

                var card_id_list = deckTuple.deck;
                var center_card = deckTuple.center;
                var friend_card = deckTuple.friend;

                context.Player.Deck.UpdateCards(card_id_list, friend_card);
                long newScore = -1;
                try
                {
                    newScore = sim2.Run(context, center_card);
                }
                catch (Exception ex)
                {
                    if (Interlocked.CompareExchange(ref fatalError, ex, null) == null)
                    {
                        errorContextInfo = $"卡组: ({string.Join(", ", card_id_list)})\nC位:   {center_card}";
                        state.Stop();
                    }
                }
                buffer.AddResult(card_id_list, center_card, friend_card, newScore);

                if (newScore > bestScore)
                {
                    lock (lockObject)
                    {
                        if (newScore > bestScore)
                        {
                            bestScore = newScore;
                            bestDeck = card_id_list;
                            bestCenter = center_card;
                            bestLog = context.Player.Deck.CardLog;
                            Console.WriteLine($"NEW HI-SCORE! Score: {bestScore:N0}".PadRight(Console.BufferWidth));
                            Console.WriteLine($"  Cards: ({string.Join(", ", card_id_list)})");
                            Console.WriteLine($"  Center: {center_card}   Friend: {friend_card}");
                        }
                    }
                }
                return context;
            },
            (finalContext) => { }
            );
            sw2.Stop();
            buffer.FlushFinal();
            buffer.MergeTempFiles();
            if (fatalError != null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n========== 模拟过程中发生严重错误 ==========");
                Console.WriteLine(errorContextInfo);
                Console.WriteLine($"错误详情: {fatalError.Message}");
                Console.WriteLine($"堆栈追踪: {fatalError.StackTrace}");
                Console.ResetColor();

                Console.WriteLine("\n按 [Enter] 键退出程序...");
                Console.ReadLine();

                Environment.Exit(1);
            }
            Console.WriteLine($"\n--- 模拟结果 ---");
            Console.WriteLine($"模拟 {deckgen.TotalDecks} 个卡组用时: {sw2.ElapsedTicks / (decimal)Stopwatch.Frequency:F2}s");
            Console.WriteLine($"歌曲: {musicDb[MusicId].Title} ({Tier})");
            Console.WriteLine($"最高分: {bestScore:N0}");
            Console.WriteLine($"卡组: ({string.Join(", ", bestDeck)})");
            Console.WriteLine($"C位:   {bestCenter}");
            var bestLogStr = string.Join(
                Environment.NewLine,
                bestLog
                    .Select((s, i) => new { s, i })
                    .GroupBy(x => x.i / 3)
                    .Select(g => string.Join(" | ", g.Select(x => x.s)))
            );
            Console.WriteLine($"Log ({bestLog.Count}):\n{bestLogStr}");
        }
        Console.WriteLine($"\n已完成全部模拟任务，按 [Enter] 退出程序...");
        Console.Read();
    }
}
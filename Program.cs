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
        Console.WriteLine("--- SukuShow Deck Miner ---");

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

        // ------------------------------------------------------------------
        // 步骤 2: 读取模拟任务
        // ------------------------------------------------------------------
        var taskConfig = TaskLoader.LoadTasks("task.jsonc");
        List<int> globalCardPool = taskConfig.CardPool;
        var task = taskConfig.Task.First();
        string MusicId = task.MusicId;
        string Tier = task.Tier;

        Console.WriteLine($"--- 歌曲: {musicDb[MusicId].Title} ({Tier}) ---");
        Console.WriteLine("[卡池配置]");
        List<int> excludeCards = [];
        List<int> secondaryCenter = [1031533, 1032530, 1033528];
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

        Console.WriteLine($"共计 {cardPool.Count} 张备选卡牌，正在计算卡组数量...");
        Stopwatch sw = new();
        sw.Start();
        DeckGenerator deckgen = new DeckGenerator(cardPool, mustcards, centerChar, availableCenter);
        sw.Stop();
        Console.WriteLine($"  卡组数量: {deckgen.TotalDecks}");
        Console.WriteLine($"  计算用时: {sw.ElapsedTicks / (decimal)Stopwatch.Frequency:F2}s");

        Simulator sim2 = new(MusicId, Tier); 

        Console.WriteLine($"[开始模拟]");
        Stopwatch sw2 = new();
        long bestScore = 0;
        object lockObject = new();

        SimulationBuffer buffer = new(
            musicId: MusicId,
            tier: Tier,
            batchSize: 10000000
        );

        sw2.Start();
        Parallel.ForEach(Tqdm.Wrap(deckgen, total:deckgen.TotalDecks, printsPerSecond: 5), (deckTuple) =>
        {
            var card_id_list = deckTuple.deck;
            var center_card = deckTuple.center;

            var deckInfo = CardConfig.ConvertDeckToSimulatorFormat(card_id_list.ToList());
            Deck deckToSimulate = new Deck(deckInfo);

            var newScore = sim2.Run(deckToSimulate, (int)center_card);

            buffer.AddResult(card_id_list, center_card, newScore);

            if (newScore > bestScore)
            {
                lock (lockObject)
                {
                    if (newScore > bestScore)
                    {
                        bestScore = newScore;
                        Console.WriteLine($"NEW HI-SCORE! Score: {bestScore:N0}".PadRight(Console.BufferWidth));
                        Console.WriteLine($"  Cards: ({string.Join(", ", card_id_list)})");
                        Console.WriteLine($"  Center: {center_card}");
                    }
                }
            }
        });
        sw2.Stop();
        buffer.FlushFinal();
        buffer.MergeTempFiles();
        Console.WriteLine($"最高分: {bestScore:N0}");
        Console.WriteLine($"模拟 {deckgen.TotalDecks} 个卡组用时: {sw2.ElapsedTicks / (decimal)Stopwatch.Frequency:F2}");
        Console.WriteLine($"按 [Enter] 退出程序...");
        Console.Read();
    }
}
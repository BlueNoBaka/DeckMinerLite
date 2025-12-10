using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using TqdmSharp;

using DeckMiner.Config;
using DeckMiner.Data;
using DeckMiner.Models;
using DeckMiner.Services; // DataManager 所在的命名空间

// 注意：如果 Card 类中的 _initStatus 方法依赖 CardDataManager.CardDatabase，
// 那么必须确保 CardDataManager 在 Deck 初始化之前被初始化。

class Program
{

    static void Main(string[] args)
    {
        Console.WriteLine("--- 卡牌模拟器数据加载和卡组构建测试程序 ---");

        // ------------------------------------------------------------------
        // 步骤 1: 加载数据库文件
        // ------------------------------------------------------------------
        DataManager dataManager = DataManager.Instance;

        var cardDb = dataManager.GetCardDatabase();
        var skillDb = dataManager.GetSkillDatabase();
        var centerAttrDb = dataManager.GetCenterAttributeDatabase();
        var centerSkillDb = dataManager.GetCenterSkillDatabase();

        // ------------------------------------------------------------------
        // 步骤 2: 初始化静态数据管理器
        // ------------------------------------------------------------------
        Console.WriteLine("\n4. 初始化 CardDataManager...");
        CardDataManager.Initialize(cardDb);
        Console.WriteLine($"数据库中包含 {cardDb.Count} 张卡牌数据。");

        Console.WriteLine("   初始化 SkillDataManager...");
        SkillDataManager.Initialize(skillDb, centerAttrDb, centerSkillDb);

        // ------------------------------------------------------------------
        // 步骤 3: 构造测试卡组信息
        // ------------------------------------------------------------------
        // 格式: List<List<dynamic>> cardInfo
        // [Card ID (string), [Card LV (int), CSkill LV (int), Skill LV (int)]]

        var cardInfo = CardConfig.ConvertDeckToSimulatorFormat(
            [1041513, 1021701, 1021523, 1022701, 1043516, 1043802]
            );

        // ------------------------------------------------------------------
        // 步骤 4: 创建 Deck 实例
        // ------------------------------------------------------------------
        Console.WriteLine("\n4. 正在创建 Deck (卡组)...");
        Deck myDeck;
        try
        {
            myDeck = new Deck(cardInfo);
        }
        catch (KeyNotFoundException ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"警告：尝试创建卡组时发生 KeyNotFoundException。可能是测试卡牌ID不存在。详细: {ex.Message}");
            Console.ResetColor();
            return;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"创建 Deck 失败: {ex.Message}");
            Console.ResetColor();
            return;
        }

        const string TestMusicId = "405105";
        const string TestTier = "02";

        Simulator sim = new(TestMusicId, TestTier); 
        sim.Run(myDeck, 1043802);
        // ------------------------------------------------------------------
        // 步骤 7: LiveStatus 测试
        // ------------------------------------------------------------------
        List<int> cardpool = [
        1011501,  // 沙知
        1021701, 1021702, 1021523, 1021512,  // 梢: LR 银河 舞会
        // 1021901, 
        1021801, 1021802,  // 梢: BR PE EA
        1022701, 1022702, // 1022504, 1022521,  // 缀: LR 明月 银河
        //1022901, 
        1022801, 1022802,  // 缀: BR PE EA
        1023702,  // 1023520,  // 慈: LR 银河
        // 1023901, 1023801, 1023802,  // 慈: BR PE EA
        1031530, 1031533,  // 1031519,  // 帆: IDOME 地平 舞会
        // 1031901, 
        1031801, 1031802, 1031803,  //帆: BR(2024) PE EA
        1032518, 1032528, 1032530, 1032532, // 沙: 舞会 IDOME 地平 Flash
        // 1032901, 
        1032801, 1032802, 1032803,  // 沙: BR PE EA OE
        1033514, 1033524, 1033525,  // 乃: 舞会 IDOME COCO夏
        1033526, 1033527, 1033528,  // 乃: 喵信号 一生梦 地平
        1033530, 1033531, // 1033901, // 乃: 人偶 HHH
        1033803, 1033801, 1033802,  // 乃: BR(2024) OE PE EA
        1041513, 
        1041512, 1041516, // 1041517,  // 吟: 舞会 梦烦 水果 花火
        1041901, 1041801, 1041802,  // 吟: BR EA OE
        1042515, 1042516, 1042518, 1042519, 1042801, 1042802,  // 铃: 暧昧 太阳 羽音 Flash EA OE
        1043515, 1043516, 1043519,  // 芽: BLAST COCO夏 VS 羽音
        // 1043902, 1043801, 1043802,  // 芽: BR(2025) EA OE    
        1051506, 1051901, 
        1051503, 
        1051505,  // 1051501, 1051502,  // 泉: 片翼 天地黎明 FC DB RF
        1052506, 1052901, 
        //1052503, 1052801, // 1052504  // 塞: 片翼 BR 十六夜 OE 天地黎明
        ];
        List<int> mustcards_all = [];
        List<int> mustcards_any = [];
        List<int> mustskills_all = [];
        List<List<int>> mustcards = [mustcards_all, mustcards_any, mustskills_all];
        int centerChar = 1051;
        HashSet<int> availableCenter = new(){1051506, 1051901, 1051503, 1051505};
        Stopwatch sw = new();
        sw.Start();
        DeckGenerator deckgen = new DeckGenerator(cardpool, mustcards, centerChar, availableCenter);
        Console.WriteLine(deckgen.TotalDecks);
        sw.Stop();
        Console.WriteLine($"计算卡组数量用时: {sw.ElapsedTicks / Stopwatch.Frequency}");
        

        const string MusicId = "105103";
        const string Tier = "02";

        Simulator sim2 = new(MusicId, Tier); 

        Stopwatch sw2 = new();
        long bestScore = 0;
        object lockObject = new();

        SimulationBuffer buffer = new(
            musicId: MusicId,
            tier: Tier,
            batchSize: 1000000
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
        Console.WriteLine($"模拟 {deckgen.TotalDecks} 个卡组用时: {sw2 .ElapsedTicks / (decimal)Stopwatch.Frequency}");
        Console.WriteLine($"按 [Enter] 退出程序...");
        Console.Read();
    }
}
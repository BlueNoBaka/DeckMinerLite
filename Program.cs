using System;
using System.Collections.Generic;
using System.IO;
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

        Dictionary<string, CardDbData> cardDb;
        Dictionary<string, SkillDbData> skillDb;
        Dictionary<string, CenterAttributeDbData> centerAttrDb;
        Dictionary<string, CenterSkillDbData> centerSkillDb;

        cardDb = dataManager.GetCardDatabase();
        skillDb = dataManager.GetSkillDatabase();
        centerAttrDb = dataManager.GetCenterAttributeDatabase();
        centerSkillDb = dataManager.GetCenterSkillDatabase();

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
        LiveStatus Player = new LiveStatus();
        Player.SetDeck(myDeck);
        Player.HpCalc();
        SkillResolver.ApplySkillEffect(Player, 701025312);
        SkillResolver.ApplySkillEffect(Player, 801025312);
        Console.WriteLine($"{Player}");
        SkillResolver.ApplySkillEffect(Player, 300001170);
        Console.WriteLine($"{Player}");
    }
}
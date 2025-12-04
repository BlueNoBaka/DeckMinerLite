using System;
using System.Collections.Generic;
using System.IO;
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
        DataManager dataManager = new DataManager();

        Dictionary<string, CardDbData> cardDb;
        Dictionary<string, SkillDbData> skillDb;
        Dictionary<string, CenterAttributeDbData> centerAttrDb;
        Dictionary<string, CenterSkillDbData> centerSkillDb;
        try
        {
            Console.WriteLine("1. 正在加载 CardDbData...");
            cardDb = dataManager.GetCardDatabase();


            Console.WriteLine("2. 正在加载 Skill 数据库...");
            skillDb = dataManager.GetSkillDatabase();
            centerAttrDb = dataManager.GetCenterAttributeDatabase();
            centerSkillDb = dataManager.GetCenterSkillDatabase();
        }
        catch (FileNotFoundException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"致命错误：{ex.Message}");
            Console.ResetColor();
            return;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"数据加载失败: {ex.Message}");
            Console.ResetColor();
            return;
        }

        // ------------------------------------------------------------------
        // 步骤 2: 初始化静态数据管理器
        // ------------------------------------------------------------------
        Console.WriteLine("\n3. 初始化 CardDataManager...");
        CardDataManager.Initialize(cardDb);
        Console.WriteLine($"数据库中包含 {cardDb.Count} 张卡牌数据。");

        Console.WriteLine("   初始化 SkillDataManager...");
        SkillDataManager.Initialize(skillDb, centerAttrDb, centerSkillDb);

        // ------------------------------------------------------------------
        // 步骤 3: 构造测试卡组信息
        // ------------------------------------------------------------------
        // 格式: List<List<dynamic>> cardInfo
        // [Card ID (string), [Card LV (int), CSkill LV (int), Skill LV (int)]]

        var cardInfo = new List<List<dynamic>>
        {
            new List<dynamic> { "1041513", new List<int> { 120, 14, 14 } },
            new List<dynamic> { "1021701", new List<int> { 140, 14, 14 } },
            new List<dynamic> { "1021523", new List<int> { 120, 14, 14 } },
            new List<dynamic> { "1022701", new List<int> { 140, 14, 14 } },
            new List<dynamic> { "1043516", new List<int> { 120, 14, 14 } },
            new List<dynamic> { "1043802", new List<int> { 140, 14, 14 } },
        };

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

        // ------------------------------------------------------------------
        // 步骤 5: 输出卡牌信息
        // ------------------------------------------------------------------
        Console.WriteLine("\n5. 卡组构建成功，输出卡牌信息:");
        int index = 1;
        foreach (var card in myDeck.Cards)
        {
            Console.WriteLine($"--- 卡牌 {index++} ---");
            Console.WriteLine($" ID: {card.CardId}");
            Console.WriteLine($" 名称: {card.FullName}");
            Console.WriteLine($" 等级: Lv.{card.CardLevel}");
            Console.WriteLine($" Smile: {card.Smile} | Pure: {card.Pure} | Cool: {card.Cool}");
            Console.WriteLine($" Mental: {card.Mental}");
            Console.WriteLine($" 技能消耗: {card.Cost}");
            foreach (var skill in card.SkillUnit.Effect)
            {
                Console.WriteLine($" 技能信息: {skill.ToString()}");    
            }
            Console.WriteLine(new string('-', 20));
        }
        
        // ------------------------------------------------------------------
        // 步骤 6: 进一步测试（Appeal / Mental 计算）
        // ------------------------------------------------------------------
        int musicType = 2; // 假设 Smile 属性的歌曲
        int appeal = myDeck.AppealCalc(musicType);
        int mental = myDeck.MentalCalc();
        
        Console.WriteLine("\n--- 卡组总属性计算 ---");
        Console.WriteLine($"Appeal (歌曲属性 {musicType}): {appeal:N0}");
        Console.WriteLine($"Mental (总HP): {mental:N0}");
        Console.WriteLine("-------------------------");
    }
}
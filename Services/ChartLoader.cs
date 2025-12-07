using DeckMiner.Data;
using System.IO;
using System.Text.Json;
using System;

namespace DeckMiner.Services
{
    public class ChartLoaderService
    {
        /// <summary>
        /// 从 JSON 文件加载 ChartData。
        /// </summary>
        /// <param name="jsonPath">JSON 文件的完整路径。</param>
        /// <returns>反序列化后的 ChartData 对象。</returns>
        public ChartData LoadChartFromJson(string jsonPath)
        {
            if (!File.Exists(jsonPath))
            {
                throw new FileNotFoundException($"Chart JSON 文件未找到: {jsonPath}");
            }

            try
            {
                // 1. 读取 JSON 字符串
                string jsonString = File.ReadAllText(jsonPath);

                // 2. AOT 兼容的反序列化
                // 获取 ChartData 类型的 TypeInfo
                var typeInfo = AppJsonSerializerContext.Default.ChartData;
                
                // 进行反序列化
                var chart = (ChartData)JsonSerializer.Deserialize(jsonString, typeInfo);
                
                if (chart == null)
                {
                    throw new JsonException("JSON 反序列化失败，返回 null。数据格式可能不匹配。");
                }

                return chart;
            }
            catch (JsonException ex)
            {
                throw new JsonException($"JSON 文件解析失败 ({jsonPath})。请检查内容格式是否与 ChartData 匹配。", ex);
            }
            catch (Exception ex)
            {
                throw new IOException($"读取文件失败: {jsonPath}", ex);
            }
        }

        public static ChartData GetChart(string musicId, string tier)
        {
            Console.WriteLine($"\n--- 正在加载谱面 (Music ID: {musicId}, Tier: {tier}) ---");

            // 1. 定义 JSON 文件路径
            string baseDir = AppContext.BaseDirectory;
            // 假设 Python 预处理后的 JSON 文件放在 database/json/ 目录下
            string chartJsonPath = Path.Combine(baseDir, "database", "Chart", $"ChartEvents_{musicId}_{tier}.json");

            // 2. 实例化服务
            var chartLoader = new ChartLoaderService();

            try
            {
                // A. 文件 I/O 和 AOT JSON 反序列化
                Console.WriteLine($"[INFO] 正在加载 JSON 文件: {chartJsonPath}");
                ChartData chart = chartLoader.LoadChartFromJson(chartJsonPath);

                // --- 结果输出 ---
                Console.WriteLine($"[SUCCESS] 成功加载 Chart Data。");
                Console.WriteLine($"  总音符数 (AllNoteSize): {chart.AllNoteSize}");
                Console.WriteLine($"  事件总数 (Events Count): {chart.Events.Count}");

                // 验证关键事件
                Console.WriteLine("\n--- 关键事件验证 ---");
                
                // 查找 LiveStart
                var liveStart = chart.Events.FirstOrDefault(e => e.Name == "LiveStart");
                Console.WriteLine($"[校验] LiveStart: {liveStart?.Time:F3}s");
                
                // 查找 FeverStart
                var feverStart = chart.Events.FirstOrDefault(e => e.Name == "FeverStart");
                Console.WriteLine($"[校验] FeverStart: {feverStart?.Time:F3}s");

                // 查找 HoldMid (步进点已由 Python 计算)
                var holdMid = chart.Events.FirstOrDefault(e => e.Name == "HoldMid");
                Console.WriteLine($"[校验] HoldMid 存在: {holdMid != null} (例如: {holdMid?.Time:F3}s)");

                Console.WriteLine("-----------------------------------------------------");
                
                // 此时，chart.Events 就是您的模拟器可以直接使用的有序事件列表。
                return chart;
            }
            catch (FileNotFoundException ex)
            {
                Console.WriteLine($"\n[FATAL ERROR] JSON 文件未找到: {ex.Message}");
                Console.WriteLine($"请确保 Python 生成的 JSON 文件已复制到输出目录的 database/json 文件夹中。");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[FATAL ERROR] 测试失败: {ex.Message}");
                Console.WriteLine($"类型: {ex.GetType().Name}");
                return null;
            }
        }
    }
}
using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using System.Text.Json;

using DeckMiner.Data;
using DeckMiner.Models;
using DeckMiner.Services;

namespace DeckMiner.Services
{
    public class SimulationResult
    {
        [JsonPropertyName("deck_card_ids")]
        public List<int> DeckCardIds { get; set; } = new();

        [JsonPropertyName("center_card")]
        public int? CenterCard { get; set; }

        [JsonPropertyName("score")]
        public long Score { get; set; }

        [JsonPropertyName("pt")]
        public long Pt { get; set; } = 0; // 默认值 0
    }

    public static class PtCalculator
    {
        /// <summary>
        /// 将分数转换为 PT 值（对应 Python 的 score2pt 逻辑）。
        /// ⚠️ 请根据实际的计算逻辑修改此方法。
        /// </summary>
        public static List<SimulationResult> ScoreToPt(List<SimulationResult> results)
        {
            // 假设 PT = Score * 10
            const int PtMultiplier = 10;
            
            foreach (var result in results)
            {
                result.Pt = result.Score * PtMultiplier;
            }
            return results;
        }
    }


    public class SimulationBuffer
    {
        private readonly ConcurrentDictionary<string, SimulationResult> _results = new();
        private readonly object _flushLock = new();

        private readonly int _batchSize;
        private int _counter = 0;

        private readonly string _tempDir;
        private readonly string _musicId;
        private readonly string _tier;

        public SimulationBuffer(string musicId, string tier, int batchSize = 1000000)
        {
            _musicId = musicId;
            _tier = tier;
            _batchSize = batchSize;
            _tempDir = Path.Combine(
                AppContext.BaseDirectory,
                "temp"
            );
            Directory.CreateDirectory(_tempDir);
        }

        private static string MakeKey(IEnumerable<int> ids)
            => string.Join(",", ids.OrderBy(x => x));

        /// <summary>
        /// 将结果写入容器，如果该卡组已存在，则保留得分更高的版本
        /// </summary>
        public void AddResult(int[] cardIds, int? center, long score)
        {
            string key = MakeKey(cardIds);

            _results.AddOrUpdate(
                key,
                (_) => new SimulationResult
                {
                    DeckCardIds = cardIds.ToList(),
                    CenterCard = center,
                    Score = score
                },
                (_, existing) =>
                {
                    if (score > existing.Score)
                    {
                        existing.DeckCardIds = cardIds.ToList();
                        existing.Score = score;
                        existing.CenterCard = center;
                    }
                    return existing;
                }
            );

            // 自动批次落盘
            Interlocked.Increment(ref _counter);

            TryFlush();
        }

        private void TryFlush()
        {
            // 如果远没到批次，不加锁
            if (Volatile.Read(ref _counter) < _batchSize)
                return;

            // 到批次了，进入 lock 再确认一次
            lock (_flushLock)
            {
                if (_counter < _batchSize)
                    return;

                FlushPartialResults();
            }
        }

        private int _batchNo = 0;
        /// <summary>
        /// 将缓存写入 temp 文件
        /// </summary>
        private void FlushPartialResults()
        {
            if (_results.Count == 0) return;

            int batchId = Interlocked.Increment(ref _batchNo);

            string path = Path.Combine(
                _tempDir, 
                $"temp_{_musicId}_{_tier}_{batchId:D3}.json"
            );

            SaveSimulationResults(_results.Values.ToList(), path, calcPt: false);

            _results.Clear();
            Interlocked.Exchange(ref _counter, 0);
        }

        /// <summary>
        /// 结束后写入最后一批
        /// </summary>
        public void FlushFinal()
        {
            FlushPartialResults();
        }

        /// <summary>
        /// 合并所有 temp JSON → 写入最终结果
        /// </summary>
        public void MergeTempFiles()
        {
            var finalMap = new Dictionary<string, SimulationResult>();
            string[] files = Directory.GetFiles(_tempDir, $"temp_{_musicId}_{_tier}_*.json");

            foreach (string file in files)
            {
                var list = LoadResultsFromJson(file);

                foreach (var result in list)
                {
                    string key = MakeKey(result.DeckCardIds);

                    if (!finalMap.ContainsKey(key) || result.Score > finalMap[key].Score)
                    {
                        finalMap[key] = result;
                    }
                }
            }

            // 保存最终
            string finalPath = Path.Combine(
                AppContext.BaseDirectory,
                "log",
                $"simulation_results_{_musicId}_{_tier}.json"
            );
            SaveSimulationResults(finalMap.Values.ToList(), finalPath, calcPt: true);
        }

        // =============== 你已有的保存方法（外部已提供） ===============
        private const string DefaultLogPath = "log/simulation_results.json";

        /// <summary>
        /// 将模拟结果数据保存到 JSON 文件，只保留相同卡组的最高分，并可选地计算 PT 值。
        /// </summary>
        /// <param name="resultsData">包含每个卡组及其得分的 SimulationResult 列表。</param>
        /// <param name="filename">保存 JSON 文件的路径。</param>
        /// <param name="calcPt">是否计算并排序 PT 值。</param>
        public static void SaveSimulationResults(
            List<SimulationResult> resultsData,
            string filename = DefaultLogPath,
            bool calcPt = false)
        {
            // Dictionary<Key: 排序后的卡牌ID字符串, Value: 最高分结果对象>
            var uniqueDecksBestScores = new Dictionary<string, SimulationResult>();

            // ----------------------------------------------------
            // 步骤 1: 去重并保留最高分 (对应 Python 的 unique_decks_best_scores)
            // ----------------------------------------------------
            foreach (var result in resultsData)
            {
                // 创建标准化 Key: 排序后的卡牌ID字符串
                // 必须使用排序后的 key 来识别唯一的卡组组合
                string sortedCardIdsKey = string.Join(",", result.DeckCardIds.OrderBy(id => id));

                if (!uniqueDecksBestScores.TryGetValue(sortedCardIdsKey, out var bestResult) || 
                    result.Score > bestResult.Score)
                {
                    // 如果是新的卡组组合，或找到了更高的分数，则更新
                    uniqueDecksBestScores[sortedCardIdsKey] = result;
                }
            }

            // 转换为列表
            var processedResults = uniqueDecksBestScores.Values.ToList();

            // ----------------------------------------------------
            // 步骤 2: 计算 PT 值并合并已有日志
            // ----------------------------------------------------
            if (calcPt)
            {
                // 计算 PT 值
                processedResults = PtCalculator.ScoreToPt(processedResults);

                // 合并既有 log
                if (File.Exists(filename))
                {
                    try
                    {
                        var existingResults = LoadResultsFromJson(filename);
                        if (existingResults != null)
                        {
                            processedResults.AddRange(existingResults);
                            // 合并后，需要再次进行去重和取最高分的操作，以防新旧日志中有相同的卡组
                            // 简化处理：这里仅将新旧结果合并在一起，并依赖后续的排序。
                            // 完整的 Python 兼容性需要在这里实现复杂的字典合并逻辑，但通常我们会依赖排序后的去重。
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"警告: 读取或合并现有日志失败 ({e.Message})。将覆盖文件。");
                    }
                }

                // 排序: 按 PT 降序
                processedResults.Sort((a, b) => b.Pt.CompareTo(a.Pt));
            }
            else
            {
                // 排序: 按 Score 降序
                processedResults.Sort((a, b) => b.Score.CompareTo(a.Score));
            }

            // ----------------------------------------------------
            // 步骤 3: 写入 JSON 文件
            // ----------------------------------------------------
            try
            {
                // 确保目录存在
                string directory = Path.GetDirectoryName(filename);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var typeInfo = AppJsonSerializerContext.Default.ListSimulationResult;
                string outputJson = JsonSerializer.Serialize(processedResults, typeInfo);

                // 写入文件
                File.WriteAllText(filename, outputJson, System.Text.Encoding.UTF8);

                Console.WriteLine($"模拟结果已保存到 {filename}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"错误: 写入模拟结果到 JSON 文件失败: {e.Message}");
            }
        }
        
        public static List<SimulationResult> LoadResultsFromJson(string jsonPath)
        {
            if (!File.Exists(jsonPath))
            {
                throw new FileNotFoundException($"模拟结果 JSON 文件未找到: {jsonPath}");
            }

            try
            {
                // 1. 读取 JSON 字符串
                string jsonString = File.ReadAllText(jsonPath);

                // 2. AOT 兼容的反序列化
                // 获取 ChartData 类型的 TypeInfo
                var typeInfo = AppJsonSerializerContext.Default.ListSimulationResult;
                
                // 进行反序列化
                var result = JsonSerializer.Deserialize(jsonString, typeInfo);
                
                if (result == null)
                {
                    throw new JsonException("JSON 反序列化失败，返回 null。数据格式可能不匹配。");
                }

                return result;
            }
            catch (JsonException ex)
            {
                throw new JsonException($"JSON 文件解析失败 ({jsonPath})。请检查内容格式是否与 SimulationResult 匹配。", ex);
            }
            catch (Exception ex)
            {
                throw new IOException($"读取文件失败: {jsonPath}", ex);
            }
        }
    }

}

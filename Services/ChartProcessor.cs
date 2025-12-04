using DeckMiner.Data;
using DeckMiner.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace DeckMiner.Services
{
    // 定义 Chart 事件的简单结构
    public record ChartEvent(double Time, string EventName);
    
    // 假设 CSV 文件的行结构，用于 FeverSection 解析
    // 实际项目中应使用 CsvHelper 或其他专用结构
    public record MusicScoreSection(int KeyType, int SongTime); 

    public partial class ChartProcessorService
    {
        // 游戏浮点数误差常量 (与 Python 实现中保持一致)
        private const double NoteError = 0.00010001; 

        // ----------------------------------------------------
        // I. 核心逻辑：长条步进点计算 (对应 _GetHolds_multi_bpm)
        // ----------------------------------------------------
        
        /// <summary>
        /// 针对 Hold 音符，计算其起始时间和结束时间之间的半拍步进点。
        /// 假设 BPM 在 Hold 持续期间恒定不变（以起始时间点前的最后一次BPM为准）。
        /// </summary>
        /// <param name="startTime">Hold 音符的起始时间 (秒)。</param>
        /// <param name="endTime">Hold 音符的结束时间 (秒)。</param>
        /// <param name="bpms">从谱面 JSON 中读取的 BPM 数据列表。</param>
        /// <returns>包含所有步进点和准确结束时间的 List<double>。</returns>
        public List<double> CalculateHoldPoints(double startTime, double endTime, List<BpmData> bpms)
        {
            List<double> holds = new List<double>();
            if (startTime >= endTime) return holds;

            // 1. 查找起始时间点生效的 BPM
            // 找到 Time 小于 startTime 的最后一个 BPM 数据
            var bpmData = bpms.LastOrDefault(b => b.Time < startTime);
            double bpmNow = bpmData?.Bpm ?? bpms.FirstOrDefault()?.Bpm ?? 120.0; // 默认值 120.0
            
            if (bpmNow <= 0) return holds;

            // 2. 计算半拍时长
            double secondsPerBeat = 60.0 / bpmNow;
            double halfBeatDuration = secondsPerBeat * 0.5;
            
            double currentTime = startTime + halfBeatDuration;

            // 3. 步进计算
            while (currentTime < endTime - NoteError)
            {
                holds.Add(currentTime);
                currentTime += halfBeatDuration;
                
                // 优化：如果当前时间非常接近结束时间，则退出循环，避免重复添加
                if (Math.Abs(currentTime - endTime) < NoteError)
                    break;
            }

            // 4. 确保精确的结束时间被添加 (如果它与最后一个步进点不同)
            if (holds.Count == 0 || Math.Abs(holds.Last() - endTime) > NoteError)
            {
                holds.Add(endTime);
            }

            return holds;
        }

        // ----------------------------------------------------
        // II. FeverSection 时间计算 (对应 _loadcsv)
        // ----------------------------------------------------

        /// <summary>
        /// 根据 MusicId 从 CSV 文件中获取 FeverSection 的起始和结束时间。
        /// </summary>
        /// <param name="music">歌曲数据库实体。</param>
        /// <returns>FeverSection 的起始时间和结束时间 (秒)。</returns>
        public (double StartTime, double EndTime) GetFeverTimes(MusicDbData music)
        {
            // 实际项目路径应根据您的文件结构调整
            string csvPath = Path.Combine("Data", "csv", $"musicscore_{music.Id}.csv");
            
            // 1. 读取并解析 CSV
            List<int> sectionTimesMs;
            try
            {
                // **TODO: 将您的 CSV 读取逻辑放在这里**
                // 假设 GetSectionTimesFromCsv(csvPath) 返回一个按顺序排列的 List<int> (毫秒)
                sectionTimesMs = GetSectionTimesFromCsv(csvPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading MusicScore CSV for ID {music.Id}: {ex.Message}");
                // 失败时返回默认值
                return (0.0, (double)music.PlayTime / 1000.0);
            }

            int fever = music.FeverSectionNo;
            long playTimeMs = music.PlayTime;

            if (fever < 2) 
            {
                 // 如果 FeverSectionNo 不合理，返回默认
                 return (0.0, (double)playTimeMs / 1000.0);
            }
            
            // 2. 计算起始时间 (sectionTimesMs 是 0-indexed)
            // Python: section[fever - 2]
            double feverStartTime = (double)sectionTimesMs[fever - 2] / 1000.0;
            double feverEndTime;

            // 3. 计算结束时间
            if (fever == 5) // 假设 5 是 PlayTime 结束的特殊标记
            {
                feverEndTime = (double)playTimeMs / 1000.0;
            }
            else
            {
                // Python: section[fever - 1]
                feverEndTime = (double)sectionTimesMs[fever - 1] / 1000.0;
            }

            return (feverStartTime, feverEndTime);
        }
        
        // ----------------------------------------------------
        // III. 事件列表生成 (对应 _initevents)
        // ----------------------------------------------------
        
        /// <summary>
        /// 将音符列表和 Fever 时间转换为有序的事件列表。
        /// </summary>
        /// <param name="notes">经过合并和处理后的音符列表。</param>
        /// <param name="music">歌曲数据库实体。</param>
        /// <param name="feverTimes">FeverSection 的起止时间。</param>
        /// <returns>按时间排序的 ChartEvent 列表。</returns>
        public List<ChartEvent> GenerateEvents(List<Note> notes, MusicDbData music, (double StartTime, double EndTime) feverTimes)
        {
            var events = new List<ChartEvent>();
            
            double liveEnd = (double)music.PlayTime / 1000.0;

            // 1. 添加核心 Live 事件
            events.Add(new ChartEvent(0.0, "LiveStart"));
            events.Add(new ChartEvent(feverTimes.StartTime, "FeverStart"));

            // 2. 添加音符事件
            foreach (var note in notes)
            {
                // 主音符事件 (Single, Hold, Flick, Trace)
                events.Add(new ChartEvent(note.JustTime, note.Type.ToString()));
                
                // Hold 步进点事件
                for (int i = 0; i < note.HoldsTime.Count; i++)
                {
                    double time = note.HoldsTime[i];
                    // 最后一个时间点是 "Hold"，之前的是 "HoldMid"
                    string eventName = (i == note.HoldsTime.Count - 1) ? "Hold" : "HoldMid";
                    events.Add(new ChartEvent(time, eventName));
                }
            }

            // 3. 添加结束 Live 事件
            events.Add(new ChartEvent(feverTimes.EndTime, "FeverEnd"));
            events.Add(new ChartEvent(liveEnd, "LiveEnd"));

            // 4. 排序：按时间戳从小到大排序
            return events.OrderBy(e => e.Time).ToList();
        }
    }
    
    public partial class ChartProcessorService
    {
        // 假设 CSV 文件的列顺序：... [Index 1: key_type], [Index 2: song_time], ...
        private const int KeyTypeColumnIndex = 1;
        private const int SongTimeColumnIndex = 2;

        /// <summary>
        /// 从 MusicScore CSV 文件中读取所有 Fever Section (key_type = 20) 的时间点。
        /// </summary>
        /// <param name="csvPath">CSV 文件路径。</param>
        /// <returns>按顺序排列的 List<int>，包含所有 Section 的 SongTime (毫秒)。</returns>
        private List<int> GetSectionTimesFromCsv(string csvPath)
        {
            if (!File.Exists(csvPath))
            {
                // 注意：在实际应用中，这里应该使用更健壮的日志记录
                Console.WriteLine($"Error: Musicscore CSV file not found at '{csvPath}'");
                return new List<int>();
            }

            var sectionTimesMs = new List<int>();

            try
            {
                // 使用 File.ReadLines 以流的方式逐行读取，内存效率高
                foreach (var line in File.ReadLines(csvPath))
                {
                    // 跳过空行和标题行（假设标题行不包含数字，或者通过其他方式判断）
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("column1")) // 假设标题行以 "column1" 开头
                    {
                        continue;
                    }

                    // 1. 分割行
                    // 使用 string.Split(',') 进行分割，并移除潜在的首尾空格
                    var columns = line.Split(',').Select(c => c.Trim()).ToArray();

                    // 2. 检查列数是否足够
                    if (columns.Length <= SongTimeColumnIndex)
                    {
                        continue; // 跳过格式不正确的行
                    }

                    // 3. 解析 KeyType 和 SongTime
                    if (int.TryParse(columns[KeyTypeColumnIndex], out int keyType) &&
                        int.TryParse(columns[SongTimeColumnIndex], out int songTime))
                    {
                        // 4. 筛选：只添加 key_type == 20 的条目 (FeverSection)
                        if (keyType == 20)
                        {
                            sectionTimesMs.Add(songTime);
                        }
                    }
                    // 忽略解析失败的行
                }
            }
            catch (Exception ex)
            {
                // 捕获所有I/O或解析异常
                Console.WriteLine($"An error occurred during CSV parsing: {ex.Message}");
                // 返回目前已解析的部分或空列表
                return new List<int>();
            }

            // 原始数据应该已经是按时间排序的，但为了安全，可以再次排序
            return sectionTimesMs.OrderBy(t => t).ToList();
        }
    }
}
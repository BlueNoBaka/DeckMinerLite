namespace DeckMiner.Data
{
    // 用于 Source Generator (AOT) 反序列化
    public class RawNoteData
    {
        public int Uid { get; set; }
        // 注意：Flags 是 ulong (64位无符号整数)
        // C# 中使用 ulong 或 long 来匹配 JSON 整数
        public ulong Flags { get; set; } 
        public string just { get; set; }
        public List<string> holds { get; set; } = new List<string>();
    }

    // JSON 中 Bpms 数组的映射
    public class BpmData
    {
        public float Time { get; set; } // 时间点
        public float Bpm { get; set; }  // BPM 值
    }

    // Chart.bytes 文件的顶层结构
    public class ChartJsonData
    {
        public List<BpmData> Bpms { get; set; } = new List<BpmData>();
        public List<RawNoteData> Notes { get; set; } = new List<RawNoteData>();
    }
}
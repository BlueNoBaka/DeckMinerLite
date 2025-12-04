using DeckMiner.Models;

namespace DeckMiner.Data
{
    // 逻辑音符实体，包含解析后的位置
    public class Note
    {
        // 原始属性
        public int Uid { get; }
        public NoteType Type { get; private set; }
        
        // 解析后的属性（对应 StartPos 和 EndPos）
        // 使用 ValueTuple (int, int) 对应 Python 的 tuple[int, int]
        public (int L, int R) StartPos { get; private set; }
        public (int L, int R) EndPos { get; private set; }

        // 链表属性
        public Note PrevNote { get; set; }
        public Note NextNote { get; set; }

        // 最终计算出的时间点（从 string 转换为 float/double，方便后续计算）
        public double JustTime { get; }
        public List<double> HoldsTime { get; set; } = new List<double>();
        
        // 构造函数：接受原始数据并进行解析
        public Note(RawNoteData rawData, bool isMirror = false)
        {
            Uid = rawData.Uid;
            // 确保时间字符串转换为 double
            if (double.TryParse(rawData.just, out double justTime))
                JustTime = justTime;
            
            HoldsTime = rawData.holds
                .Where(s => double.TryParse(s, out _))
                .Select(double.Parse)
                .ToList();

            // 执行核心的位操作解析
            _ParseFlags(rawData.Flags, isMirror);
        }

        // 对应 Python 的 _parse_flags
        private void _ParseFlags(ulong flagsValue, bool isMirror)
        {
            // C# 的位操作：& (AND), | (OR), << (左移), >> (右移)
            
            // 1. 提取原始值
            int rawType = (int)((flagsValue >> 0) & 0xF); // 4 bits
            int R1_raw = (int)((flagsValue >> 4) & 0x3F);  // 6 bits
            int R2_raw = (int)((flagsValue >> 10) & 0x3F); // 6 bits
            int L1_raw = (int)((flagsValue >> 16) & 0x3F); // 6 bits
            int L2_raw = (int)((flagsValue >> 22) & 0x3F); // 6 bits

            Type = (NoteType)rawType;

            // 2. 应用镜像逻辑 (逻辑与 Python 相同)
            int R1 = R1_raw, L1 = L1_raw, R2 = R2_raw, L2 = L2_raw;
            
            if (isMirror)
            {
                const int MirrorConst = 59; 

                R1 = MirrorConst - L1_raw; 
                L1 = MirrorConst - R1_raw;

                R2 = MirrorConst - L2_raw;
                L2 = MirrorConst - R2_raw;
            }

            // 3. 设置最终位置 (L1/R1 是 StartPos，L2/R2 是 EndPos)
            StartPos = (L1, R1);
            EndPos = (L2, R2);
        }
    }
}
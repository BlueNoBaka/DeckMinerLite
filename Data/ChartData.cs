using System.Collections.Generic;

namespace DeckMiner.Data
{
    /// <summary>
    /// 单个事件对象，对应 JSON 数组中的元素。
    /// Time 属性由于 JSON 中是字符串，我们定义为 string。
    /// </summary>
    public class ChartEventData
    {
        public double Time { get; set; }
        public string Name { get; set; }
    }

    /// <summary>
    /// 最终的 Chart 对象，包含所有事件和音符总数。
    /// </summary>
    public class ChartData
    {
        public List<ChartEventData> Events { get; set; } = new List<ChartEventData>();
        public int AllNoteSize { get; set; }
    }
}
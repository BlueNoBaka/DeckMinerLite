using System.Text.Json;
using DeckMiner.Data;

namespace DeckMiner.Config
{
    public class TaskConfig
    {
        public List<int> CardPool { get; set; } = new();
        public List<SimulationTask> Task { get; set; } = new();
    }

    public class SimulationTask
    {
        public string MusicId { get; set; } = string.Empty;
        public string Tier { get; set; } = "02";
        public int MLv { get; set; } = 50;
        public RequiredCards MustCards { get; set; } = new();
        public List<int> ExcludeCards { get; set; } = new();
        public List<int> SecondaryCenter { get; set; } = new();
        public List<int> MustSkills { get; set; } = new();
    }

    public class RequiredCards
    {
        public List<int> All { get; set; } = new();
        public List<int> Any { get; set; } = new();
    }

    public static class TaskLoader
    {
        private static TaskConfig _taskInstance;
        // 默认路径，如果启动参数没给，就用这个
        private static string _currentFilePath = Path.Combine(AppContext.BaseDirectory, "task.jsonc");

        /// <summary>
        /// 直接通过 TaskLoader.Task 访问配置
        /// </summary>
        public static TaskConfig Task
        {
            get
            {
                // 如果还没加载，或者被显式清空了，执行首次加载
                if (_taskInstance == null)
                {
                    _taskInstance = InternalLoad();
                }
                return _taskInstance;
            }
        }

        /// <summary>
        /// 提供给外部（如 Main 函数解析启动参数后）显式设置路径并立即加载
        /// </summary>
        public static void Initialize(string filePath)
        {
            if (!string.IsNullOrWhiteSpace(filePath))
            {
                if (!File.Exists(_currentFilePath))
                    _currentFilePath = filePath;
                else
                    Console.WriteLine($"警告: 任务配置文件 '{_currentFilePath}' 不存在，使用默认配置文件 'task.jsonc'。");
            }
            // 显式调用时强制重新加载，确保路径生效
            _taskInstance = InternalLoad();
        }

        private static TaskConfig InternalLoad()
        {
            if (!File.Exists(_currentFilePath))
            {
                Console.WriteLine($"警告: 默认配置文件 '{_currentFilePath}' 不存在。");
                return new TaskConfig();
            }

            try
            {
                string jsonString = File.ReadAllText(_currentFilePath);
                var typeInfo = AppJsonSerializerContext.Default.TaskConfig;

                var task = JsonSerializer.Deserialize(jsonString, typeInfo);

                // 如果 TaskConfig 也有 Initialize 逻辑（如前面讨论的预解析），在此调用
                // task?.Initialize(); 

                return task ?? new TaskConfig();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"加载任务配置失败 ({_currentFilePath}): {ex.Message}");
                return new TaskConfig();
            }
        }
    }
}
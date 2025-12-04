using System.Text.Json;
using DeckMiner.Data;

namespace DeckMiner.Services
{
    // 定义您的数据库类型别名，方便代码阅读
    using SkillDbDictionaryType = Dictionary<string, SkillDbData>;
    using CenterAttrDbDictionaryType = Dictionary<string, CenterAttributeDbData>;
    using CenterSkillDbDictionaryType = Dictionary<string, CenterSkillDbData>;
    using CardDbDictionaryType = Dictionary<string, CardDbData>;
    using MusicDbDictionaryType = Dictionary<string, MusicDbData>;

    public class DataManager
    {
        private SkillDbDictionaryType _skillDb;
        private CenterAttrDbDictionaryType _centerAttrDb;
        private CenterSkillDbDictionaryType _centerSkillDb;
        private CardDbDictionaryType _cardDb;
        private MusicDbDictionaryType _musicDb;

        // ----------------------------------------------------
        // 🚀 通用加载方法 (通用化您的 Python db_load 函数)
        // ----------------------------------------------------
        /// <summary>
        /// 泛型方法，用于加载任何已被 JsonContext 标记的数据库类型。
        /// </summary>
        /// <typeparam name="TDictionary">期望返回的字典类型，例如 Dictionary<string, SkillDbData></typeparam>
        /// <param name="filePath">JSON 文件路径</param>
        private TDictionary LoadDb<TDictionary>(string filePath) where TDictionary : class
        {
            if (!File.Exists(filePath))
            {
                string currentDir = Directory.GetCurrentDirectory();
                throw new FileNotFoundException($"数据库文件不存在: {filePath}. 检查当前工作目录是否正确: {currentDir}.");
            }

            try
            {
                string jsonString = File.ReadAllText(filePath);
                
                // 1. 获取我们想要反序列化的类型
                var typeToDeserialize = typeof(TDictionary); 
                
                // 2. 从上下文获取该类型的序列化信息 (TypeInfo)，这是通用的关键
                var typeInfo = AppJsonSerializerContext.Default.GetTypeInfo(typeToDeserialize);

                if (typeInfo == null)
                {
                    throw new InvalidOperationException($"无法获取 {typeof(TDictionary).Name} 的 TypeInfo。请在 JsonContext.cs 中标记该类型。");
                }
                
                // 3. 执行反序列化，并转换为 TDictionary 类型
                var db = (TDictionary)JsonSerializer.Deserialize(jsonString, typeInfo);

                Console.WriteLine($"成功加载 {filePath} 中的 {typeof(TDictionary).Name} 数据库。");
                return db;
            }
            catch (Exception ex)
            {
                throw new Exception($"加载文件 {filePath} 失败。", ex);
            }
        }

        // ----------------------------------------------------
        // 💻 针对特定数据库的公共访问方法
        // ----------------------------------------------------
        
        public SkillDbDictionaryType GetSkillDatabase()
        {
            if (_skillDb == null)
            {
                // 只需调用一次通用方法，并传入目标类型和路径
                _skillDb = LoadDb<SkillDbDictionaryType>("database/RhythmGameSkills.json");
            }
            return _skillDb;
        }

        public CenterAttrDbDictionaryType GetCenterAttributeDatabase()
        {
            if (_centerAttrDb == null)
            {
                _centerAttrDb = LoadDb<CenterAttrDbDictionaryType>("database/CenterAttributes.json");
            }
            return _centerAttrDb;
        }

        public CenterSkillDbDictionaryType GetCenterSkillDatabase()
        {
            if (_centerSkillDb == null)
            {
                _centerSkillDb = LoadDb<CenterSkillDbDictionaryType>("database/CenterSkills.json");
            }
            return _centerSkillDb;
        }

        public CardDbDictionaryType GetCardDatabase()
        {
            if (_cardDb == null)
            {
                // 只需调用一次通用方法，并传入目标类型和路径
                _cardDb = LoadDb<CardDbDictionaryType>("database/CardDatas.json");
            }
            return _cardDb;
        }

        public MusicDbDictionaryType GetMusicDatabase()
        {
            if (_musicDb == null)
            {
                // 只需调用一次通用方法，并传入目标类型和路径
                _musicDb = LoadDb<MusicDbDictionaryType>("database/Musics.json");
            }
            return _musicDb;
        }
        // ... 未来所有新的数据库都只需添加 Get 方法和在 JsonContext 中标记类型
    }
}
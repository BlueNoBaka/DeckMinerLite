using System.Collections.Generic;
using System.Text.Json.Serialization;
using DeckMiner.Config;

namespace DeckMiner.Data
{
    // 定义您的数据库顶级类型：一个字典，键是 string (Skill ID)，值是 SkillDbData
    using SkillDbDictionaryType = Dictionary<string, SkillDbData>;
    using CenterAttrDbDictionaryType = Dictionary<string, CenterAttributeDbData>;
    using CenterSkillDbDictionaryType = Dictionary<string, CenterSkillDbData>;
    using CardDbDictionaryType = Dictionary<string, CardDbData>;
    using MusicDbDictionaryType = Dictionary<string, MusicDbData>;

    // 1. 使用 [JsonSerializable] 属性标记需要生成代码的类型
    [JsonSerializable(typeof(SkillDbDictionaryType))]
    [JsonSerializable(typeof(CenterAttrDbDictionaryType))]
    [JsonSerializable(typeof(CenterSkillDbDictionaryType))]
    [JsonSerializable(typeof(CardDbDictionaryType))]
    [JsonSerializable(typeof(MusicDbDictionaryType))]
    [JsonSerializable(typeof(ChartData))]
    [JsonSerializable(typeof(CardConfig))]

    // 2. 告诉生成器如何格式化输出
    [JsonSourceGenerationOptions(WriteIndented = true)]
    
    // 3. 继承自 JsonSerializerContext，并使用 partial 关键字
    public partial class AppJsonSerializerContext : JsonSerializerContext
    {
        // 编译器将在此处自动生成代码，包括访问 Dictionary<string, SkillDbData> 的属性。
    }
}
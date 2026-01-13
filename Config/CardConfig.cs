using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Frozen;
using DeckMiner.Models;

namespace DeckMiner.Config
{
    [JsonConverter(typeof(CardLevelsConverter))]
    public readonly struct CardLevels(int card, int center, int skill)
    {
        public readonly int CardLevel = card;
        public readonly int CenterSkillLevel = center;
        public readonly int SkillLevel = skill;
    }

    // 完整的配置结构
    public class CardConfig
    {
        // 默认等级配置 (RarityId: Level)
        // 对应 default_card_level
        public Dictionary<int, int> DefaultCardLevels { get; set; } = new()
        {
            { 3, 80 },    // R
            { 4, 100 },   // SR
            { 5, 120 },   // UR
            { 7, 140 },   // LR
            { 8, 140 },   // DR
            { 9, 120 }    // BR
        };

        // 默认技能等级
        public int DefaultSkillLevel { get; set; } = 14;
        public int DefaultCenterSkillLevel { get; set; } = 14;

        [JsonPropertyName("CardCache")]
        public Dictionary<int, CardLevels> RawCardCache { get; set; } = new();

        public Dictionary<int, CardLevels> FriendCache { get; set; } = new();

        [JsonIgnore]
        public ConcurrentDictionary<int, CardLevels> CardCache { get; private set; }

        // 背水血线配置 (CardId: HPThreshold)
        // 对应 DEATH_NOTE
        public Dictionary<int, double> DeathNote { get; set; } = new();

        public void Initialize()
        {
            var allAvailableCardIds = TaskLoader.Task.CardPool;
            var tempMap = new ConcurrentDictionary<int, CardLevels>();

            foreach (var id in allAvailableCardIds)
            {
                // 1. 优先看 JSON 里有没有手动定义的 CardCache 原始数据
                // 这里的 RawCardCache 是你从 JSON 读入的 Dictionary<int, CardLevels>
                if (RawCardCache != null && RawCardCache.TryGetValue(id, out var customLevels))
                {
                    tempMap[id] = customLevels;
                }
                else
                {
                    // 2. 没有就计算默认值
                    int rarityDigit = id / 100 % 10;
                    int defaultLvl = DefaultCardLevels.GetValueOrDefault(rarityDigit, 140);
                    tempMap[id] = new CardLevels(defaultLvl, DefaultCenterSkillLevel, DefaultSkillLevel);
                }
            }

            CardCache = tempMap;
        }
    }

    public class CardLevelsConverter : JsonConverter<CardLevels>
    {
        public override CardLevels Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
                throw new JsonException("Expected StartArray for CardLevels.");

            // 按顺序读取数组中的值
            reader.Read(); int card = reader.GetInt32();
            reader.Read(); int center = reader.GetInt32();
            reader.Read(); int skill = reader.GetInt32();

            // 读掉结束符号 ]
            while (reader.TokenType != JsonTokenType.EndArray) reader.Read();

            return new CardLevels(card, center, skill);
        }

        public override void Write(Utf8JsonWriter writer, CardLevels value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            writer.WriteNumberValue(value.CardLevel);
            writer.WriteNumberValue(value.CenterSkillLevel);
            writer.WriteNumberValue(value.SkillLevel);
            writer.WriteEndArray();
        }
    }
}
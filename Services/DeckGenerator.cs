using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Collections;
using System.Collections.Concurrent;

using DeckMiner.Data;
using DeckMiner.Models;
using DeckMiner.Services;

namespace DeckMiner.Services
{
    public class SkillData
    {
        public List<int> RhythmGameSkillEffectId { get; set; }
    }

    public static class DB
    {
        public static Dictionary<int, HashSet<object>> DB_TAG = new();
    }

    // ========== 生成 DB_TAG ==========
    public static class TagGenerator
    {
        public static void BuildDBTag()
        {
            DB.DB_TAG.Clear();
            var dataManager = DataManager.Instance;
            var cardDb = dataManager.GetCardDatabase();
            var skillDb = dataManager.GetSkillDatabase();
    
            foreach (var kv in cardDb)
            {
                var data = kv.Value;
                int skillSeries = data.RhythmGameSkillSeriesId.Last();
                string skillId = $"{skillSeries}14";

                List<int> effects = skillDb[skillId].RhythmGameSkillEffectId;

                HashSet<object> tag = new();

                foreach (int effect in effects)
                {
                    int effectType = effect / 100000000;
                    tag.Add((SkillEffectType)effectType);
                }

                tag.Add((Rarity)data.Rarity);
                DB.DB_TAG[data.CardSeriesId] = tag;
            }
        }
    }

    // ================== 计数 tag ==================
    public static class SkillTagCounter
    {
        public static Dictionary<object, int> CountSkillTags(List<int> cardIds)
        {
            List<object> allTags = new();

            foreach (var cid in cardIds)
            {
                if (DB.DB_TAG.TryGetValue(cid, out var tagset))
                {
                    allTags.AddRange(tagset);
                }
                else
                {
                    Console.WriteLine($"警告: 卡牌 {cid} 未在映射中找到。");
                }
            }

            return allTags
                .GroupBy(t => t)
                .ToDictionary(g => g.Key, g => g.Count());
        }
    }

    // ================== 生成角色分布 ==================
    public static class RoleDistribution
    {
        public static List<int[]> GenerateRoleDistributions(List<int> allCharacters)
        {
            HashSet<string> seen = new();
            List<int[]> results = new();

            for (int doubleCount = 0; doubleCount <= 3; doubleCount++)
            {
                foreach (var doubles in Combinations(allCharacters, doubleCount))
                {
                    int remaining = 6 - doubleCount * 2;
                    var remainChars = allCharacters.Except(doubles).ToList();

                    foreach (var singles in Combinations(remainChars, remaining))
                    {
                        List<int> dist = new();
                        dist.AddRange(doubles);
                        dist.AddRange(doubles); // 双卡
                        dist.AddRange(singles);

                        var sorted = dist.OrderBy(x => x).ToArray();
                        string key = string.Join(",", sorted);

                        if (seen.Add(key))
                        {
                            results.Add(sorted);
                        }
                    }
                }
            }

            return results;
        }

        // 通用组合方法
        static IEnumerable<List<T>> Combinations<T>(List<T> list, int k)
        {
            if (k == 0)
            {
                yield return new List<T>();
                yield break;
            }
            if (k > list.Count) yield break;

            for (int i = 0; i < list.Count; i++)
            {
                foreach (var tail in Combinations(list.Skip(i + 1).ToList(), k - 1))
                {
                    var result = new List<T> { list[i] };
                    result.AddRange(tail);
                    yield return result;
                }
            }
        }
    }

    // ============= 加载模拟过的卡组（避免重复） =============
    // public static class SimulatedDeckLoader
    // {
    //     public static HashSet<string> Load(string path)
    //     {
    //         HashSet<string> decks = new();
    //         if (!File.Exists(path)) return decks;

    //         var raw = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(
    //             File.ReadAllText(path)
    //         );

    //         foreach (var record in raw)
    //         {
    //             var ids = ((Newtonsoft.Json.Linq.JArray)record["deck_card_ids"])
    //                 .Select(v => (int)v)
    //                 .OrderBy(x => x);

    //             decks.Add(string.Join(",", ids));
    //         }

    //         return decks;
    //     }
    // }

    // =================== 主生成器 ===================
    public class DeckGenerator : IEnumerable<(int[] deck, int? center)>
    {
        List<int> cardpool;
        List<List<int>> mustcards;
        int? centerChar;
        HashSet<int> centerCard;
        Dictionary<int, List<int>> charCards = new();
        HashSet<string> simulated;

        public int TotalDecks { get; private set; }

        public DeckGenerator(
            List<int> cardpool,
            List<List<int>> mustcards,
            int? center_char = null,
            HashSet<int> center_card = null,
            string logPath = null)
        {
            this.cardpool = cardpool;
            this.mustcards = mustcards;
            this.centerChar = center_char;
            this.centerCard = center_card;
            // this.simulated = logPath == null ? new() : SimulatedDeckLoader.Load(logPath);

            TagGenerator.BuildDBTag();

            foreach (int cid in cardpool)
            {
                int charId = cid / 1000;
                if (!charCards.ContainsKey(charId))
                    charCards[charId] = new List<int>();
                charCards[charId].Add(cid);
            }

            TotalDecks = ComputeTotalCount();
        }

        // 迭代生成所有卡组
        public IEnumerator<(int[] deck, int? center)> GetEnumerator()
        {
            var allChars = charCards.Keys.ToList();
            if (allChars.Count < 3) yield break;

            foreach (var distr in RoleDistribution.GenerateRoleDistributions(allChars))
            {
                if (centerChar.HasValue && !distr.Contains(centerChar.Value))
                    continue;

                foreach (var item in GenerateByDistribution(distr))
                    yield return item;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        // ---------------- 检查技能 ----------------
        bool CheckSkillTags(Dictionary<object, int> tagCount)
        {
            // mustcards[2]：需要所有技能都不为0（与原逻辑一致）
            foreach (var skill in mustcards[2])
            {
                if (!tagCount.TryGetValue((SkillEffectType)skill, out int cnt) || cnt == 0)
                    return false;
            }

            // DR <= 1
            tagCount.TryGetValue(Rarity.DR, out int drCount);
            return drCount <= 1;
        }

        // ================== 分发角色 → 枚举卡组 ==================
        IEnumerable<(int[], int?)> GenerateByDistribution(int[] distribution)
        {
            var charCounts = distribution.GroupBy(x => x)
                                        .ToDictionary(g => g.Key, g => g.Count());

            List<List<int[]>> cardChoices = new();

            foreach (var (cid, count) in charCounts)
            {
                var pool = charCards[cid];

                if (count == 1)
                {
                    cardChoices.Add(pool.Select(c => new[] { c }).ToList());
                }
                else if (count == 2)
                {
                    cardChoices.Add(Combinations(pool, 2).ToList());
                }
            }

            foreach (var pick in Cartesian(cardChoices))
            {
                List<int> deck = pick.SelectMany(a => a).ToList();
                deck.Sort();
                string deckKey = string.Join(",", deck);

                // if (simulated.Contains(deckKey)) continue;
                if (!CheckMustCards(deck)) continue;
                if (HasCardConflict(deck)) continue;

                var tags = SkillTagCounter.CountSkillTags(deck);
                if (!CheckSkillTags(tags)) continue;

                HashSet<int> centers =
                    centerCard != null ? centerCard.Intersect(deck).ToHashSet() :
                                        new HashSet<int> { 0 };

                if (centerCard != null && centers.Count == 0)
                    continue;

                foreach (var perm in Permute(deck))
                {
                    if (DB.DB_TAG[perm[0]].Contains(SkillEffectType.ScoreGain)) continue;
                    if (DB.DB_TAG[perm[^1]].Contains(SkillEffectType.DeckReset)) continue;

                    if (centerCard != null)
                    {
                        foreach (var c in centers)
                            yield return (perm, c);
                    }
                    else
                    {
                        yield return (perm, null);
                    }
                }
            }
        }

        // =================================== 工具方法 ===================================

        bool CheckMustCards(List<int> deck)
        {
            if (mustcards[0].Count > 0 &&
                !mustcards[0].All(deck.Contains))
                return false;

            if (mustcards[1].Count > 0 &&
                !mustcards[1].Any(deck.Contains))
                return false;

            return true;
        }

        bool HasCardConflict(List<int> deck)
        {
            // 你可以在这里实现你自己的冲突判断逻辑
            return false;
        }

        // 组合
        static IEnumerable<int[]> Combinations(List<int> list, int k)
        {
            if (k == 0) { yield return Array.Empty<int>(); yield break; }
            if (k > list.Count) yield break;

            for (int i = 0; i < list.Count; i++)
            {
                foreach (var tail in Combinations(list.Skip(i + 1).ToList(), k - 1))
                {
                    yield return (new[] { list[i] }).Concat(tail).ToArray();
                }
            }
        }

        // 笛卡尔积
        static IEnumerable<List<T>> Cartesian<T>(List<List<T>> sequences)
        {
            IEnumerable<List<T>> result = new[] { new List<T>() };
            foreach (var seq in sequences)
            {
                result =
                    from r in result
                    from s in seq
                    select r.Concat(new[] { s }).ToList();
            }
            return result;
        }

        // 全排列
        static IEnumerable<int[]> Permute(List<int> list)
        {
            return PermuteInternal(list, 0);
        }
        static IEnumerable<int[]> PermuteInternal(List<int> arr, int start)
        {
            if (start >= arr.Count)
                yield return arr.ToArray();

            for (int i = start; i < arr.Count; i++)
            {
                (arr[start], arr[i]) = (arr[i], arr[start]);
                foreach (var result in PermuteInternal(arr, start + 1))
                    yield return result;
                (arr[start], arr[i]) = (arr[i], arr[start]);
            }
        }

        // 计算卡组总数
        int ComputeTotalCount()
        {
            int total = 0;
            var allChars = charCards.Keys.ToList();
            if (allChars.Count < 3) return 0;

            foreach (var dist in RoleDistribution.GenerateRoleDistributions(allChars))
            {
                if (centerChar.HasValue && !dist.Contains(centerChar.Value))
                    continue;

                total += CountByDistribution(dist);
            }
            return total;
        }

        int CountByDistribution(int[] distribution)
        {
            int count = 0;

            foreach (var (deck, center) in GenerateByDistribution(distribution))
                count++;

            return count;
        }
    }

}

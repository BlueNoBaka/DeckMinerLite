using DeckMiner.Data;
using System.IO;
using System.Text.Json;
using System;

namespace DeckMiner.Services
{
    public class ChartLoaderService
    {
        /// <summary>
        /// 从 JSON 文件加载 ChartData。
        /// </summary>
        /// <param name="jsonPath">JSON 文件的完整路径。</param>
        /// <returns>反序列化后的 ChartData 对象。</returns>
        public ChartData LoadChartFromJson(string jsonPath)
        {
            if (!File.Exists(jsonPath))
            {
                throw new FileNotFoundException($"Chart JSON 文件未找到: {jsonPath}");
            }

            try
            {
                // 1. 读取 JSON 字符串
                string jsonString = File.ReadAllText(jsonPath);

                // 2. AOT 兼容的反序列化
                // 获取 ChartData 类型的 TypeInfo
                var typeInfo = AppJsonSerializerContext.Default.ChartData;
                
                // 进行反序列化
                var chart = (ChartData)JsonSerializer.Deserialize(jsonString, typeInfo);
                
                if (chart == null)
                {
                    throw new JsonException("JSON 反序列化失败，返回 null。数据格式可能不匹配。");
                }

                return chart;
            }
            catch (JsonException ex)
            {
                throw new JsonException($"JSON 文件解析失败 ({jsonPath})。请检查内容格式是否与 ChartData 匹配。", ex);
            }
            catch (Exception ex)
            {
                throw new IOException($"读取文件失败: {jsonPath}", ex);
            }
        }
    }
}
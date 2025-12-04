using System.IO;
using System.IO.Compression; // .NET 内置的压缩库
using System.Text.Json;
using System.Numerics;       // 可能用于 ulong 的位运算
using DeckMiner.Data;

namespace DeckMiner.Services
{
    // 确保使用 Source Generator Context
    // using ChartJsonContext = AppJsonSerializerContext.Default.ChartJsonData; 

    public class ChartParserService
    {
        private const int WBITS_RAW = -15; // zlib raw inflate/deflate window bits
        
        // 对应 Python 的 _loadbytes 逻辑
        public ChartJsonData LoadAndDecompress(string bytesPath)
        {
            if (!File.Exists(bytesPath))
            {
                throw new FileNotFoundException($"谱面文件未找到: {bytesPath}");
            }

            // 1. 读取压缩的 bytes
            byte[] compressedData = File.ReadAllBytes(bytesPath);

            // 2. 解压 (zlib raw decompress)
            // 在 .NET 中，可以使用 System.IO.Compression.ZLibStream 或 DeflateStream
            // 依赖 zlib raw 格式，System.IO.Compression.DeflateStream 通常需要手动处理头部/尾部。
            // 更好的方式是使用第三方库如 K4os.Compression.LZ4, SharpZipLib 或 DeflateStream + 适当封装。
            // **假设我们能用 DeflateStream 成功解压（可能需要处理头部）：**
            
            byte[] jsonBytes;
            try
            {
                // C# 的 DeflateStream 默认使用 RFC 1951 格式，zlib 使用 RFC 1950。
                // 这里的关键是：如果原始压缩是 zlib raw (-15 wbits)，需要使用 DeflateStream，
                // 并且可能要跳过 zlib 头部。如果无法确定，可以尝试：
                using (var compressedStream = new MemoryStream(compressedData))
                {
                    // 假设 compressedData 包含 zlib header，则需要跳过
                    // 如果是原始Deflate/zlib raw，则直接使用 DeflateStream
                    using (var deflateStream = new DeflateStream(compressedStream, CompressionMode.Decompress))
                    {
                        using (var decompressedStream = new MemoryStream())
                        {
                            deflateStream.CopyTo(decompressedStream);
                            jsonBytes = decompressedStream.ToArray();
                        }
                    }
                }
            }
            catch (InvalidDataException ex)
            {
                // DeflateStream 解压失败，可能是格式不匹配
                throw new IOException("Zlib 解压失败。请检查原始压缩格式是否为 Deflate 或 zlib raw。", ex);
            }

            // 3. 反序列化 (AOT 兼容)
            try
            {
                // 使用您配置的 Source Generator Context
                // 假设您在 JsonContext.cs 中为 ChartJsonData 配置了 ChartJsonContext.Default
                var typeInfo = AppJsonSerializerContext.Default.GetTypeInfo(typeof(ChartJsonData));
                
                return (ChartJsonData)JsonSerializer.Deserialize(jsonBytes, typeInfo);
            }
            catch (JsonException ex)
            {
                throw new JsonException($"JSON 解析失败，数据可能损坏。{ex.Message}", ex);
            }
        }
    }
}
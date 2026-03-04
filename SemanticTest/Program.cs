using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Text;
using NPOI.HWPF;
using NPOI.HWPF.Extractor;
using NPOI.XWPF.UserModel;
using System.Text;
using System.Text.Json;

namespace SemanticTest
{
    public class KnowledgeItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Text { get; set; } = "";
        public string FileName { get; set; } = "";
        public float[]? Vector { get; set; }
    }

    internal class Program
    {
        #pragma warning disable SKEXP0001, SKEXP0050 // 禁用 MemoryBuilder 的评估警告

        const string Endpoint = "http://localhost:8080/v1";
        const string DocsPath = @"D:\trace_test\gaotest\.trae\skills\funacapi\resources";
        const string JsonDbPath = "fanuc_knowledge_base.json";

        static async Task Main(string[] args)
        {
            // 关键修复 1: 注册编码提供程序，解决 'Windows-1252' 报错
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            Console.OutputEncoding = Encoding.UTF8;
            await RunRAGSystem();
        }

        private static async Task RunRAGSystem()
        {
            // 1. 创建一个自定义的 HttpClient，指向你的 llama-server
            var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:8080/v1") };

            // 2. 初始化服务
            // 参数1: modelId (随便填)
            // 参数2: apiKey (不能为 null，随便填个占位符)
            // 参数3: organization (设为 null)
            // 参数4: httpClient (关键：通过这个指定本地地址)
            var embeddingService = new OpenAITextEmbeddingGenerationService(
                "qwen-embed",
                "placeholder-key",
                null,
                httpClient
            ); 
            var memoryStore = new VolatileMemoryStore();
            // ✅ 关键修复：确保集合存在
            if (!await memoryStore.DoesCollectionExistAsync("fanuc"))
            {
                await memoryStore.CreateCollectionAsync("fanuc");
            }

            var memory = new SemanticTextMemory(memoryStore, embeddingService);

            // 2. 加载逻辑
            if (File.Exists(JsonDbPath))
            {
                await LoadFromCache(memoryStore);
            }
            else
            {
                await BuildVectorCache(embeddingService, memory);
            }

            // 3. 聊天服务初始化
            var kernel = Kernel.CreateBuilder()
                .AddOpenAIChatCompletion("qwen3", apiKey: "no", endpoint: new Uri(Endpoint))
                .Build();

            // 4. 对话循环
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n>>> Fanuc CNC API 知识库就绪！请输入问题 (exit 退出)");
            Console.ResetColor();

            while (true)
            {
                Console.Write("\n问: ");
                string query = Console.ReadLine() ?? "";
                if (query.ToLower() == "exit") break;

                // 检索相关片段
                var results = memory.SearchAsync("fanuc", query, limit: 3, minRelevanceScore: 0.2);
                var context = new StringBuilder();

                await foreach (var res in results)
                {
                    context.AppendLine($"[来源: {res.Metadata.Description}]\n{res.Metadata.Text}\n");
                }

                if (context.Length == 0)
                {
                    Console.WriteLine("AI: 在知识库中未找到相关信息。");
                    continue;
                }

                // 构造 RAG Prompt
                string prompt = $"""
                你是一个专业 Fanuc CNC API 技术支持专家。
                请严格基于以下【参考资料】回答用户问题。如果资料中未提及，请说明。
                
                【参考资料】:
                {context}
                
                【用户问题】:
                {query}
                """;

                Console.WriteLine("AI 正在思考...");
                var response = await kernel.InvokePromptAsync(prompt);
                Console.WriteLine($"\n答: {response}");
            }
        }

        // --- 核心方法：构建向量缓存 ---
        private static async Task BuildVectorCache(OpenAITextEmbeddingGenerationService embeddingService, SemanticTextMemory memory)
        {
            Console.WriteLine("正在构建向量索引，这可能需要几分钟...");
            var knowledgeList = new List<KnowledgeItem>();
            var files = Directory.GetFiles(DocsPath, "*.*", SearchOption.AllDirectories)
                         .Where(f => f.EndsWith(".doc") || f.EndsWith(".docx")).ToList();

            foreach (var file in files)
            {
                Console.WriteLine($"处理中: {Path.GetFileName(file)}");
                string rawContent = ReadDocumentText(file);

                // 关键：清洗内容，防止 TextChunker 失败
                string cleanContent = CleanText(rawContent);
                if (string.IsNullOrWhiteSpace(cleanContent)) continue;

                // 切分文本
                var lines = TextChunker.SplitPlainTextLines(cleanContent, 128);
                var paragraphs = TextChunker.SplitPlainTextParagraphs(lines, 512);

                if (paragraphs.Count == 0 && cleanContent.Length > 0)
                {
                    paragraphs.Add(cleanContent.Length > 2000 ? cleanContent.Substring(0, 2000) : cleanContent);
                }

                foreach (var para in paragraphs)
                {
                    try
                    {
                        // 生成向量并存入列表
                        var vector = await embeddingService.GenerateEmbeddingAsync(para);
                        var item = new KnowledgeItem
                        {
                            Text = para,
                            FileName = Path.GetFileName(file),
                            Vector = vector.ToArray()
                        };
                        knowledgeList.Add(item);

                        // 存入当前运行内存
                        await memory.SaveInformationAsync("fanuc", para, item.Id, item.FileName);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"向量生成失败: {ex.Message}");
                    }
                }
            }

            // 持久化到 JSON
            var json = JsonSerializer.Serialize(knowledgeList, new JsonSerializerOptions { WriteIndented = false });
            File.WriteAllText(JsonDbPath, json);
            Console.WriteLine($"已完成 {knowledgeList.Count} 个片段的向量化。");
        }

        // --- 核心方法：从缓存加载 ---
        private static async Task LoadFromCache(VolatileMemoryStore memoryStore)
        {
            Console.WriteLine("正在从本地 JSON 加载预计算数据...");
            var json = File.ReadAllText(JsonDbPath);
            var items = JsonSerializer.Deserialize<List<KnowledgeItem>>(json);
            if (items != null)
            {
                foreach (var item in items)
                {
                    await memoryStore.UpsertAsync("fanuc", new MemoryRecord(
                        new MemoryRecordMetadata(true, item.Id, item.Text, "", item.FileName, ""),
                        item.Vector!, null));
                }
                Console.WriteLine($"成功加载 {items.Count} 条记录。");
            }
        }

        // --- 文本读取：兼容 Doc & Docx ---
        static string ReadDocumentText(string path)
        {
            string ext = Path.GetExtension(path).ToLower();
            StringBuilder sb = new StringBuilder();
            try
            {
                using var stream = File.OpenRead(path);
                if (ext == ".docx")
                {
                    var docx = new XWPFDocument(stream);
                    foreach (var p in docx.Paragraphs) sb.AppendLine(p.ParagraphText);
                }
                else
                {
                    // NPOI.ScratchPad 处理 .doc 的方式
                    var doc = new HWPFDocument(stream);
                    var range = doc.GetRange();
                    for (int i = 0; i < range.NumSections; i++)
                    {
                        var section = range.GetSection(i);
                        for (int j = 0; j < section.NumParagraphs; j++)
                        {
                            sb.AppendLine(section.GetParagraph(j).Text);
                        }
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine($"读取出错 {path}: {ex.Message}"); }
            return sb.ToString();
        }

        // --- 清洗函数：移除二进制噪音 ---
        static string CleanText(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "";
            // 移除控制字符但保留换行
            return new string(input.Where(c => !char.IsControl(c) || c == '\n' || c == '\r').ToArray()).Trim();
        }
    }
}
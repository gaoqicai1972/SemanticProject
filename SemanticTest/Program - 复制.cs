//using DocumentFormat.OpenXml.Packaging;
//using Microsoft.SemanticKernel;
//using Microsoft.SemanticKernel.Connectors.OpenAI;
//using Microsoft.SemanticKernel.Embeddings;
//using Microsoft.SemanticKernel.Memory;
//using Microsoft.SemanticKernel.Text;
//using NPOI.HWPF;
//using NPOI.XWPF.UserModel;
//using System.IO;
//using System.Text;
//using System.Text.Json;

//namespace SemanticTest
//{
//    // --- 1. 数据结构：用于持久化存储 ---
//    public class KnowledgeItem
//    {
//        public string Id { get; set; } = Guid.NewGuid().ToString();
//        public string Text { get; set; } = "";
//        public string FileName { get; set; } = "";
//        public float[]? Vector { get; set; } // 核心：保存向量值，避免二次计算
//    }
//    internal class Program
//    {
//#pragma warning disable SKEXP0001, SKEXP0050 // 禁用 MemoryBuilder 的评估警告

//        // --- 2. 配置 ---
//        const string Endpoint = "http://localhost:8080/v1";
//        const string DocsPath = @"D:\trace_test\gaotest\.trae\skills\funacapi\resources";
//        const string JsonDbPath = "fanuc_knowledge_base.json";
//        static async Task Main(string[] args)
//        {
//            Console.WriteLine("Hello, World!");
//            await TestAsync();
//        }

//        private static async Task TestAsync()
//        {
//            var embeddingService = new OpenAITextEmbeddingGenerationService("qwen3", Endpoint);
//            var memoryStore = new VolatileMemoryStore();
//            var memory = new SemanticTextMemory(memoryStore, embeddingService);

//            // --- 3. 初始加载逻辑 ---
//            if (File.Exists(JsonDbPath))
//            {
//                Console.WriteLine("正在从本地 JSON 加载预计算的向量...");
//                var json = File.ReadAllText(JsonDbPath);
//                var items = JsonSerializer.Deserialize<List<KnowledgeItem>>(json);
//                if (items != null)
//                {
//                    foreach (var item in items)
//                    {
//                        // 直接将向量和文本存入内存，不调用 API
//                        await memoryStore.UpsertAsync("fanuc", new MemoryRecord(
//                            new MemoryRecordMetadata(true, item.Id, item.Text, "", item.FileName, ""),
//                            item.Vector!,
//                            null));
//                    }
//                }
//            }
//            else
//            {
//                Console.WriteLine("正在解析文档并生成向量 (第一次运行较慢)...");
//                var knowledgeList = new List<KnowledgeItem>();
//                var files = Directory.GetFiles(DocsPath, "*.doc", SearchOption.AllDirectories);

//                foreach (var file in files)
//                {
//                    string content = ReadDocumentText(file);
//                    var paragraphs = TextChunker.SplitPlainTextParagraphs(TextChunker.SplitPlainTextLines(content, 128), 512);

//                    foreach (var para in paragraphs)
//                    {
//                        // 生成向量
//                        var vector = await embeddingService.GenerateEmbeddingAsync(para);
//                        var item = new KnowledgeItem { Text = para, FileName = Path.GetFileName(file), Vector = vector.ToArray() };
//                        knowledgeList.Add(item);

//                        // 存入当前内存
//                        await memory.SaveInformationAsync("fanuc", para, item.Id, item.FileName);
//                    }
//                }
//                // 保存到本地文件
//                File.WriteAllText(JsonDbPath, JsonSerializer.Serialize(knowledgeList));
//                Console.WriteLine("向量库已持久化到 JSON。");
//            }

//            // --- 4. 问答对话 ---
//            var kernel = Kernel.CreateBuilder()
//                .AddOpenAIChatCompletion("qwen3", apiKey: "no", endpoint: new Uri(Endpoint))
//                .Build();

//            while (true)
//            {
//                Console.Write("\n[Fanuc API 提问]: ");
//                string query = Console.ReadLine() ?? "";
//                if (query == "exit") break;

//                // 语义搜索
//                var results = memory.SearchAsync("fanuc", query, limit: 3, minRelevanceScore: 0.3);
//                var context = new StringBuilder();
//                await foreach (var res in results)
//                {
//                    context.AppendLine($"文件: {res.Metadata.Description}\n内容: {res.Metadata.Text}\n");
//                }

//                // 调用大模型推理
//                var prompt = $"""
//    你是一个 Fanuc CNC 专家。基于以下文档回答。
//    文档内容：
//    {context}
//    用户问题：{query}
//    """;

//                var response = await kernel.InvokePromptAsync(prompt);
//                Console.WriteLine($"\n[回答]:\n{response}");
//            }

//            // 读取 Docx
//            static string ReadDocumentText(string path)
//            {
//                string extension = Path.GetExtension(path).ToLower();
//                StringBuilder sb = new StringBuilder();

//                try
//                {
//                    using var stream = File.OpenRead(path);

//                    if (extension == ".docx")
//                    {
//                        // NPOI 处理 .docx 的方式
//                        var docx = new XWPFDocument(stream);
//                        foreach (var para in docx.Paragraphs)
//                        {
//                            sb.AppendLine(para.ParagraphText);
//                        }
//                    }
//                    else if (extension == ".doc")
//                    {
//                        // NPOI.ScratchPad 处理 .doc 的方式
//                        var doc = new HWPFDocument(stream);
//                        var range = doc.GetRange();
//                        for (int i = 0; i < range.NumSections; i++)
//                        {
//                            var section = range.GetSection(i);
//                            for (int j = 0; j < section.NumParagraphs; j++)
//                            {
//                                sb.AppendLine(section.GetParagraph(j).Text);
//                            }
//                        }
//                    }
//                }
//                catch (Exception ex)
//                {
//                    Console.WriteLine($"[读取失败] {Path.GetFileName(path)}: {ex.Message}");
//                }

//                return sb.ToString();
//            }
//        }
//    }
//}


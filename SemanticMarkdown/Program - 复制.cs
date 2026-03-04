//using Microsoft.SemanticKernel;
//using Microsoft.SemanticKernel.ChatCompletion;
//using Microsoft.SemanticKernel.Connectors.OpenAI;

//namespace SemanticMarkdown
//{
//    internal class Program
//    {
//        static async Task Main(string[] args)
//        {
//            Console.WriteLine("Hello, World!");
//            await StartAsync();
//        }

//        private static async Task StartAsync()
//        {
//            // ====================== 1. Kernel 配置 ======================
//            var builder = Kernel.CreateBuilder();

//            builder.AddOpenAIChatCompletion(
//                modelId: "qwen2.5-3b-instruct-q5_k_m",   // ← 关键！请先运行 curl http://localhost:8080/v1/models 查看实际 model id，通常是 gguf 文件名
//                endpoint: new Uri("http://localhost:8080/v1"),
//                apiKey: "sk-no-key-required"             // llama-server 默认不校验 key，填任意字符串即可
//            );

//            Kernel kernel = builder.Build();

//            // ====================== 2. 注册 funacApi Skill（插件） ======================
//            kernel.Plugins.Add(
//                KernelPluginFactory.CreateFromType<FanucResourcePlugin>("funacApi")
//            );

//            var chatService = kernel.GetRequiredService<IChatCompletionService>();

//            // ====================== 3. System Prompt（skill.md 完整内容 + 工具说明） ======================
//            string systemPrompt = """
//                ---
//                name: funacApi
//                description: 查询funac CNC的API调用时使用
//                ---
//                # Fanuc CNC API 助手 (funacapi)
//                你是一个专门负责 Fanuc CNC Focas 开发的专家。你拥有关于 CNC 程序管理、系统配置和数据采集的详细文档库.

//                ## 技能描述
//                当用户询问关于 Fanuc Focas 库（FWLIB32.dll）的函数用法、C# 调用示例、结构体定义或错误代码时，请调用此技能.

//                ## 资源索引说明
//                你的 API 文档存储在以下路径：
//                - resources/Function related to CNC program/ : 包含程序读写、查询相关的 API（如 cnc_rdproginfo）
//                - resources/Function related to CNC file data/ : 包含参数、偏置、刀补等文件数据的传输与管理
//                - resources/Function related to controlled axis&spindle/ : 包含坐标轴、进给速度、主轴状态及负载数据
//                - resources/Function related to history data/ : 包含报警历史、操作历史记录的读取
//                - resources/Function related to library handle, node/ : 包含库句柄获取（如 cnc_allclibhndl3）及多节点管理
//                - resources/Function related to PMC/ : 包含 PMC 数据（R, D, G, F 地址等）的读写操作
//                - resources/Function related to tool life management data/ : 包含刀具寿命、刀具组及管理数据
//                - resources/Function related to others/ : 包含系统状态、计时器等其他杂项功能

//                ## 核心 API 示例：cnc_rdproginfo (程序信息查询)
//                ...（你提供的完整 skill.md 剩余内容全部粘贴在这里，我这里省略以节省篇幅，实际代码中请把你贴的全部内容复制进去）

//                ## 使用建议
//                1. 始终提醒用户先通过 `cnc_allclibhndl3` 获取句柄
//                2. 检查返回值是否为 `EW_OK` (0)

//                【重要工具使用规则】
//                你**必须**通过以下工具渐进式加载文档，绝不能凭空编造：
//                - funacApi.list_categories → 列出所有类别
//                - funacApi.list_files → 列出某个类别下的所有 .md 文件
//                - funacApi.search_files → 在类别中按关键词搜索文件名（推荐使用）
//                - funacApi.load_file → 加载具体文件完整内容

//                流程示例：
//                用户问 "cnc_rdproginfo 如何使用" → 先 search_files(category="Function related to CNC program", keyword="cnc_rdproginfo") → 再 load_file 加载找到的文件 → 最后用加载的内容回答。
//                """;

//            ChatHistory history = new();
//            history.AddSystemMessage(systemPrompt);

//            Console.WriteLine("🚀 Fanuc CNC API 知识问答系统已启动（渐进式加载 + 函数调用）");
//            Console.WriteLine("输入问题，输入 exit 退出\n");
//            var settings = new OpenAIPromptExecutionSettings
//            {
//                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions, // 自动多轮函数调用
//                MaxTokens = 1024,
//                Temperature = 0.3
//            };

//            while (true)
//            {
//                Console.Write("你: ");
//                string? userInput = Console.ReadLine()?.Trim();
//                if (string.IsNullOrEmpty(userInput)) continue;
//                if (userInput.ToLower() == "exit") break;
//                history.AddUserMessage(userInput);
//                try
//                {
//                    var result = await chatService.GetChatMessageContentsAsync(
//                        history, settings, kernel);

//                    string answer = result[0].Content ?? "无回复";
//                    Console.WriteLine($"助手: {answer}\n");

//                    history.AddAssistantMessage(answer);
//                }
//                catch (Exception ex)
//                {
//                    Console.WriteLine($"❌ 错误: {ex.Message}");
//                }
//            }
//        }
//    }
//}

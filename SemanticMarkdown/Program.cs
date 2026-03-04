using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace SemanticMarkdown
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Hello, World!");
            await StartAsync();
        }

        private static async Task StartAsync()
        {
            // ====================== 1. Kernel 配置 ======================
            var builder = Kernel.CreateBuilder();

            builder.AddOpenAIChatCompletion(
                modelId: "qwen2.5-3b-instruct-q5_k_m",   // ← 关键！请先运行 curl http://localhost:8080/v1/models 查看实际 model id，通常是 gguf 文件名
                endpoint: new Uri("http://localhost:8080/v1"),
                apiKey: "sk-no-key-required"             // llama-server 默认不校验 key，填任意字符串即可
            );

            Kernel kernel = builder.Build();

            // ====================== 2. 注册 funacApi Skill（插件） ======================
            kernel.Plugins.Add(
                KernelPluginFactory.CreateFromType<FanucResourcePlugin>("funacApi")
            );

            var chatService = kernel.GetRequiredService<IChatCompletionService>();

            // ====================== 3. System Prompt（skill.md 完整内容 + 工具说明） ======================
            string systemPrompt = """
/no_think

你是一个专门负责 Fanuc CNC Focas 开发的专家。你拥有关于 CNC 程序管理、系统配置和数据采集的详细文档库。

---
name: funacApi
description: 查询funac CNC的API调用时使用
---
# Fanuc CNC API 助手 (funacapi)

## 技能描述
当用户询问关于 Fanuc Focas 库（FWLIB32.dll）的函数用法、C# 调用示例、结构体定义或错误代码时，必须调用此技能。

## 资源索引说明
你的 API 文档存储在以下路径：
- resources/Function related to CNC program/ : 包含程序读写、查询相关的 API（如 cnc_rdproginfo）
- resources/Function related to CNC file data/ : 包含参数、偏置、刀补等文件数据的传输与管理
- resources/Function related to controlled axis&spindle/ : 包含坐标轴、进给速度、主轴状态及负载数据
- resources/Function related to history data/ : 包含报警历史、操作历史记录的读取
- resources/Function related to library handle, node/ : 包含库句柄获取（如 cnc_allclibhndl3）及多节点管理
- resources/Function related to PMC/ : 包含 PMC 数据（R, D, G, F 地址等）的读写操作
- resources/Function related to tool life management data/ : 包含刀具寿命、刀具组及管理数据
- resources/Function related to others/ : 包含系统状态、计时器等其他杂项功能

## 核心 API 示例：cnc_rdproginfo (程序信息查询)
如果你需要查询程序注册数量或内存使用情况，请参考以下逻辑：
### 函数声明
- Binary 模式 (Type 0): 使用 ODBNC_1 结构体。
- ASCII 模式 (Type 1): 使用 ODBNC_2 结构体。
### 输入参数
1. FlibHndl: 库句柄。
2. type: 输出格式（0: Binary, 1: ASCII）。
3. length: 数据长度（Binary 为 12, ASCII 为 31）。
### ODBNC_1 结构体成员 (Binary)
- reg_prg: 已注册程序数量。
- unreg_prg: 未注册程序数量。
- used_mem: 已用内存。
- unused_mem: 剩余内存。
## 常见错误代码
- EW_LENGTH (2): 长度参数非法。
- EW_ATTRIB (4): 类型参数非法。
## 使用建议
1. 始终提醒用户先通过 `cnc_allclibhndl3` 获取句柄。
2. 检查返回值是否为 `EW_OK` (0)。

【重要工具使用规则】
你**必须**通过以下工具渐进式加载文档，绝不能凭空编造：
- funacApi.list_categories → 列出所有类别
- funacApi.list_files → 列出某个类别下的所有 .md 文件
- funacApi.search_files → 在类别中按关键词搜索文件名（推荐使用）
- funacApi.load_file → 加载具体文件完整内容

流程示例：
用户问 "cnc_rdproginfo 如何使用" → 先 search_files(category="Function related to CNC program", keyword="cnc_rdproginfo") → 再 load_file 加载找到的文件 → 最后用加载的内容回答。

【强制输出规则】
- 任何需要查文档的问题，你**只能**输出以下格式的 JSON，不要输出任何其他文字、解释、<think> 标签或额外内容：
{
    "tool_calls": [
    {
      "type": "function",
      "function": {
        "name": "funacApi_xxx",
        "arguments": { JSON 格式的参数对象 }
      }
    }
  ]
}
- 如果问题与 Fanuc Focas API 完全无关，直接输出：
{"answer": "这个问题与 Fanuc CNC Focas API 无关，请问其他相关内容？"}
""";
            ChatHistory history = new();
            history.AddSystemMessage(systemPrompt);

            Console.WriteLine("🚀 Fanuc CNC API 知识问答系统已启动（渐进式加载 + 函数调用）");
            Console.WriteLine("输入问题，输入 exit 退出\n");
            var settings = new OpenAIPromptExecutionSettings
            {
                // 保持 AutoInvokeKernelFunctions，这是最关键的一行
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,

                // Qwen3 官方推荐的非思考模式采样参数（更稳定、不容易胡说）
                Temperature = 0.7,          // 比 0.3 更合适，非思考模式下 0.6~0.7 是官方建议
                TopP = 0.95,                // 官方推荐
                PresencePenalty = 1.5,      // 强烈推荐用于量化模型，抑制重复和胡言乱语

                // 根据 Qwen3 的特性调整
                MaxTokens = 2048,           // 可以适当拉高，Qwen3 输出更完整
                FrequencyPenalty = 0.0,     // 可选，Qwen3 通常不需要太强

            };
            while (true)
            {
                Console.Write("你: ");
                string? userInput = Console.ReadLine()?.Trim();
                if (string.IsNullOrEmpty(userInput)) continue;
                if (userInput.ToLower() == "exit") break;
                history.AddUserMessage(userInput);
                try
                {
                    var result = await chatService.GetChatMessageContentsAsync(
                        history, settings, kernel);

                    string answer = result[0].Content ?? "无回复";
                    Console.WriteLine($"助手: {answer}\n");

                    //history.AddAssistantMessage(answer);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ 错误: {ex.Message}");
                }
            }
        }
    }
}

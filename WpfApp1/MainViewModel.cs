using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Collections.ObjectModel;
using System.Windows;


namespace WpfApp1
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly Kernel _kernel;
        private readonly IChatCompletionService _chatService;
        private readonly ChatHistory _history = new();
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
用户问 "如何使用 cnc_rdproginfo" 
→ 先调用 list_categories 获取所有类别 
→ 根据 API 名称推测可能属于 CNC program 类别，调用 search_files(category="Function related to CNC program", keyword="cnc_rdproginfo") 
→ 若找到文件，加载并回答；若未找到，继续在其他相关类别（如 Function related to tool life management data）中搜索。
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

        [ObservableProperty] private string _userInput = "";
        [ObservableProperty] private bool _isBusy;

        // 存储聊天记录
        public ObservableCollection<ChatMessage> Messages { get; } = new();

        public MainViewModel(Kernel kernel)
        {
            _kernel = kernel;
            _chatService = kernel.GetRequiredService<IChatCompletionService>();
            _history = new ChatHistory(systemPrompt); // 填入你的 System Prompt
        }

        [RelayCommand]
        private async Task SendMessageAsync()
        {
            if (string.IsNullOrWhiteSpace(UserInput)) return;

            var userMsg = UserInput;
            UserInput = string.Empty;
            IsBusy = true;

            // 添加用户消息
            Messages.Add(new ChatMessage { Role = "User", Content = userMsg });
            _history.AddUserMessage(userMsg);

            // 创建一个空的助手消息用于接收流
            var assistantMsg = new ChatMessage { Role = "Assistant", Content = "" };
            Messages.Add(assistantMsg);

            try
            {
                //var settings = new OpenAIPromptExecutionSettings
                //{
                //    // 关键点 1：自动调用插件
                //    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,

                //    // 关键点 2：降低随机性，强制模型直接输出结论，不进行发散思考
                //    Temperature = 0.7,

                //    // 关键点 3：核采样，配合低 Temp 效果更佳
                //    TopP = 0.9,

                //    // 关键点 4：惩罚项，防止模型在推理时陷入死循环或重复废话
                //    PresencePenalty = 1.3,
                //    FrequencyPenalty = 0.35,

                //    MaxTokens = 2048,
                //};

                var settings = new OpenAIPromptExecutionSettings
                {
                    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                    Temperature = 0.7,
                    TopP = 0.95
                };

                // 使用流式接口
                IAsyncEnumerable<StreamingChatMessageContent> streamingResults =
                    _chatService.GetStreamingChatMessageContentsAsync(_history, settings, _kernel);

                string fullContent = "";
                await foreach (var chunk in streamingResults)
                {
                    if (chunk.Content != null)
                    {
                        fullContent += chunk.Content;
                        // 实时更新 UI 绑定的 Content 属性
                        assistantMsg.Content = fullContent;
                    }
                }

                // 最后将完整回复存入历史
                //_history.AddAssistantMessage(fullContent);
            }
            catch (Exception ex)
            {
                assistantMsg.Content += $"\n\n❌ 运行错误: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }

}


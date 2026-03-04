using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Collections.ObjectModel;
using System.Windows;


namespace WpfApp2
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly Kernel _kernel;
        private readonly IChatCompletionService _chatService;
        private  ChatHistory _history = new();

        // ==================== 新增 ====================
        public ObservableCollection<ExpertItem> Experts { get; } = new()
    {
        new ExpertItem { Name = "Funac CNC 专家", Key = "Fanuc" },
        new ExpertItem { Name = "EPLAN 专家", Key = "EPLAN" },
        new ExpertItem { Name = "Solidworks 专家", Key = "Solidworks" }
    };

        [ObservableProperty]
        private ExpertItem _selectedExpert;


        string _fanucSystemPrompt = """
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

        private readonly string _eplanSystemPrompt = """
/no_think

你是一个专门负责 EPLAN Electric P8 开发的专家。你拥有关于 EPLAN 项目管理、原理图设计、PLC集成、报表生成和API调用的详细文档库。

---
name: eplanApi
description: 查询EPLAN API调用时使用
---
# EPLAN API 助手 (eplanApi)

## 技能描述
当用户询问关于 EPLAN API 的函数用法、C# 调用示例、对象模型或错误代码时，必须调用此技能。

## 资源索引说明
...（类似 Fanuc，改成 EPLAN 对应的类别路径，例如：
- resources/Function related to project management/
- resources/Function related to schematic/
- resources/Function related to PLC integration/
等）

【重要工具使用规则】
你**必须**通过以下工具渐进式加载文档：
- eplanApi.list_categories
- eplanApi.list_files
- eplanApi.search_files
- eplanApi.load_file

...（其余强制输出 JSON 部分把 funacApi_xxx 全部改成 eplanApi_xxx）
""";

        private readonly string _solidworksSystemPrompt = """
/no_think

你是一个专门负责 solidworksApi 开发的专家。你拥有关于 EPLAN 项目管理、原理图设计、PLC集成、报表生成和API调用的详细文档库。

---
name: solidworksApi
description: 查询solidworksApi API调用时使用
---
# solidworksApi 助手 (solidworksApi)

## 技能描述
当用户询问关于 solidworksApi 的函数用法、C# 调用示例、对象模型或错误代码时，必须调用此技能。

## 资源索引说明
...（类似 Fanuc，改成 EPLAN 对应的类别路径，例如：
- resources/Function related to project management/
- resources/Function related to schematic/
- resources/Function related to PLC integration/
等）

【重要工具使用规则】
你**必须**通过以下工具渐进式加载文档：
- solidworksApi.list_categories
- solidworksApi.list_files
- solidworksApi.search_files
- solidworksApi.load_file

...（其余强制输出 JSON 部分把 funacApi_xxx 全部改成 eplanApi_xxx）
""";

        [ObservableProperty] private string _userInput = "";
        [ObservableProperty] private bool _isBusy;

        // 存储聊天记录
        public ObservableCollection<ChatMessage> Messages { get; } = new();

        public MainViewModel(Kernel kernel)
        {
            _kernel = kernel;
            _chatService = kernel.GetRequiredService<IChatCompletionService>();

            // 默认选中第一个
            SelectedExpert = Experts[0];  // 会触发 OnSelectedExpertChanged
        }
        partial void OnSelectedExpertChanged(ExpertItem? oldValue, ExpertItem newValue)
        {
            if (newValue == null) return;

            // 切换专家 → 重置会话
            Messages.Clear();
            _history = new ChatHistory(GetSystemPrompt(newValue.Key));

            // 可选：添加一条系统提示消息给用户
            Messages.Add(new ChatMessage { Role = "Assistant", Content = $"已切换到 {newValue.Name} 模式，新的对话已开始。" });
        }
        private string GetSystemPrompt(string key) => key switch
        {
            "Fanuc" => _fanucSystemPrompt,
            "EPLAN" => _eplanSystemPrompt,
            "Solidworks" => _solidworksSystemPrompt,
            _ => _fanucSystemPrompt
        };


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


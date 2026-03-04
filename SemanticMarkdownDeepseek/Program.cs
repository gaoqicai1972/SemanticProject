using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Threading.Tasks;

namespace SemanticMarkdownDeepseek
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Hello, SemanticMarkdownDeepseek!");
            await TestAsync();
        }

        private static async Task TestAsync()
        {
            // 1. 配置 Kernel
            var builder = Kernel.CreateBuilder();

            // 使用本地 llama-server 的 OpenAI 兼容端点
            builder.AddOpenAIChatCompletion(
                modelId: "qwen2.5-3b-instruct",        // 模型名称，可任意
                endpoint: new Uri("http://localhost:8080/v1"),
                apiKey: "not-needed"                    // llama-server 通常不检查 API Key
            );

            // 2. 添加自定义插件
            builder.Plugins.AddFromType<FanucApiPlugin>();

            var kernel = builder.Build();

            // 3. 获取聊天服务
            var chat = kernel.GetRequiredService<IChatCompletionService>();

            // 4. 创建聊天历史，加入系统提示（可选）
            var chatHistory = new ChatHistory("""
    你是一个专门负责 Fanuc CNC Focas 开发的专家助手。
    你可以通过以下函数获取详细的 API 文档信息：
    - GetApiCategories: 获取所有 API 类别（如 CNC program、PMC 等）及其描述。
    - ListApisInCategory: 根据类别名称列出该类别下的所有 API 名称。
    - GetApiDetails: 根据 API 名称获取完整的 Markdown 文档内容。
    当用户询问具体 API 的用法、参数、结构体或错误码时，请先调用相应函数获取信息，然后基于返回内容回答。
    如果用户的问题比较宽泛，你可以先调用 GetApiCategories 了解类别，再引导用户细化问题。
    """);

            // 5. 启用自动函数调用
            var executionSettings = new OpenAIPromptExecutionSettings
            {
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
            };

            // 6. 交互循环
            Console.WriteLine("Fanuc Focas API 助手已启动（输入 exit 退出）");
            while (true)
            {
                Console.Write("\n用户: ");
                var input = Console.ReadLine();
                if (string.IsNullOrEmpty(input) || input.Equals("exit", StringComparison.OrdinalIgnoreCase))
                    break;

                chatHistory.AddUserMessage(input);

                // 调用模型
                var response = await chat.GetChatMessageContentAsync(chatHistory, executionSettings, kernel);
                Console.WriteLine($"助手: {response}");

                chatHistory.AddAssistantMessage(response.Content);
            }
        }
    }
}

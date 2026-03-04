#项目概述
•	WpfApp2 是一个基于 .NET 8 的 Windows WPF 演示应用，集成了 Microsoft Semantic Kernel 与本地/远端 LLM（示例使用本地 llama-server），通过插件化方式把本地 Markdown 文档库作为“专家”知识源，提供流式聊天与 Markdown 渲染的交互式界面。
#主要特性
•	基于 CommunityToolkit.Mvvm 的 MVVM 架构。
•	使用 Microsoft.SemanticKernel + Microsoft.SemanticKernel.Connectors.OpenAI 调用模型（示例配置为本地 llama-server）。
•	插件化资源检索：FanucResourcePlugin、EplanResourcePlugin、SolidworksResourcePlugin（读取本地 Markdown 文件并作为 Kernel 插件暴露函数）。
•	流式聊天显示（使用 Kernel 的流式 ChatCompletion），UI 实时更新回复。
•	使用 Markdig / Markdig.Wpf 将 Markdown 渲染为 WPF FlowDocument。
#依赖（来自 WpfApp2.csproj）
•	CommunityToolkit.Mvvm 8.4.0
•	Markdig 0.45.0
•	Markdig.Wpf 0.5.0.1
•	Microsoft.SemanticKernel 1.71.0
•	Microsoft.SemanticKernel.Connectors.OpenAI 1.71.0
#运行前准备
•	操作系统：Windows（WPF）。
•	.NET SDK：.NET 8。
•	IDE：建议使用 Visual Studio 2026 打开并还原 NuGet 包。
•	模型端点：示例代码在 App.xaml.cs 中通过 builder.AddOpenAIChatCompletion(...) 指向 http://localhost:8080/v1（模型 Qwen2.5-7B-Instruct-Q4_K_M ，apiKey 示例为 sk-no-key-required）。如需使用 OpenAI/其他服务，请修改 App.xaml.cs 中的相关配置。
•	本地资源目录（必须存在或修改为你的路径）：
•	Fanuc: D:\funacapimd\resources
•	EPLAN: D:\eplan\resources
•	Solidworks 插件路径（如存在）同理在对应插件中配置
#快速启动（摘要）
•	打开工程 -> 还原 NuGet -> 编译并运行。
•	确保本地模型服务或远端 API 可用，且资源目录存在（否则启动时会弹出错误并退出）。
#关键文件说明
•	WpfApp2.csproj：项目与依赖声明（.NET 8，WPF）。
•	App.xaml.cs：初始化 Kernel、注册插件、设置 ChatCompletion（模型端点）并注入 MainViewModel。
•	MainViewModel.cs：聊天逻辑、专家（Expert）切换、使用流式 ChatCompletion 更新消息集合。
•	MainWindow.xaml / MainWindow.xaml.cs：界面与流式 Markdown 渲染（Markdig.Wpf）和自动滚动逻辑。
•	FanucDocPlugin.cs / EplanResourcePlugin.cs / SolidworksResourcePlugin.cs：Kernel 插件，暴露 list_categories、list_files、search_files、load_file 等函数，读取本地 Markdown 文档作为知识来源。
•	ChatMessage.cs：消息模型与辅助类型（ExpertItem）。
#开发注意事项
•	资源目录路径硬编码在插件中，克隆后请根据本地环境修改这些路径或创建对应目录和 Markdown 文档。
•	App.xaml.cs 中的模型端点/模型 ID 可按需替换；在使用公开 API 时注意安全保存 API Key。
•	插件函数使用 [KernelFunction] 标注，遵循 Semantic Kernel 的插件约定以支持自动调用工具（AutoInvokeKernelFunctions）。
•	UI 的 Markdown 渲染依赖 Markdig.Wpf，大段 Markdown 输出会被转为 FlowDocument。
#贡献与许可
•	欢迎提 Issues / Pull Requests。请在提交前确保变更在 Windows/.NET 8 环境下可复现。
•	仓库未在此处声明许可证（请根据需要补充 LICENSE 文件）。


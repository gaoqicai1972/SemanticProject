using Microsoft.SemanticKernel;
using System.Configuration;
using System.Data;
using System.IO;
using System.Windows;

namespace WpfApp2
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                // 1. 初始化 Kernel 构建器
                var builder = Kernel.CreateBuilder();

                // 2. 配置本地 Llama-server 接口
                // 请确保端口与你运行的 llama-server (http://localhost:8080) 一致
                builder.AddOpenAIChatCompletion(
                    modelId: "Qwen2.5-7B-Instruct-Q4_K_M ",
                    endpoint: new Uri("http://localhost:8080/v1"),
                    apiKey: "sk-no-key-required"
                );

                // 3. 注册 Fanuc 资源插件
                builder.Plugins.Add(KernelPluginFactory.CreateFromType<FanucResourcePlugin>("funacApi"));
                builder.Plugins.Add(KernelPluginFactory.CreateFromType<EplanResourcePlugin>("eplanApi"));
                builder.Plugins.Add(KernelPluginFactory.CreateFromType<SolidworksResourcePlugin>("solidworksApi"));
                // 4. 构建 Kernel 实例
                Kernel kernel = builder.Build();

                // 5. 创建 ViewModel 并注入 Kernel
                // 注意：ViewModel 构造函数需要接收 Kernel 实例
                var viewModel = new MainViewModel(kernel);

                // 6. 实例化并显示主窗体
                var mainWindow = new MainWindow
                {
                    DataContext = viewModel
                };

                mainWindow.Show();
            }
            catch (DirectoryNotFoundException ex)
            {
                // 专门处理资源路径 D:\funacapimd\resources 不存在的情况
                MessageBox.Show($"资源目录配置错误: {ex.Message}\n请检查插件代码中的路径。",
                                "启动失败", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"系统初始化失败: {ex.Message}",
                                "致命错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }
    }

}

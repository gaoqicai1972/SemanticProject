using Markdig;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace WpfApp1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainViewModel ViewModel => DataContext as MainViewModel;
        public MainWindow()
        {
            InitializeComponent();
            // 监听 ContentRendered 确保 DataContext 已经由 App.xaml.cs 注入
            this.Loaded += (s, e) =>
            {
                if (ViewModel?.Messages != null)
                {
                    ((INotifyCollectionChanged)ViewModel.Messages).CollectionChanged += (sender, args) =>
                    {
                        // 异步执行滚动，确保 UI 已经渲染完成
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            ChatScrollViewer.ScrollToEnd();
                        }));
                    };
                }
            };
        }

        private void RichTextBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is RichTextBox rtb && rtb.DataContext is ChatMessage msg)
            {
                // 监听 Content 变化实现流式刷新
                msg.PropertyChanged += (s, args) =>
                {
                    if (args.PropertyName == nameof(ChatMessage.Content))
                    {
                        UpdateRichTextBox(rtb, msg.Content);
                    }
                };
                UpdateRichTextBox(rtb, msg.Content);
            }
        }

        private void UpdateRichTextBox(RichTextBox rtb, string markdown)
        {
            // 使用 Markdig 将 Markdown 转为 WPF 原生的 FlowDocument
            var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
            FlowDocument doc = Markdig.Wpf.Markdown.ToFlowDocument(markdown, pipeline);
            // 设置一些基础样式（如字体、间距）
            doc.FontFamily = new FontFamily("Microsoft YaHei");
            doc.FontSize = 14;
            doc.PagePadding = new Thickness(0);

            rtb.Document = doc;
        }
    }
}
using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace SemanticMarkdown
{
    public class FanucResourcePlugin
    {
        private readonly string _basePath = @"D:\funacapimd\resources";

        public FanucResourcePlugin()
        {
            if (!Directory.Exists(_basePath))
                throw new DirectoryNotFoundException($"资源目录不存在: {_basePath}");
        }

        [KernelFunction("list_categories")]
        [Description("列出所有可用的资源类别")]
        public string ListCategories()
        {
            var cats = Directory.GetDirectories(_basePath)
                .Select(Path.GetFileName)
                .OrderBy(x => x)
                .ToList();

            return cats.Count > 0
                ? "可用类别:\n" + string.Join("\n", cats)
                : "未找到任何类别";
        }

        [KernelFunction("list_files")]
        [Description("列出指定类别下的所有 .md 文件")]
        public string ListFiles([Description("精确类别名称")] string category)
        {
            var dir = Path.Combine(_basePath, category);
            if (!Directory.Exists(dir)) return $"类别不存在: {category}";

            var files = Directory.GetFiles(dir, "*.md")
                .Select(Path.GetFileName)
                .OrderBy(x => x)
                .ToList();

            return files.Count > 0
                ? $"类别 {category} 中的文件:\n" + string.Join("\n", files)
                : "该类别无 .md 文件";
        }

        [KernelFunction("search_files")]
        [Description("在指定类别中搜索文件名包含关键词的文件（不区分大小写）")]
        public string SearchFiles(
            [Description("类别名称")] string category,
            [Description("关键词，例如 cnc_rdproginfo 或 program")] string keyword)
        {
            var dir = Path.Combine(_basePath, category);
            if (!Directory.Exists(dir)) return $"类别不存在: {category}";

            if (string.IsNullOrWhiteSpace(keyword))
                return ListFiles(category);

            var files = Directory.GetFiles(dir, "*.md")
                .Where(f => Path.GetFileName(f).Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .Select(Path.GetFileName)
                .OrderBy(x => x)
                .ToList();

            return files.Count > 0
                ? $"在 {category} 中找到匹配 '{keyword}' 的文件:\n" + string.Join("\n", files)
                : $"未找到包含 '{keyword}' 的文件";
        }

        [KernelFunction("load_file")]
        [Description("加载指定类别和文件的完整 Markdown 内容")]
        public string LoadFile(
            [Description("类别名称")] string category,
            [Description("文件名（含 .md）")] string fileName)
        {
            var fullPath = Path.Combine(_basePath, category, fileName);
            if (!File.Exists(fullPath)) return $"文件不存在: {category}/{fileName}";

            try
            {
                string content = File.ReadAllText(fullPath);
                return $"\n--- 开始 {category}/{fileName} ---\n{content}\n--- 文件结束 ---\n";
            }
            catch (Exception ex)
            {
                return $"读取文件失败: {ex.Message}";
            }
        }
    }
}

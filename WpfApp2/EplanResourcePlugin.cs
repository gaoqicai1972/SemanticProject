using System.ComponentModel;
using System.IO;
using Microsoft.SemanticKernel;

namespace WpfApp2
{
    public class EplanResourcePlugin
    {
        private readonly string _basePath = @"D:\eplan\resources";

        public EplanResourcePlugin()
        {
            if (!Directory.Exists(_basePath))
                throw new DirectoryNotFoundException($"EPLAN 资源目录不存在: {_basePath}");
        }

        [KernelFunction("list_categories")]
        [Description("列出所有可用的 EPLAN 资源类别")]
        public string ListCategories() => /* 与 Fanuc 完全相同的实现代码 */
            // （复制 FanucResourcePlugin.cs 中的 ListCategories 完整代码）
            Directory.GetDirectories(_basePath)
                .Select(Path.GetFileName)
                .OrderBy(x => x)
                .ToList() is var cats && cats.Count > 0
                ? "可用 EPLAN 类别:\n" + string.Join("\n", cats)
                : "未找到任何 EPLAN 类别";

        [KernelFunction("list_files")]
        [Description("列出指定 EPLAN 类别下的所有 .md 文件")]
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
        [Description("在指定 EPLAN 类别中搜索文件名包含关键词的文件（不区分大小写）")]
        public string SearchFiles([Description("类别名称")] string category, [Description("关键词")] string keyword)
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
        [Description("加载指定 EPLAN 类别和文件的完整 Markdown 内容")]
        public string LoadFile([Description("类别名称")] string category, [Description("文件名（含 .md）")] string fileName)
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

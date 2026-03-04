using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace SemanticMarkdownDeepseek
{

    public class FanucApiPlugin
    {
        // 资源根目录
        private static readonly string ResourcesRoot = @"D:\funacapimd\resources";

        // 预定义的类别描述（可从 skill.md 提取，此处硬编码）
        private static readonly Dictionary<string, string> CategoryDescriptions = new()
        {
            ["Function related to CNC program"] = "包含程序读写、查询相关的 API（如 cnc_rdproginfo）",
            ["Function related to CNC file data"] = "包含参数、偏置、刀补等文件数据的传输与管理",
            ["Function related to controlled axis&spindle"] = "包含坐标轴、进给速度、主轴状态及负载数据",
            ["Function related to history data"] = "包含报警历史、操作历史记录的读取",
            ["Function related to library handle, node"] = "包含库句柄获取（如 cnc_allclibhndl3）及多节点管理",
            ["Function related to PMC"] = "包含 PMC 数据（R, D, G, F 地址等）的读写操作",
            ["Function related to tool life management data"] = "包含刀具寿命、刀具组及管理数据",
            ["Function related to others"] = "包含系统状态、计时器等其他杂项功能"
        };

        // 文件名到绝对路径的索引（懒加载）
        private static Dictionary<string, string>? _fileIndex;
        private static readonly object _lock = new();

        /// <summary>
        /// 获取所有 API 类别及其描述
        /// </summary>
        [KernelFunction]
        [Description("获取所有可用的 API 类别列表，每个类别对应一组相关的 Fanuc Focas 函数。")]
        public string GetApiCategories()
        {
            var lines = CategoryDescriptions.Select(kvp => $"- **{kvp.Key}**: {kvp.Value}");
            return "可用的 API 类别如下：\n" + string.Join("\n", lines);
        }

        /// <summary>
        /// 列出指定类别下的所有 API 名称
        /// </summary>
        /// <param name="category">类别名称，例如 "Function related to CNC program"</param>
        [KernelFunction]
        [Description("根据类别名称列出该类别下所有可用的 API 名称。")]
        public string ListApisInCategory(
            [Description("类别名称，必须与 GetApiCategories 返回的类别名完全一致")] string category)
        {
            // 查找对应的目录名（去除前缀 "Function related to " 等）
            var dirName = GetDirectoryNameFromCategory(category);
            if (dirName == null)
                return $"未找到类别“{category}”，请先调用 GetApiCategories 查看有效类别。";

            var dirPath = Path.Combine(ResourcesRoot, dirName);
            if (!Directory.Exists(dirPath))
                return $"目录不存在：{dirPath}";

            var files = Directory.GetFiles(dirPath, "*.md")
                                 .Select(Path.GetFileNameWithoutExtension)
                                 .Where(name => !string.IsNullOrEmpty(name))
                                 .ToList();

            return files.Any()
                ? $"类别“{category}”包含以下 API：\n" + string.Join("\n", files.Select(f => $"- {f}"))
                : $"类别“{category}”下没有找到任何 API 文档。";
        }

        /// <summary>
        /// 获取指定 API 的详细文档内容（Markdown 原始文本）
        /// </summary>
        /// <param name="apiName">API 名称，例如 "cnc_rdproginfo"</param>
        [KernelFunction]
        [Description("根据 API 名称获取详细的文档内容，包括函数声明、参数、结构体定义、错误代码和示例。")]
        public async Task<string> GetApiDetails(
            [Description("要查询的 API 名称，不包含 .md 后缀")] string apiName)
        {
            // 确保索引已构建
            EnsureFileIndex();

            if (_fileIndex!.TryGetValue(apiName, out var filePath))
            {
                try
                {
                    var content = await File.ReadAllTextAsync(filePath);
                    return $"# {apiName} 文档\n\n{content}";
                }
                catch (Exception ex)
                {
                    return $"读取文档时出错：{ex.Message}";
                }
            }

            // 未找到：尝试模糊搜索（可选）
            var similar = _fileIndex.Keys
                .Where(k => k.Contains(apiName, StringComparison.OrdinalIgnoreCase))
                .Take(5)
                .ToList();

            if (similar.Any())
            {
                return $"未找到 API“{apiName}”。您是不是想查找：\n" + string.Join("\n", similar.Select(s => $"- {s}"));
            }

            return $"未找到 API“{apiName}”，请先调用 ListApisInCategory 查看可用的 API 名称。";
        }

        // ---------- 辅助方法 ----------

        private void EnsureFileIndex()
        {
            if (_fileIndex != null) return;
            lock (_lock)
            {
                if (_fileIndex != null) return;

                var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var dir in Directory.GetDirectories(ResourcesRoot))
                {
                    foreach (var file in Directory.GetFiles(dir, "*.md"))
                    {
                        var name = Path.GetFileNameWithoutExtension(file);
                        if (!string.IsNullOrEmpty(name) && !index.ContainsKey(name))
                        {
                            index[name] = file;
                        }
                    }
                }
                _fileIndex = index;
            }
        }

        private string? GetDirectoryNameFromCategory(string category)
        {
            // 根据类别描述反向查找目录名（简单匹配，实际可维护映射表）
            // 此处使用硬编码映射，更健壮的方式是从 skill.md 中解析或使用配置文件
            return category switch
            {
                "Function related to CNC program" => "Function related to CNC program",
                "Function related to CNC file data" => "Function related to CNC file data",
                "Function related to controlled axis&spindle" => "Function related to controlled axis&spindle",
                "Function related to history data" => "Function related to history data",
                "Function related to library handle, node" => "Function related to library handle, node",
                "Function related to PMC" => "Function related to PMC",
                "Function related to tool life management data" => "Function related to tool life management data",
                "Function related to others" => "Function related to others",
                _ => null
            };
        }
    }
}

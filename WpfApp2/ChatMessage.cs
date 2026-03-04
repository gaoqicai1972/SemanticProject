using CommunityToolkit.Mvvm.ComponentModel;

namespace WpfApp2
{
    public class ChatMessage : ObservableObject
    {
        public string Role { get; set; }
        private string _content;
        public string Content { get => _content; set => SetProperty(ref _content, value); }
    }
    public class ExpertItem
    {
        public string Name { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
    }
}

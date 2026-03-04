using CommunityToolkit.Mvvm.ComponentModel;

namespace WpfApp1
{
    public class ChatMessage : ObservableObject
    {
        public string Role { get; set; }
        private string _content;
        public string Content { get => _content; set => SetProperty(ref _content, value); }
    }

}

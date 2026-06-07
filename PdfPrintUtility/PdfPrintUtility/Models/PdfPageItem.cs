using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

namespace PdfPrintUtility.Models
{
    public class PdfPageItem : INotifyPropertyChanged
    {
        private string _sourceFilePath = string.Empty;
        public string SourceFilePath
        {
            get => _sourceFilePath;
            set { _sourceFilePath = value; OnPropertyChanged(); }
        }

        private int _sourcePageIndex;
        public int SourcePageIndex
        {
            get => _sourcePageIndex;
            set { _sourcePageIndex = value; OnPropertyChanged(); }
        }

        private BitmapSource? _thumbnail;
        public BitmapSource? Thumbnail
        {
            get => _thumbnail;
            set { _thumbnail = value; OnPropertyChanged(); }
        }

        private int _rotation;
        public int Rotation
        {
            get => _rotation;
            set { _rotation = value; OnPropertyChanged(); }
        }

        private string _displayName = string.Empty;
        public string DisplayName
        {
            get => _displayName;
            set { _displayName = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

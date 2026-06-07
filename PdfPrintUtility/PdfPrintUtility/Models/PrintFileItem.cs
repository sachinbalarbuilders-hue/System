using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace PdfPrintUtility.Models
{
    public class PrintFileItem : INotifyPropertyChanged
    {
        private string _filePath = string.Empty;
        public string FilePath
        {
            get => _filePath;
            set { _filePath = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayText)); }
        }

        private int _rotation = 0;
        public int Rotation
        {
            get => _rotation;
            set { _rotation = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayText)); }
        }

        public string DisplayText
        {
            get
            {
                string name = Path.GetFileName(FilePath);
                if (Rotation != 0) return $"{name}  ({Rotation}° CW)";
                return name;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

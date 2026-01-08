using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FileMerger.Models
{
    // Base class for MVVM
    public class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class FileItem : ObservableObject
    {
        private bool _isSelected = true;

        public string Name { get; set; }
        public string FullPath { get; set; }
        public string Extension { get; set; }
        public string Content { get; set; } // Loaded on demand or preloaded

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }
    }
}
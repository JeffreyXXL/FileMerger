using FileMerger.Models;
using Microsoft.Win32; // For SaveFileDialog
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
// Note: Add reference to System.Windows.Forms for FolderBrowserDialog if needed, 
// or use Microsoft.WindowsAPICodePack for modern dialogs.

namespace FileMerger.ViewModels
{
    public class MainViewModel : ObservableObject
    {
        private string _selectedFolderPath;
        private string _previewContent;
        private FileItem _selectedFile;
        private bool _groupByType;
        private bool _isBusy;

        public ObservableCollection<FileItem> Files { get; set; }
        public ICollectionView FilesView { get; set; }

        public string SelectedFolderPath
        {
            get => _selectedFolderPath;
            set { _selectedFolderPath = value; OnPropertyChanged(); }
        }

        public string PreviewContent
        {
            get => _previewContent;
            set { _previewContent = value; OnPropertyChanged(); }
        }

        public FileItem SelectedFile
        {
            get => _selectedFile;
            set
            {
                _selectedFile = value;
                if (value != null) PreviewContent = value.Content;
                OnPropertyChanged();
            }
        }

        public bool GroupByType
        {
            get => _groupByType;
            set
            {
                _groupByType = value;
                ApplyGrouping();
                OnPropertyChanged();
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        public ICommand ScanCommand { get; }
        public ICommand MergeCommand { get; }
        public ICommand ExportCommand { get; }
        public ICommand ToggleGroupCommand { get; }

        public MainViewModel()
        {
            Files = new ObservableCollection<FileItem>();
            FilesView = CollectionViewSource.GetDefaultView(Files);

            ScanCommand = new RelayCommand(async (o) => await ScanFolderAsync());
            MergeCommand = new RelayCommand((o) => MergeFiles());
            ExportCommand = new RelayCommand((o) => ExportMergedContent());
            ToggleGroupCommand = new RelayCommand(ToggleGroup);
        }

        private async Task ScanFolderAsync()
        {
            // Ideally, open FolderBrowserDialog here via a Service
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                SelectedFolderPath = dialog.SelectedPath;
                IsBusy = true;
                Files.Clear();

                await Task.Run(() =>
                {
                    try
                    {
                        var files = Directory.GetFiles(SelectedFolderPath, "*.*", SearchOption.AllDirectories);
                        foreach (var filePath in files)
                        {
                            var info = new FileInfo(filePath);
                            // Simple text check filter (optional)
                            if (IsTextFile(info.Extension))
                            {
                                string content = "";
                                try { content = File.ReadAllText(filePath); } catch { content = "[Error reading file]"; }

                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    Files.Add(new FileItem
                                    {
                                        Name = info.Name,
                                        FullPath = filePath,
                                        Extension = info.Extension,
                                        Content = content
                                    });
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error scanning: {ex.Message}");
                    }
                });

                IsBusy = false;
                ApplyGrouping();
            }
        }

        private bool IsTextFile(string extension)
        {
            string[] textExt = { ".txt", ".cs", ".xml", ".json", ".md", ".xaml", ".css", ".js", ".html", ".config" };
            return textExt.Contains(extension.ToLower());
        }

        private void ApplyGrouping()
        {
            FilesView.GroupDescriptions.Clear();
            if (GroupByType)
            {
                FilesView.GroupDescriptions.Add(new PropertyGroupDescription("Extension"));
            }
        }

        private void ToggleGroup(object parameter)
        {
            // When grouped, the key (Name) is passed as parameter (e.g., ".cs")
            if (parameter is string extension)
            {
                var filesInGroup = Files.Where(f => f.Extension == extension).ToList();
                if (!filesInGroup.Any()) return;

                // Logic: If all are currently selected, deselect all. Otherwise, select all.
                bool allSelected = filesInGroup.All(f => f.IsSelected);
                bool targetState = !allSelected;

                foreach (var file in filesInGroup)
                {
                    file.IsSelected = targetState;
                }
            }
        }

        private void MergeFiles()
        {
            var sb = new StringBuilder();
            var selectedFiles = Files.Where(f => f.IsSelected).ToList();

            sb.AppendLine($"--- Merged {selectedFiles.Count} Files ---");
            sb.AppendLine($"Generated on: {DateTime.Now}");
            sb.AppendLine("------------------------------------------");

            foreach (var file in selectedFiles)
            {
                sb.AppendLine("==========================================");
                sb.AppendLine($"FILE: {file.Name}");
                sb.AppendLine($"PATH: {file.FullPath}");
                sb.AppendLine("==========================================");
                sb.AppendLine(file.Content);
                sb.AppendLine(""); // Extra spacing between files
            }

            PreviewContent = sb.ToString();
            // Deselect specific file so the main text box shows the merge result
            SelectedFile = null;
        }

        private void ExportMergedContent()
        {
            if (string.IsNullOrEmpty(PreviewContent))
            {
                MessageBox.Show("Nothing to export. Please merge files first.");
                return;
            }

            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Text file (*.txt)|*.txt|All files (*.*)|*.*";
            if (saveFileDialog.ShowDialog() == true)
            {
                File.WriteAllText(saveFileDialog.FileName, PreviewContent);
                MessageBox.Show("Export Successful!");
            }
        }
    }
}
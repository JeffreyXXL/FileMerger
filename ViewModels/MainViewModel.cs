using FileMerger.Models;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace FileMerger.ViewModels
{
    public class MainViewModel : ObservableObject
    {
        private string _selectedFolderPath;
        private string _previewContent;
        private string _fullMergedContent; // Holds the complete text for export
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
                if (value != null)
                {
                    // Truncate individual file preview if extremely large
                    if (value.Content.Length > 20000)
                        PreviewContent = value.Content.Substring(0, 20000) + "\r\n... [Preview Truncated] ...";
                    else
                        PreviewContent = value.Content;
                }
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
            MergeCommand = new RelayCommand(async (o) => await MergeFilesAsync());
            ExportCommand = new RelayCommand((o) => ExportMergedContent());
            ToggleGroupCommand = new RelayCommand(ToggleGroup);
        }

        private async Task ScanFolderAsync()
        {
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
            if (parameter is CheckBox checkBox && checkBox.Tag is string extension)
            {
                var filesInGroup = Files.Where(f => f.Extension == extension).ToList();
                if (!filesInGroup.Any()) return;

                bool allSelected = filesInGroup.All(f => f.IsSelected);
                bool targetState = !allSelected;

                checkBox.IsChecked = targetState;

                foreach (var file in filesInGroup)
                {
                    file.IsSelected = targetState;
                }
            }
        }

        // Optimized Async Merge to prevent UI freeze
        private async Task MergeFilesAsync()
        {
            var selectedFiles = Files.Where(f => f.IsSelected).ToList();
            if (selectedFiles.Count == 0) return;

            IsBusy = true;
            SelectedFile = null; // Clear individual file selection

            await Task.Run(() =>
            {
                var sb = new StringBuilder();
                sb.AppendLine($"--- Merged {selectedFiles.Count} Files ---");
                sb.AppendLine($"Generated on: {DateTime.Now}");
                sb.AppendLine("------------------------------------------\r\n");

                foreach (var file in selectedFiles)
                {
                    sb.AppendLine("==========================================");
                    sb.AppendLine($"FILE: {file.Name}");
                    sb.AppendLine($"PATH: {file.FullPath}");
                    sb.AppendLine("==========================================");
                    sb.AppendLine(file.Content);
                    sb.AppendLine("\r\n");
                }

                _fullMergedContent = sb.ToString();
            });

            // On UI Thread: Update Preview with truncated content
            if (_fullMergedContent.Length > 20000)
            {
                PreviewContent = _fullMergedContent.Substring(0, 20000) + "\r\n\r\n... [Preview Truncated for Performance - Full content will be exported] ...";
            }
            else
            {
                PreviewContent = _fullMergedContent;
            }

            IsBusy = false;
        }

        private void ExportMergedContent()
        {
            if (string.IsNullOrEmpty(_fullMergedContent))
            {
                MessageBox.Show("Nothing to export. Please merge files first.");
                return;
            }

            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Text file (*.txt)|*.txt|All files (*.*)|*.*";
            if (saveFileDialog.ShowDialog() == true)
            {
                // Write the full content, not the truncated preview
                File.WriteAllText(saveFileDialog.FileName, _fullMergedContent);
                MessageBox.Show("Export Successful!");
            }
        }
    }
}
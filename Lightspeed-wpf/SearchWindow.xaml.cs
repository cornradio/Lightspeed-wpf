using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Lightspeed_wpf
{
    public partial class SearchWindow : Window
    {
        private const string basePath = @"C:\lightspeed";

        private class SearchResultItem
        {
            public ImageSource? Icon { get; set; }
            public string Name { get; set; } = "";
            public string FullPath { get; set; } = "";
            public string FolderTag { get; set; } = "";
            public bool IsDirectory { get; set; }
        }

        public SearchWindow()
        {
            InitializeComponent();
        }

        public void ShowSearch()
        {
            SearchTextBox.Text = "";
            SearchPlaceholder.Visibility = Visibility.Visible;
            SearchResultsList.Items.Clear();
            ShowRecentFiles();
            Show();
            Activate();
            SearchTextBox.Focus();
        }

        private void HideSearch()
        {
            Hide();
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            Hide();
        }

        private void SearchBorder_MouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true; // 阻止点击穿透到背景
        }

        private void ShowRecentFiles()
        {
            SearchResultsList.Items.Clear();
            var recent = AppSettings.Instance.RecentFiles;
            int count = 0;

            foreach (string path in recent)
            {
                if (count >= 20) break;
                if (!File.Exists(path) && !Directory.Exists(path)) continue;

                bool isDir = Directory.Exists(path);
                string name = Path.GetFileName(path);
                string displayName = name;
                if (!isDir && AppSettings.Instance.HideExtensions && name.Contains('.'))
                {
                    int dotIndex = name.LastIndexOf('.');
                    displayName = name.Substring(0, dotIndex);
                }

                int? parentFolder = GetParentFolderNum(path);
                string alias = parentFolder.HasValue
                    ? (AppSettings.Instance.FolderAliases.TryGetValue(parentFolder.Value.ToString(), out var a) ? a : $"[{parentFolder}]")
                    : "";
                string tag = parentFolder.HasValue ? $"[{parentFolder}] {alias}" : "";

                SearchResultsList.Items.Add(new SearchResultItem
                {
                    Icon = IconHelper.GetIcon(path, isDir, 28),
                    Name = displayName,
                    FullPath = path,
                    FolderTag = tag,
                    IsDirectory = isDir
                });
                count++;
            }

            if (SearchResultsList.Items.Count > 0)
                SearchResultsList.SelectedIndex = 0;
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = SearchTextBox.Text.Trim();
            SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchTextBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;

            SearchResultsList.Items.Clear();

            if (string.IsNullOrEmpty(query))
            {
                ShowRecentFiles();
                return;
            }

            var recentSet = new HashSet<string>(AppSettings.Instance.RecentFiles, StringComparer.OrdinalIgnoreCase);
            var recentResults = new List<SearchResultItem>();
            var otherResults = new List<SearchResultItem>();

            for (int i = 0; i <= 9; i++)
            {
                string folderPath = Path.Combine(basePath, i.ToString());
                if (!Directory.Exists(folderPath)) continue;

                string alias = AppSettings.Instance.FolderAliases.TryGetValue(i.ToString(), out var a) ? a : $"[{i}]";
                string tag = $"[{i}] {alias}";

                try
                {
                    foreach (string dir in Directory.GetDirectories(folderPath))
                    {
                        string name = Path.GetFileName(dir);
                        if (!PinyinHelper.Matches(name, query))
                            continue;

                        var item = new SearchResultItem
                        {
                            Icon = IconHelper.GetIcon(dir, true, 28),
                            Name = name,
                            FullPath = dir,
                            FolderTag = tag,
                            IsDirectory = true
                        };
                        if (recentSet.Contains(dir))
                            recentResults.Add(item);
                        else
                            otherResults.Add(item);
                    }

                    foreach (string file in Directory.GetFiles(folderPath))
                    {
                        string name = Path.GetFileName(file);
                        string displayName = name;
                        if (AppSettings.Instance.HideExtensions && name.Contains('.'))
                        {
                            int dotIndex = name.LastIndexOf('.');
                            displayName = name.Substring(0, dotIndex);
                        }

                        if (!PinyinHelper.Matches(displayName, query) && !PinyinHelper.Matches(name, query))
                            continue;

                        if (AppSettings.Instance.HideDesktopIni && name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase))
                            continue;

                        var item = new SearchResultItem
                        {
                            Icon = IconHelper.GetIcon(file, false, 28),
                            Name = displayName,
                            FullPath = file,
                            FolderTag = tag,
                            IsDirectory = false
                        };
                        if (recentSet.Contains(file))
                            recentResults.Add(item);
                        else
                            otherResults.Add(item);
                    }
                }
                catch { }
            }

            var orderedRecent = recentResults
                .OrderBy(r => AppSettings.Instance.RecentFiles.IndexOf(r.FullPath))
                .ToList();

            foreach (var item in orderedRecent)
                SearchResultsList.Items.Add(item);
            foreach (var item in otherResults)
                SearchResultsList.Items.Add(item);

            if (SearchResultsList.Items.Count > 0)
            {
                SearchResultsList.SelectedIndex = 0;
            }
        }

        private void SearchTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                HideSearch();
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                if (SearchResultsList.Items.Count > 0)
                {
                    SearchResultsList.Focus();
                    if (SearchResultsList.SelectedIndex < 0)
                        SearchResultsList.SelectedIndex = 0;
                    else if (SearchResultsList.SelectedIndex < SearchResultsList.Items.Count - 1)
                        SearchResultsList.SelectedIndex++;
                    SearchResultsList.ScrollIntoView(SearchResultsList.SelectedItem);
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                OpenSearchResult();
                e.Handled = true;
            }
        }

        private void SearchResultsList_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                HideSearch();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                OpenSearchResult();
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                if (SearchResultsList.SelectedIndex > 0)
                    SearchResultsList.SelectedIndex--;
                else
                    SearchTextBox.Focus();
                if (SearchResultsList.SelectedItem != null)
                    SearchResultsList.ScrollIntoView(SearchResultsList.SelectedItem);
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                if (SearchResultsList.SelectedIndex < SearchResultsList.Items.Count - 1)
                    SearchResultsList.SelectedIndex++;
                if (SearchResultsList.SelectedItem != null)
                    SearchResultsList.ScrollIntoView(SearchResultsList.SelectedItem);
                e.Handled = true;
            }
        }

        private void SearchResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            OpenSearchResult();
        }

        private void OpenSearchResult()
        {
            if (SearchResultsList.SelectedItem is SearchResultItem item)
            {
                HideSearch();
                var main = System.Windows.Application.Current.MainWindow as MainWindow;
                main?.OpenSearchResultPath(item.FullPath);
            }
        }

        private int? GetParentFolderNum(string path)
        {
            string? current = Path.GetDirectoryName(path);
            while (current != null && current != basePath)
            {
                string name = Path.GetFileName(current);
                if (int.TryParse(name, out int num) && num >= 0 && num <= 9)
                    return num;
                current = Path.GetDirectoryName(current);
            }
            return null;
        }
    }
}

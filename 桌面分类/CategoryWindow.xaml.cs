using System;
using System.Collections.Generic;
using System.Diagnostics; // 用于 Process.Start
using System.IO;          // 用于文件操作

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

// --- 消除歧义的别名定义 ---
using Button = System.Windows.Controls.Button;
using Image = System.Windows.Controls.Image;
using Cursors = System.Windows.Input.Cursors;
using DragEventArgs = System.Windows.DragEventArgs;
using DataFormats = System.Windows.DataFormats;
using Application = System.Windows.Application;
using Brushes = System.Windows.Media.Brushes;
using MessageBox = System.Windows.MessageBox;
using DataObject = System.Windows.DataObject;
using DragDropEffects = System.Windows.DragDropEffects;
// ------------------------------------

namespace DesktopOrganizer {
    /// <summary>
    /// CategoryWindow.xaml 的交互逻辑
    /// </summary>
    public partial class CategoryWindow : Window {
        // 当前窗口绑定的数据模型
        public CategoryData Data {
            get; private set;
        }

        // 文件系统监控器
        private FileSystemWatcher _watcher;

        // 构造函数
        public CategoryWindow(CategoryData data = null) {
            InitializeComponent();

            // 1. 数据初始化
            if (data == null) {
                Data = new CategoryData(); // 新建窗口
            }
            else {
                Data = data; // 加载已有配置
            }

            this.DataContext = Data;

            // 2. 恢复窗口位置和大小
            this.Left = Data.X > 0 ? Data.X : 100;
            this.Top = Data.Y > 0 ? Data.Y : 100;
            if (Data.Width > 0)
                this.Width = Data.Width;
            if (Data.Height > 0)
                this.Height = Data.Height;

            // 如果你的 XAML 中有 TitleBox 用于显示标题，请解开下行注释
            TitleBox.Text = Data.Title; 
            

            // 3. 加载图标内容
            LoadIcons();

            // 4. 初始化文件监控
            InitializeWatcher();

            // 5. 绑定窗口状态变更事件（用于自动保存配置）
            this.LocationChanged += (s, e) => { Data.X = this.Left; Data.Y = this.Top; };
            this.SizeChanged += (s, e) => { Data.Width = this.Width; Data.Height = this.Height; };

            // 6. 启用拖放功能
            this.AllowDrop = true;
            this.Drop += Window_Drop;
        }

        // --- 核心功能 1: 加载与显示图标 ---

        /// <summary>
        /// 读取 Data.FileNames 并在界面上重新生成图标
        /// </summary>
        public void LoadIcons() {
            // 清空现有图标
            IconPanel.Children.Clear();

            // 创建一个临时列表用于校验文件是否存在
            List<string> validFiles = new List<string>();

            foreach (var fileName in Data.FileNames) {
                string fullPath = Path.Combine(StateManager.StoragePath, fileName);

                // 只有文件实际存在时才显示
                if (File.Exists(fullPath)) {
                    AddIconControl(fullPath);
                    validFiles.Add(fileName);
                }
            }

            // 更新内存数据，移除不存在的文件记录
            Data.FileNames = validFiles;
        }

        /// <summary>
        /// 创建单个图标控件 (图标 + 文字 + 事件绑定)
        /// </summary>
        private void AddIconControl(string filePath) {
            string fileName = Path.GetFileName(filePath);

            // A. 垂直布局容器 (图标在上，文字在下)
            StackPanel stack = new StackPanel {
                Width = 60,
                Margin = new Thickness(2)
            };

            // B. 图标图片 (使用 IconHelper 提取系统图标)
            Image img = new Image {
                Width = 32,
                Height = 32,
                Margin = new Thickness(0, 5, 0, 2),
                Source = IconHelper.GetIcon(filePath)
            };

            // C. 文件名文本
            TextBlock txt = new TextBlock {
                Text = fileName,
                TextTrimming = TextTrimming.CharacterEllipsis, // 长文本自动省略
                TextAlignment = TextAlignment.Center,
                Foreground = Brushes.White,
                FontSize = 11,
                Height = 32,
                TextWrapping = TextWrapping.Wrap
            };

            stack.Children.Add(img);
            stack.Children.Add(txt);

            // D. 按钮外壳 (透明背景)
            Button btn = new Button {
                Content = stack,
                Width = 70,
                Height = 85,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Tag = filePath, // 【关键】将完整路径存储在 Tag 中
                Cursor = Cursors.Hand
            };

            // E. 事件绑定：双击运行
            btn.MouseDoubleClick += (s, e) => {
                try {
                    ProcessStartInfo psi = new ProcessStartInfo(filePath) { UseShellExecute = true };
                    Process.Start(psi);
                }
                catch (Exception ex) {
                    MessageBox.Show($"无法打开文件：{ex.Message}");
                }
            };

            // F. 事件绑定：鼠标按下准备拖拽 (实现跨窗口移动)
            btn.PreviewMouseLeftButtonDown += (s, e) => {
                // 获取当前点击的 Button
                if (s is Button sourceBtn) {
                    // 启动拖拽操作，携带文件路径数据
                    DragDrop.DoDragDrop(sourceBtn, new DataObject(DataFormats.FileDrop, new string[] { filePath }), DragDropEffects.Move);
                }
            };

            IconPanel.Children.Add(btn);
        }

        // --- 核心功能 2: 拖放逻辑 (接收文件) ---

        private void Window_Drop(object sender, DragEventArgs e) {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

                bool needRefresh = false;

                foreach (var file in files) {
                    string fName = Path.GetFileName(file);

                    // 场景 A: 跨窗口移动 (文件已经在 Storage 目录中)
                    if (file.StartsWith(StateManager.StoragePath, StringComparison.OrdinalIgnoreCase)) {
                        // 1. 查找这个文件目前属于哪个窗口
                        var mainWindow = Application.Current.MainWindow as MainWindow;
                        if (mainWindow != null) {
                            foreach (var win in mainWindow.OpenCategoryWindows) {
                                // 如果找到了原来的宿主，且不是当前窗口
                                if (win != this && win.Data.FileNames.Contains(fName)) {
                                    // 从原窗口移除
                                    win.Data.FileNames.Remove(fName);
                                    win.LoadIcons(); // 刷新原窗口 UI
                                    break;
                                }
                            }
                        }

                        // 2. 添加到当前窗口
                        if (!Data.FileNames.Contains(fName)) {
                            Data.FileNames.Add(fName);
                            needRefresh = true;
                        }
                    }
                    // 场景 B: 从桌面/资源管理器导入 (新文件)
                    else {
                        string destPath = Path.Combine(StateManager.StoragePath, fName);

                        // 移动物理文件 (处理重名)
                        string finalPath = StateManager.TryMoveFile(file, destPath);

                        if (finalPath != null) {
                            Data.FileNames.Add(Path.GetFileName(finalPath));
                            needRefresh = true;
                        }
                    }
                }

                if (needRefresh) {
                    LoadIcons();
                }
            }
        }

        // --- 核心功能 3: 文件监控 (自动同步外部删除/重命名) ---

        private void InitializeWatcher() {
            if (!Directory.Exists(StateManager.StoragePath))
                return;

            _watcher = new FileSystemWatcher(StateManager.StoragePath);
            _watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite;
            _watcher.EnableRaisingEvents = true;

            // 绑定事件
            _watcher.Deleted += OnFileChanged;
            _watcher.Renamed += OnFileRenamed;
        }

        // 处理文件删除
        private void OnFileChanged(object sender, FileSystemEventArgs e) {
            // FileSystemWatcher 在后台线程运行，必须 Invoke 到 UI 线程
            Dispatcher.Invoke(() => {
                if (Data.FileNames.Contains(e.Name)) {
                    Data.FileNames.Remove(e.Name);
                    LoadIcons(); // 刷新界面，移除消失的图标
                }
            });
        }

        // 处理文件重命名
        private void OnFileRenamed(object sender, RenamedEventArgs e) {
            Dispatcher.Invoke(() => {
                if (Data.FileNames.Contains(e.OldName)) {
                    // 更新列表中的文件名
                    Data.FileNames.Remove(e.OldName);
                    Data.FileNames.Add(e.Name);
                    LoadIcons(); // 刷新界面，更新文件名和可能变化的图标
                }
            });
        }

        // --- 窗口基础交互逻辑 ---

        // 窗口拖动
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            try {
                this.DragMove();
            }
            catch { }
        }

        // 菜单：点击三点按钮
        private void Menu_Click(object sender, RoutedEventArgs e) {
            // 假设 XAML 中有名为 CatMenu 的 Popup
            if (this.FindName("CatMenu") is System.Windows.Controls.Primitives.Popup menu) {
                menu.IsOpen = true;
            }
        }

        // 菜单：新建窗口
        private void New_Click(object sender, RoutedEventArgs e) {
            // 调用主窗口的方法来创建新分类
            if (Application.Current.MainWindow is MainWindow mw) {
                mw.CreateNewCategory();
            }
        }

        // 菜单：删除本窗口
        private void Delete_Click(object sender, RoutedEventArgs e) {
            // 1. 将文件归还到桌面
            StateManager.RestoreFilesToDesktop(Data.FileNames);

            // 2. 通知主窗口移除引用
            if (Application.Current.MainWindow is MainWindow mw) {
                mw.RemoveCategoryWindow(this);
            }

            // 3. 关闭自身
            this.Close();
        }
    }
}
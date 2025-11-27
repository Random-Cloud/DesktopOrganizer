using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms; // 用于 NotifyIcon
using System.Windows.Input;
using System.Windows.Resources;
using Application = System.Windows.Application; // 消除歧义

namespace DesktopOrganizer {
    public partial class MainWindow : Window {
        // 运行时维护所有打开的分类窗口
        public List<CategoryWindow> OpenCategoryWindows { get; set; } = new List<CategoryWindow>();
        private Dictionary<DateTime, string> _notes = new Dictionary<DateTime, string>();
        private DateTime _currentMonth = DateTime.Today;

        private NotifyIcon _notifyIcon;
        private bool _isExit = false; // 标记是否真正退出

        public MainWindow() {
            InitializeComponent();
            InitializeTrayIcon();

            // 1. 初始化环境
            StateManager.Initialize();
            StateManager.LoadConfig();

            // 新增功能：删除过期备忘录
            try {
                var allNotes = StateManager.CurrentConfig.CalendarNotes;
                List<string> expiredKeys = new List<string>();
                DateTime currentMonth = DateTime.Today;

                // 1. 遍历检查所有记录
                foreach (var key in allNotes.Keys) {
                    // 尝试解析日期 (Key 格式通常为 "yyyy-MM-dd")
                    if (DateTime.TryParse(key, out DateTime noteDate)) {
                        // 如果 年份不同 或 月份不同，则标记为过期
                        if (noteDate.Year != currentMonth.Year || noteDate.Month != currentMonth.Month) {
                            expiredKeys.Add(key);
                        }
                    }
                }

                // 2. 执行删除并保存
                if (expiredKeys.Count > 0) {
                    foreach (var key in expiredKeys) {
                        allNotes.Remove(key);
                    }
                    // 立即保存清理后的结果到硬盘
                    StateManager.SaveConfig();

                }
            }
            catch (Exception ex) {
                // 防止清理逻辑出错影响软件启动，仅记录错误或忽略
                System.Diagnostics.Debug.WriteLine("清理过期记录失败: " + ex.Message);
            }

            // 2. 恢复主窗口状态
            this.Left = StateManager.CurrentConfig.MainX;
            this.Top = StateManager.CurrentConfig.MainY;
            // 恢复日历记录 (转换 Dictionary Key 类型)
            foreach (var kvp in StateManager.CurrentConfig.CalendarNotes) {
                if (DateTime.TryParse(kvp.Key, out DateTime date)) {
                    // _notes 是 MainWindow 原有的字典变量
                    _notes[date] = kvp.Value;
                }
            }
            RefreshCalendar();

            // 3. 恢复分类窗口
            RestoreCategories();

            // 4. 初始化标题日期
            if (MainTitleText != null) {
                MainTitleText.Text = DateTime.Now.ToString("yyyy-MM-dd");
            }

            // 监听位置变化
            this.LocationChanged += (s, e) => {
                StateManager.CurrentConfig.MainX = this.Left;
                StateManager.CurrentConfig.MainY = this.Top;
            };

            // 监听程序退出
            Application.Current.Exit += OnAppExit;
        }
        private void InitializeTrayIcon() {
            _notifyIcon = new NotifyIcon();
            try {
                // 获取资源流 (URI 格式: pack://application:,,,/项目名;component/文件名)
                StreamResourceInfo iconInfo = Application.GetResourceStream(new Uri("pack://application:,,,/DesktopOrganizer;component/AppIcon.ico"));

                if (iconInfo != null) {
                    // 将 WPF 资源流转换为 WinForms 图标
                    _notifyIcon.Icon = new System.Drawing.Icon(iconInfo.Stream);
                }
                else {
                    // 如果没找到，兜底使用系统图标
                    _notifyIcon.Icon = SystemIcons.Application;
                }
            }
            catch {
                _notifyIcon.Icon = SystemIcons.Application; // 出错时使用默认图标
            }
            _notifyIcon.Visible = true;
            _notifyIcon.Text = "桌面整理工具";

            // 双击托盘显示主窗口
            _notifyIcon.DoubleClick += (s, e) => {
                this.Show();                        // 显示窗口
                this.WindowState = WindowState.Normal; // 【关键】如果是最小化状态，则恢复正常
                this.Activate();                    // 激活窗口（置顶）
            };

            // 托盘右键菜单
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("显示主界面", null, (s, e) => {
                this.Show();
                this.WindowState = WindowState.Normal;
                this.Activate();
            });
            menu.Items.Add("退出程序", null, (s, e) => {
                _isExit = true; // 标记为允许退出

                // 退出前清理托盘图标，防止产生“幽灵图标”
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();

                Application.Current.Shutdown();
            });
            _notifyIcon.ContextMenuStrip = menu;
        }
        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            if (!_isExit) {
                e.Cancel = true; // 取消关闭
                this.Hide();     // 隐藏窗口
                // 如果分类窗口也需要隐藏，可以在这里遍历 OpenCategoryWindows 并 Hide()
            }
            else {
                _notifyIcon.Dispose(); // 清理托盘图标
                // ... (原有的 OnAppExit 逻辑会由 App.Exit 触发) ...
            }
        }

        private void ShowMainWindow() {
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
        }

        private void RestoreCategories() {
            foreach (var catData in StateManager.CurrentConfig.Categories) {
                // 核心：把文件从桌面“吸”回来
                StateManager.ImportFilesForCategory(catData);

                // 创建窗口
                CategoryWindow win = new CategoryWindow(catData);
                win.Show();
                OpenCategoryWindows.Add(win);
            }
        }

        // 新建窗口逻辑
        public void CreateNewCategory() {
            int count = StateManager.CurrentConfig.Categories.Count + 1;
            CategoryData newData = new CategoryData {
                Title = $"分类窗口 {count}",  // 【关键修改】设置默认名称

                // 建议设置一下初始位置，避免和主窗口完全重叠
                X = this.Left + this.Width + 20,
                Y = this.Top
            };
            StateManager.CurrentConfig.Categories.Add(newData);
            CategoryWindow win = new CategoryWindow(newData); // 创建新数据
            
            win.Show();
            OpenCategoryWindows.Add(win);
            // 保存配置
            StateManager.SaveConfig();
        }

        // 删除窗口逻辑（供 CategoryWindow 调用）
        public void RemoveCategoryWindow(CategoryWindow win) {
            if (OpenCategoryWindows.Contains(win))
                OpenCategoryWindows.Remove(win);
        }

        // --- 程序退出时的处理 ---
        private void OnAppExit(object sender, ExitEventArgs e) {
            // 1. 准备保存的数据
            var config = StateManager.CurrentConfig;
            config.Categories.Clear(); // 清空旧列表，重新从当前打开的窗口获取

            // 更新日历数据
            config.CalendarNotes.Clear();
            foreach (var kvp in _notes) {
                config.CalendarNotes[kvp.Key.ToString("yyyy-MM-dd")] = kvp.Value;
            }

            // 2. 遍历所有分类窗口
            foreach (var win in OpenCategoryWindows) {
                // 将文件归还桌面
                StateManager.RestoreFilesToDesktop(win.Data.FileNames);

                // 添加到保存列表（虽然文件还回去了，但我们保留配置，
                // 这样下次启动时，程序知道该去桌面抓哪些文件）
                config.Categories.Add(win.Data);
            }

            // 3. 写入磁盘
            StateManager.SaveConfig();
        }

        // --- 刷新日历视图的方法 ---
        private void RefreshCalendar() {
            // 1. 清空现有的网格内容 (UI)
            CalendarGrid.Children.Clear();

            // 2. 计算日历逻辑
            // 获取当月第一天
            DateTime firstDayOfMonth = new DateTime(_currentMonth.Year, _currentMonth.Month, 1);
            // 获取当月总天数
            int daysInMonth = DateTime.DaysInMonth(_currentMonth.Year, _currentMonth.Month);
            // 获取当月第一天是星期几 (用于计算前面需要空几格)
            // DayOfWeek.Sunday = 0, Monday = 1...
            int startDayOfWeek = (int)firstDayOfMonth.DayOfWeek;

            // 3. 填充月初前的空白格
            for (int i = 0; i < startDayOfWeek; i++) {
                // 添加一个空的占位 Border
                CalendarGrid.Children.Add(new Border());
            }

            // 4. 循环生成每一天的格子
            for (int day = 1; day <= daysInMonth; day++) {
                // 当前生成的具体日期
                DateTime currentDate = new DateTime(_currentMonth.Year, _currentMonth.Month, day);

                // --- 创建单元格容器 ---
                Grid dayCell = new Grid();
                dayCell.Margin = new Thickness(2);

                // --- 创建日期数字文本 ---
                TextBlock txt = new TextBlock {
                    Text = day.ToString(),
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 14,
                    Foreground = System.Windows.Media.Brushes.White // 默认白色
                };

                // 如果是“今天”，高亮显示 (例如青色文字)
                if (currentDate == DateTime.Today) {
                    txt.Foreground = System.Windows.Media.Brushes.Cyan;
                    txt.FontWeight = FontWeights.Bold;
                }

                dayCell.Children.Add(txt);

                // --- 核心：检查是否有记录 (_notes) ---
                // 如果字典里包含这一天，就在下面画个小点
                if (_notes.ContainsKey(currentDate)) {
                    System.Windows.Shapes.Ellipse dot = new System.Windows.Shapes.Ellipse {
                        Width = 4,
                        Height = 4,
                        Fill = System.Windows.Media.Brushes.Cyan, // 青色圆点
                        VerticalAlignment = VerticalAlignment.Bottom,
                        Margin = new Thickness(0, 0, 0, 4) // 距离底部一点距离
                    };
                    dayCell.Children.Add(dot);
                }

                // --- 创建交互逻辑 (右键菜单) ---
                ContextMenu menu = new ContextMenu();

                // 选项1: 查看/编辑
                /*
                MenuItem viewItem = new MenuItem { Header = "查看/编辑记录", Foreground = System.Windows.Media.Brushes.Black  };
                viewItem.Click += (s, e) => ViewRecord(currentDate); // 调用 ViewRecord 方法
                */
                MenuItem viewItem = new MenuItem();
                viewItem.Header = new TextBlock { Text = "编辑记录", Foreground = System.Windows.Media.Brushes.Black };
                viewItem.Click += (s, e) => ViewRecord(currentDate);
                // 选项2: 删除
                /*
                MenuItem delItem = new MenuItem { Header = "删除记录", Foreground = System.Windows.Media.Brushes.Black };
                delItem.Click += (s, e) => DeleteRecord(currentDate); // 调用 DeleteRecord 方法 (见下方)
                */
                MenuItem delItem = new MenuItem();
                delItem.Header = new TextBlock { Text = "删除记录", Foreground = System.Windows.Media.Brushes.Black };
                delItem.Click += (s, e) => DeleteRecord(currentDate);
                menu.Items.Add(viewItem);
                menu.Items.Add(delItem);

                // --- 包装外层 Border (用于鼠标悬停效果) ---
                Border cellBorder = new Border {
                    Child = dayCell,
                    Background = System.Windows.Media.Brushes.Transparent,
                    CornerRadius = new CornerRadius(5),
                    ContextMenu = menu, // 绑定右键菜单
                    Tag = currentDate   // 将日期存入 Tag 备用
                };

                // 鼠标悬停变色效果
                cellBorder.MouseEnter += (s, e) =>
                    cellBorder.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(50, 255, 255, 255));

                cellBorder.MouseLeave += (s, e) =>
                    cellBorder.Background = System.Windows.Media.Brushes.Transparent;

                // 鼠标悬停查看当日记录
                if (_notes.TryGetValue(currentDate, out string noteContent) && !string.IsNullOrWhiteSpace(noteContent)) {
                    System.Windows.Controls.ToolTip tt = new ();

                    // 【关键修改】内容包裹在 TextBlock 中并强制设为黑色
                    tt.Content = new TextBlock {
                        Text = noteContent,
                        Foreground = System.Windows.Media.Brushes.Black,
                        FontSize = 12
                    };

                    // 设置 ToolTip 背景样式（可选，保证对比度）
                    tt.Background = System.Windows.Media.Brushes.NavajoWhite;
                    tt.BorderBrush = System.Windows.Media.Brushes.Gray;
                    // 调整位置
                    tt.PlacementTarget = cellBorder;
                    tt.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
                    tt.HorizontalOffset = 25;

                    cellBorder.ToolTip = tt;
                }

                // --- 添加到网格 ---
                CalendarGrid.Children.Add(cellBorder);
            }
        }

        // 补充：DeleteRecord 的简单实现 (如果尚未定义)
        private void DeleteRecord(DateTime date) {
            if (_notes.ContainsKey(date)) {
                _notes.Remove(date); // 从字典移除
                RefreshCalendar();   // 刷新界面，小圆点会消失
            }
        }
        private void ViewRecord(DateTime date) {
            // 1. 获取当前日期的已有记录
            // 如果字典 _notes 中包含该日期，则取出内容；否则设为空字符串
            string currentNote = _notes.ContainsKey(date) ? _notes[date] : "";

            // 2. 实例化我们自定义的“淡黄稿纸”窗口 (NoteDialog)
            // 注意：需要确保您已经创建了 NoteDialog.xaml 和 NoteDialog.xaml.cs
            NoteDialog dialog = new NoteDialog(date, currentNote);

            // 3. 以“模态”方式显示窗口 (.ShowDialog)
            // 这会暂停当前代码的执行，直到用户关闭 NoteDialog 窗口
            // 如果用户点击了“保存”，DialogResult 会返回 true
            if (dialog.ShowDialog() == true) {
                // 4. 获取用户编辑后的文本
                string newText = dialog.NoteContent;

                // 5. 判断逻辑
                if (string.IsNullOrWhiteSpace(newText)) {
                    // 情况 A: 用户把字删光了 -> 视为“删除这条记录”
                    if (_notes.ContainsKey(date)) {
                        _notes.Remove(date);
                    }
                }
                else {
                    // 情况 B: 用户输入了新内容 -> 更新或添加记录
                    _notes[date] = newText;
                }

                // 6. 刷新日历界面
                // 这一步非常重要，它会重绘日历，根据 _notes 的变化显示或移除日期下方的小圆点
                RefreshCalendar();
            }
        }
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            // 只有当鼠标左键按下时才触发拖动
            if (e.ButtonState == MouseButtonState.Pressed) {
                try {
                    this.DragMove();
                }
                catch {
                    // 忽略极少数情况下的异常
                }
            }
        }
        // 菜单点击
        private void MenuButton_Click(object sender, RoutedEventArgs e) {
            // 假设您的 Popup 名字叫 SettingsPopup
            SettingsPopup.IsOpen = true;
        }

        // 新建窗口
        private void NewWindow_Click(object sender, RoutedEventArgs e) {
            CreateNewCategory(); // 调用您已实现的创建逻辑
            SettingsPopup.IsOpen = false; // 点击后关闭菜单
        }

        // 删除(隐藏)主窗口
        private void DeleteWindow_Click(object sender, RoutedEventArgs e) {
            // 主窗口不建议彻底删除，而是隐藏到托盘
            this.Hide();
            SettingsPopup.IsOpen = false;
        }

        // 清空所有记录
        private void ClearAllRecords_Click(object sender, RoutedEventArgs e) {
            if (System.Windows.MessageBox.Show("确定要清空所有日期的记录吗？", "警告", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes) {
                _notes.Clear();
                RefreshCalendar();
            }
            SettingsPopup.IsOpen = false;
        }

        // 开机自启相关
        private const string AppName = "DesktopOrganizer";
        private void AutoStart_Checked(object sender, RoutedEventArgs e) {
            try {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true)) {
                    key.SetValue(AppName, System.Reflection.Assembly.GetExecutingAssembly().Location + ".exe");
                }
            }
            catch { }
        }

        private void AutoStart_Unchecked(object sender, RoutedEventArgs e) {
            try {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true)) {
                    key.DeleteValue(AppName, false);
                }
            }
            catch { }
        }

        // 折叠
        private double _lastHeight = 320; // 记录折叠前的高度
        private bool _isFolded = false;   // 记录当前是否折叠
        private double _oldMinHeight = 0; // 记录折叠前的最小高度

        private void ToggleFold_Click(object sender, RoutedEventArgs e) {
            if (!_isFolded) {
                // --- 执行折叠 ---

                // 1. 记录当前高度以便恢复
                _lastHeight = this.Height;
                _oldMinHeight = this.MinHeight;
                this.MinHeight = 0;

                // 2. 隐藏内容行 (设置高度为 0)
                MainContentRow.Height = new GridLength(0);

                // 3. 将窗口高度设为标题栏高度 + 边框微调
                this.Height = 45;

                // 4. 更改按钮图标
                FoldBtn.Content = "▲";

                // 5. 锁定改变大小 (折叠时禁止调整大小)
                this.ResizeMode = ResizeMode.NoResize;

                _isFolded = true;
            }
            else {
                // --- 执行展开 ---

                // 1. 恢复内容行 (设置为 *)
                MainContentRow.Height = new GridLength(1, GridUnitType.Star);

                // 2. 恢复窗口高度
                this.Height = _lastHeight;

                // 3. 更改按钮图标
                FoldBtn.Content = "▼";

                // 4. 恢复调整大小功能
                this.ResizeMode = ResizeMode.CanResizeWithGrip;

                _isFolded = false;
            }
        }

    }
}
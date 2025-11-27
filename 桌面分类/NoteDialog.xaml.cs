using System;
using System.Windows;
using System.Windows.Input;

namespace DesktopOrganizer {
    /// <summary>
    /// NoteDialog.xaml 的交互逻辑
    /// </summary>
    public partial class NoteDialog : Window {
        // 公开属性：用于让主窗口(MainWindow)在窗口关闭后获取用户输入的文本
        public string NoteContent {
            get; private set;
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="date">当前选中的日期</param>
        /// <param name="existingContent">已存在的记录内容（如果是新建则为空）</param>
        public NoteDialog(DateTime date, string existingContent) {
            InitializeComponent();

            // 1. 设置顶部标题
            DateTitle.Text = date.ToString("yyyy年M月d日") + " - 备忘";

            // 2. 填充已有内容
            NoteTextBox.Text = existingContent;

            // 3. 窗口加载完成后，让输入框自动获得焦点，并将光标移到末尾
            this.Loaded += (s, e) => {
                NoteTextBox.Focus();
                NoteTextBox.Select(NoteTextBox.Text.Length, 0);
            };
        }

        /// <summary>
        /// 实现无边框窗口的拖动功能
        /// </summary>
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            // 如果鼠标按下的位置不是文本框等交互控件，则允许拖动
            // (在WPF中，只要不处理Handled，DragMove通常能直接工作，但建议加try-catch防止极个别情况报错)
            try {
                this.DragMove();
            }
            catch { }
        }

        /// <summary>
        /// [保存] 按钮点击事件
        /// </summary>
        private void Save_Click(object sender, RoutedEventArgs e) {
            // 将文本框的内容存入属性
            NoteContent = NoteTextBox.Text;

            // 设置对话框结果为 true (表示确认)
            this.DialogResult = true;

            // 关闭窗口
            this.Close();
        }

        /// <summary>
        /// [取消] 或 [关闭] 按钮点击事件
        /// </summary>
        private void Cancel_Click(object sender, RoutedEventArgs e) {
            // 设置对话框结果为 false (表示取消)
            this.DialogResult = false;

            this.Close();
        }
    }
}
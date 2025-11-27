using System;
using System.Threading; // 用于 Mutex (单例检查)
using System.Windows;

namespace DesktopOrganizer {
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : System.Windows.Application {
        // 用于检测应用程序是否已经在运行的互斥体
        private static Mutex _mutex = null;

        /// <summary>
        /// 程序启动时触发
        /// </summary>
        protected override void OnStartup(StartupEventArgs e) {
            // --- 1. 防止重复运行 (单例检查) ---
            const string appName = "DesktopOrganizer_Unique_Mutex_ID";
            bool createdNew;

            // 尝试创建一个命名的 Mutex
            _mutex = new Mutex(true, appName, out createdNew);

            if (!createdNew) {
                // 如果 createdNew 为 false，说明已经有一个实例在运行了
                System.Windows.MessageBox.Show("桌面整理工具已经在运行中！\n请检查任务栏托盘区域。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);

                // 退出当前尝试启动的新实例
                System.Windows.Application.Current.Shutdown();
                return;
            }

            // --- 2. 注册全局异常捕获事件 ---
            // 这对于文件操作类软件非常重要，防止因某个文件被占用导致整个程序闪退
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;

            // --- 3. 继续正常的启动流程 ---
            // 这会加载 App.xaml 中定义的 StartupUri (即 MainWindow.xaml)
            base.OnStartup(e);
        }

        /// <summary>
        /// 处理未捕获的 UI 线程异常
        /// </summary>
        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e) {
            // 标记异常已处理，防止程序崩溃退出
            e.Handled = true;

            // 提示用户错误信息
            string errorMsg = $"发生了一个未预期的错误：\n{e.Exception.Message}\n\n建议保存工作并重启应用。";
            System.Windows.MessageBox.Show(errorMsg, "错误", MessageBoxButton.OK, MessageBoxImage.Error);

            // 在这里，你也可以尝试紧急保存配置
            // try { StateManager.SaveConfig(); } catch { }
        }
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace DesktopOrganizer {
    public static class StateManager {
        // 路径定义
        private static string AppDataPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DesktopOrganizer");
        private static string ConfigPath => Path.Combine(AppDataPath, "config.json");
        public static string StoragePath => Path.Combine(AppDataPath, "Storage"); // 存放被整理图标的文件夹
        private static string DesktopPath => Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

        // 全局引用
        public static AppConfig CurrentConfig { get; set; } = new AppConfig();

        // 初始化环境
        public static void Initialize() {
            if (!Directory.Exists(AppDataPath))
                Directory.CreateDirectory(AppDataPath);
            if (!Directory.Exists(StoragePath))
                Directory.CreateDirectory(StoragePath);
        }

        // 加载配置
        public static void LoadConfig() {
            if (File.Exists(ConfigPath)) {
                try {
                    string json = File.ReadAllText(ConfigPath);
                    CurrentConfig = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
                }
                catch {
                    CurrentConfig = new AppConfig();
                }
            }
        }

        // 保存配置
        public static void SaveConfig() {
            string json = JsonSerializer.Serialize(CurrentConfig, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }

        // --- 核心：文件移动逻辑 ---

        /// <summary>
        /// [启动时] 将文件从桌面“吸入”到应用存储，准备显示
        /// </summary>
        public static void ImportFilesForCategory(CategoryData catData) {
            foreach (var fileName in catData.FileNames) {
                string desktopFile = Path.Combine(DesktopPath, fileName);
                string storageFile = Path.Combine(StoragePath, fileName);

                // 如果文件在桌面上，移动进来
                if (File.Exists(desktopFile)) {
                    TryMoveFile(desktopFile, storageFile);
                }
                // 如果文件已经在存储里（可能上次非正常退出），则不做操作，直接准备显示
            }
        }

        /// <summary>
        /// [退出时] 将文件从应用存储“归还”到桌面
        /// </summary>
        public static void RestoreFilesToDesktop(List<string> fileNames) {
            foreach (var fileName in fileNames) {
                string storageFile = Path.Combine(StoragePath, fileName);
                string desktopFile = Path.Combine(DesktopPath, fileName);

                if (File.Exists(storageFile)) {
                    // 只有当文件确实在存储区时才移动
                    TryMoveFile(storageFile, desktopFile);
                }
            }
        }

        // 辅助：安全的移动文件（处理重名）
        public static string TryMoveFile(string source, string dest) {
            try {
                if (!File.Exists(source))
                    return null;

                // 目标路径查重
                string dir = Path.GetDirectoryName(dest);
                string name = Path.GetFileNameWithoutExtension(dest);
                string ext = Path.GetExtension(dest);
                int counter = 1;

                while (File.Exists(dest)) {
                    dest = Path.Combine(dir, $"{name} ({counter}){ext}");
                    counter++;
                }

                File.Move(source, dest);
                return dest;
            }
            catch { return null; }
        }
    }
}
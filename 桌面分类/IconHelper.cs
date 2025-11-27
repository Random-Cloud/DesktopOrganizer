using System;
using System.Drawing; 
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DesktopOrganizer {
    public static class IconHelper {
        public static ImageSource GetIcon(string fileName) {
            try {
                // 使用 System.Drawing.Icon 提取关联图标
                using (Icon sysIcon = Icon.ExtractAssociatedIcon(fileName)) {
                    if (sysIcon == null)
                        return null;

                    // 转换为 WPF 的 ImageSource
                    return Imaging.CreateBitmapSourceFromHIcon(
                        sysIcon.Handle,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                }
            }
            catch {
                return null; // 提取失败返回空
            }
        }
    }
}
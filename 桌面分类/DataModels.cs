using System;
using System.Collections.Generic;

namespace DesktopOrganizer {
    // 根配置
    public class AppConfig {
        public double MainX { get; set; } = 100;
        public double MainY { get; set; } = 100;
        public bool AutoStart {
            get; set;
        }

        // 日历记录 (Key: DateTime的String形式, Value: 内容)
        public Dictionary<string, string> CalendarNotes { get; set; } = new Dictionary<string, string>();

        // 分类窗口列表
        public List<CategoryData> Categories { get; set; } = new List<CategoryData>();
    }

    // 单个分类窗口的数据
    public class CategoryData {
        public string Id { get; set; } = Guid.NewGuid().ToString(); // 唯一标识
        public string Title { get; set; } = "新建分类";
        public double X {
            get; set;
        }
        public double Y {
            get; set;
        }
        public double Width {
            get; set;
        }
        public double Height {
            get; set;
        }

        // 该分类下包含的文件名列表
        public List<string> FileNames { get; set; } = new List<string>();
    }
}
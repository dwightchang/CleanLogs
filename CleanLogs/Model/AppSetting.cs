using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CleanLogs.Model
{
    public class AppSetting
    {
        public bool WriteLog { get; set; }

        /// <summary>
        /// 搬到archive目錄
        /// </summary>
        public string ArchivePath { get; set; }

        public List<LogFolder> LogFolder { get; set; }
    }
}
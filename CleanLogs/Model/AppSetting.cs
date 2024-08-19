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
        public bool DeleteEmptyFolder { get; set; } = false;
        public List<LogFolder> LogFolder { get; set; }
    }
}

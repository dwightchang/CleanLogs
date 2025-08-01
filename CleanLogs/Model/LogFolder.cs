﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CleanLogs.Model
{
    public class LogFolder
    {
        public string LogPath { get; set; }
        /// <summary>
        /// 超過N天後刪除檔案
        /// </summary>
        public int PreservedDays { get; set; }
        /// <summary>
        /// 超過N天後壓縮檔案
        /// </summary>
        public int ZipDays { get; set; }
        public List<string> FileExtNameInclude { get; set; }   
        public bool DeleteEmptyFolder { get; set; }
    }
}

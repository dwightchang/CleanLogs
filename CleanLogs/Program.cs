using CleanLogs.Model;
using Ionic.Zip;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SevenZip;

namespace CleanLogs
{
    class Program
    {
        public static bool _writeLog = true;
        public static string _baseDir = "";
        public static string[] _zipFileExtensions = new string[] { ".zip", ".rar", ".7z" };

        /// <summary>
        /// 內定不處理的資料夾
        /// </summary>
        public static string[] _excludeFolders = new string[] { ".svn", ".git" };
        
        private static string _hostIp = "";


        static void Main(string[] args)
        {
            try
            {
                _hostIp = GetHostIp();
                
                SevenZipBase.SetLibraryPath(
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lib\\7z.dll"));
                
                _baseDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
                string path = _baseDir + "\\appSetting.json";

                if (args.Length > 1)
                {
                    path = args[1];
                }

                string strAppSetting = File.ReadAllText(path);

                var config = JsonConvert.DeserializeObject<AppSetting>(strAppSetting);
                _writeLog = config.WriteLog;

                CleanLogFolder(config);
            }
            catch (Exception ex)
            {
                writeLog(ex.ToString(), true);
            }
        }

        static string GetHostIp()
        {
            string hostName = Dns.GetHostName();
            var addresses = Dns.GetHostAddresses(hostName)
                .Where(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

            return addresses.First().ToString();
        }


        /// <summary>
        /// 刪除超過期限的檔案,檔案日期=建立及上次寫入時間較大者
        /// </summary>
        static void CleanLogFolder(AppSetting config)
        {
            try
            {
                foreach (var folder in config.LogFolder)
                {
                    try
                    {
                        if (folder.LogPath.Trim().Length == 0)
                        {
                            writeLog($"folder {folder.LogPath} was ignored");
                            continue;
                        }

                        if (Directory.Exists(folder.LogPath) == false)
                        {
                            writeLog($"folder {folder.LogPath} not exists", true);
                            continue;
                        }
                        
                        writeLog($"process folder {folder.LogPath}");
                        
                        CleanFolder(folder.LogPath, folder, config);
                    }
                    catch (Exception ex)
                    {
                        writeLog(ex.ToString(), true);
                    }
                } // end for
            }
            catch (Exception ex)
            {
                writeLog(ex.ToString(), true);
            }
        }

        static void CleanFolder(string folderPath, LogFolder folderConfig, AppSetting config)
        {
            if (!Directory.Exists(folderPath))
            {
                return;
            }

            string[] subFolders;
            
            try
            {
                subFolders = Directory.GetDirectories(folderPath);
            }
            catch (Exception e)
            {
                writeLog(e.ToString(), true);
                return;
            }

            foreach (var subFolder in subFolders)
            {
                CleanFolder(subFolder, folderConfig, config);
            }
            
            string[] files = Directory.GetFiles(folderPath, "*", SearchOption.TopDirectoryOnly);
            
            var folderNames = GetFolderNames(folderPath);

            // ignored folder
            if (_excludeFolders.Any(ef => folderNames.Any(f => f?.ToLower() == ef?.ToLower())))
            {
                writeLog($"folder {folderConfig.LogPath} was ignored");
                return;
            }

            if (!files.Any() && folderConfig.DeleteEmptyFolder) // 空資料匣
            {
                try
                {
                    if(!Directory.GetDirectories(folderPath).Any())
                    {
                        Directory.Delete(folderPath);
                    }
                }
                catch (Exception e)
                {
                    writeLog($"delete folder {folderPath} error, {e}", true);
                }
            }

            foreach (var filePath in files)
            {
                try
                {
                    var dtWrite = File.GetLastWriteTime(filePath); // 上次寫入時間
                    var dtCreate = File.GetCreationTime(filePath); // 建立時間,若用複製的,則會變更建立時間,不會改變寫入時間
                    var fileDate = (dtCreate.CompareTo(dtWrite) > 0) ? dtCreate : dtWrite;
                    
                    if (canDelete(folderConfig, filePath) == false)
                    {
                        writeLog($"file {filePath} 副檔名不符合刪除條件");
                        continue;
                    }

                    ZipFile(filePath, dtCreate, dtWrite, folderConfig, fileDate);
                    ArchiveFile(fileDate, folderConfig, filePath, config);
                }
                catch (Exception e)
                {
                    writeLog($"{filePath},{e}", true);
                    throw;
                }
            }
        }

        static bool ArchiveFile(DateTime fileDate, LogFolder folderConfig, string filePath, AppSetting config)
        {
            var fileDeleted = false;
            var lastDatePreserved = DateTime.Now.AddDays(folderConfig.PreservedDays * -1);
            
            if (fileDate.CompareTo(lastDatePreserved) < 0)
            {
                try
                {
                    fileDeleted = true;

                    if (string.IsNullOrEmpty(folderConfig.ArchiveAppName))
                    {
                        File.Delete(filePath);
                        writeLog($"{filePath} was deleted");    
                    }
                    else
                    {
                        MoveToArchiveFolder(filePath, folderConfig, config);
                    }
                        
                    Sleep();
                }
                catch (Exception ex)
                {
                    writeLog($"{filePath}, {ex}", true);
                }
            }
            else
            {
                writeLog($"{filePath} 還未到刪除時間");
            }

            return fileDeleted;
        }

        static void ZipFile(string filePath, DateTime dtCreate, DateTime dtWrite, LogFolder folderConfig, DateTime fileDate)
        {
            var fileExtension = Path.GetExtension(filePath).ToLower();
            
            var zipDate = DateTime.MaxValue;

            var zipHours = folderConfig.ZipHours + folderConfig.ZipDays * 24;
                
            if (zipHours > 0)
            {
                zipDate = DateTime.Now.AddHours(zipHours * -1);
            }
            
            if (_zipFileExtensions.Contains(fileExtension))
            {
                // 不再壓縮
                writeLog($"zip file {filePath} 壓縮檔不再壓縮");
                return;
            }

            // 檢查要不要壓縮
            if (fileDate.CompareTo(zipDate) < 0)
            {
                try
                {
                    var zipFilePath = SevenZipFile(filePath);

                    if(!string.IsNullOrEmpty(zipFilePath))
                    {
                        File.SetLastWriteTime(zipFilePath, dtWrite);
                        File.SetCreationTime(zipFilePath, dtCreate);
                    }

                    // 壓縮完成後刪除
                    File.Delete(filePath);
                    writeLog($"{filePath} was deleted after zipped");

                    Sleep();
                }
                catch (Exception e)
                {
                    writeLog(e.ToString(), true);
                }
            }
            else
            {
                writeLog($"{filePath} 還未到壓縮時間");
            }
        }

        static void MoveToArchiveFolder(string filePath, LogFolder folderConfig, AppSetting config)
        {
            if (string.IsNullOrEmpty(config.ArchivePath) || string.IsNullOrEmpty(folderConfig.ArchiveAppName))
            {
                writeLog("ArchivePath or ArchiveAppName is not set, skip moving to archive folder");
                return;
            }
            
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"來源檔案不存在: {filePath}");
                return;
            }
            
            string fileName = Path.GetFileName(filePath);
            
            var destFolder = $"{config.ArchivePath.TrimEnd('\\')}\\{_hostIp}\\{folderConfig.ArchiveAppName}";
            var subFolder = filePath.Replace(fileName, "").Replace(folderConfig.LogPath, "").Trim('\\');
            
            if (!string.IsNullOrEmpty(subFolder))
            {
                destFolder += $"\\{subFolder}";
            }
            
            if (!Directory.Exists(destFolder))
            {
                Directory.CreateDirectory(destFolder);
            }
            
            var destPath = $"{destFolder}\\{fileName}";

            try
            {
                File.Move(filePath, destPath);
            }
            catch (Exception e)
            {
                writeLog($"{filePath} 移動到 {destPath} 發生錯誤, {e}", true);
                throw;
            }
            
            writeLog($"檔案已搬移到: {destPath}");
        }

        static string ZipFile(string filePath)
        {
            var zipFilePath = "";
            
            using (ZipFile zip = new ZipFile())
            {
                zip.AddFile(filePath, "");

                string fileFolder = Path.GetDirectoryName(filePath);
                string fileName = Path.GetFileName(filePath); // 包含副檔名

                zipFilePath = $"{fileFolder}\\{fileName}.zip";

                if (File.Exists(zipFilePath) == false)
                {
                    zip.Save(zipFilePath);
                    writeLog($"{filePath} was zipped");
                }
            }

            return zipFilePath;
        }

        static string SevenZipFile(string filePath)
        {
            var fileFolder = Path.GetDirectoryName(filePath);
            var fileName = Path.GetFileName(filePath); 
            
            var zipFilePath = $"{fileFolder}\\{fileName}.7z";

            if (File.Exists(zipFilePath))
            {
                return "";
            }
            
            var compressor = new SevenZipCompressor();
            compressor.CompressFiles(zipFilePath, filePath);

            return zipFilePath;
        }

        static void Sleep()
        {
            Thread.Sleep(300);
        }

        static void writeLog(string msg, bool force = false)
        {
            Console.WriteLine(msg);

            if (!_writeLog && !force)
            {
                Console.WriteLine("_writeLog false");
                return;
            }

            string folderPath = $"{_baseDir}\\log";
            if (Directory.Exists(folderPath) == false)
            {
                Directory.CreateDirectory(folderPath);
            }

            StreamWriter w = File.AppendText($"{folderPath}\\CleanLogs.log");
            msg = $"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")} {msg}";
            w.WriteLine(msg);
            w.Close();
        }

        static bool canDelete(LogFolder folder, string filename)
        {
            string ext = Path.GetExtension(filename).ToLower();

            foreach (string itm in folder.FileExtNameInclude)
            {
                if (itm.Equals("*"))
                {
                    return true;
                }

                if (isExtEqual(itm, ext))
                {
                    return true;
                }
            }

            return false;
        }

        static bool isExtEqual(string ext1, string ext2)
        {
            ext1 = "." + ext1.ToLower().TrimStart('.');
            ext2 = "." + ext2.ToLower().TrimStart('.');
            return ext1.Equals(ext2);
        }

        static List<string> GetFolderNames(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return new List<string>();
            }

            path = path.TrimEnd('\\').TrimEnd('/');

            var arr = path.Split('\\');

            if (!arr.Any())
            {
                arr = path.Split('/');
            }

            return arr.ToList();
        }
    }
}
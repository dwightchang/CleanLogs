using CleanLogs.Model;
using Ionic.Zip;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CleanLogs
{
    class Program
    {
        public static bool _writeLog = true;
        public static string _baseDir = "";
        public static string[] _zipFileExtensions = new string[] { ".zip", ".rar", ".7z" };
        /// <summary>
        /// 內定不處理的檔案
        /// </summary>
        public static string[] _excludeFileExtensions = new string[] { ".dll", ".pdb", ".config", ".json", ".xml", ".exe", ".cs", ".cshtml", ".html", ".js", ".css" };

        static void Main(string[] args)
        {
            try
            {                
                _baseDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
                string path = _baseDir + "\\appSetting.json";                

                if (args.Length > 1)
                {
                    path = args[1];
                }

                string strAppSetting = File.ReadAllText(path);

                var config = JsonConvert.DeserializeObject<AppSetting>(strAppSetting);
                _writeLog = config.WriteLog;                

                cleanLogFolder(config);
            }
            catch(Exception ex)
            {
                writeLog(ex.ToString());
            }
        }


        /// <summary>
        /// 刪除超過期限的檔案,檔案日期=建立及上次寫入時間較大者
        /// </summary>
        static void cleanLogFolder(AppSetting config)
        {
            try
            {
                foreach (LogFolder folder in config.LogFolder)
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
                            writeLog($"folder {folder.LogPath} not exists");
                            continue;
                        }

                        writeLog($"process folder {folder.LogPath}");

                        string[] files = System.IO.Directory.GetFiles(folder.LogPath, "*", SearchOption.AllDirectories);

                        for (int j = 0; j < files.Length; j++)
                        {
                            string filePath = files[j];
                            string fileExtension = Path.GetExtension(filePath).ToLower();

                            if (_excludeFileExtensions.Contains(fileExtension))
                            {
                                writeLog($"{filePath} 不處理此類型檔案");
                                continue;
                            }

                            DateTime dtWrite = System.IO.File.GetLastWriteTime(filePath);  // 上次寫入時間
                            DateTime dtCreate = System.IO.File.GetCreationTime(filePath);  // 建立時間,若用複製的,則會變更建立時間,不會改變寫入時間

                            DateTime dt = (dtCreate.CompareTo(dtWrite) > 0) ? dtCreate : dtWrite;

                            DateTime lastDatePreserved = DateTime.Now.AddDays(folder.PreservedDays * -1);
                            DateTime zipDate = DateTime.MaxValue;

                            if (folder.ZipDays > 0)
                            {
                                zipDate = DateTime.Now.AddDays(folder.ZipDays * -1);
                            }

                            if (canDelete(folder, filePath) == false)
                            {
                                writeLog($"file {filePath} 副檔名不符合刪除條件");
                                continue;
                            }

                            bool fileDeleted = false;
                            if (dt.CompareTo(lastDatePreserved) < 0)
                            {
                                try
                                {
                                    fileDeleted = true;
                                    System.IO.File.Delete(filePath);
                                    writeLog($"{filePath} was deleted");

                                }
                                catch { }
                            }
                            else
                            {
                                writeLog($"{filePath} 還未到刪除時間");
                            }

                            if (fileDeleted == false)
                            {
                                if (_zipFileExtensions.Contains(fileExtension))
                                {
                                    // 不再壓縮
                                    writeLog($"zip file {filePath} 壓縮檔不再壓縮");
                                    continue;
                                }

                                // 檢查要不要壓縮
                                if (dt.CompareTo(zipDate) < 0)
                                {
                                    try
                                    {
                                        string zipFilePath = "";
                                        using (ZipFile zip = new ZipFile())
                                        {
                                            zip.AddFile(filePath, "");

                                            string fileFolder = Path.GetDirectoryName(filePath);
                                            string fileName = Path.GetFileName(filePath);  // 包含副檔名

                                            zipFilePath = $"{fileFolder}\\{fileName}.zip";

                                            if (File.Exists(zipFilePath) == false)
                                            {
                                                zip.Save(zipFilePath);
                                                writeLog($"{filePath} was zipped");
                                            }
                                        }

                                        File.SetLastWriteTime(zipFilePath, dtWrite);
                                        File.SetCreationTime(zipFilePath, dtCreate);

                                        // 壓縮完成後刪除
                                        File.Delete(filePath);
                                        writeLog($"{filePath} was deleted after zipped");
                                    }
                                    catch { }
                                }
                                else
                                {
                                    writeLog($"{filePath} 還未到壓縮時間");
                                }
                            }
                        }
                    }
                    catch(Exception ex)
                    {
                        writeLog(ex.ToString());
                    }
                }  // end for
            }
            catch (Exception ex)
            {                
                writeLog(ex.ToString());
            }
        }   // end function

        static void writeLog(string msg)
        {
            Console.WriteLine(msg);

            if(_writeLog == false)
            {
                Console.WriteLine("_writeLog false");
                return;
            }

            string folderPath = $"{_baseDir}\\log";
            if(Directory.Exists(folderPath) == false)
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
                if(itm.Equals("*"))
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

    }
}

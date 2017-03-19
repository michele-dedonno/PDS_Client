using System;
using System.Collections.Generic;
using System.Globalization;

namespace ClientTest
{
    public class BackupRecord
    {
        public int Id { get; set; }

        public string userID { get; set; }
        public string timestamp { get; set; }
        public List<myFileInfo> fileInfoList { get; set; }



        public BackupRecord(string userID, string timestamp, List<myFileInfo> list)
        {
            this.userID = userID;
            this.timestamp = timestamp;
            this.fileInfoList = list;
        }

        //for test
        public BackupRecord(List<myFileInfo> list)
        {
            this.userID = null;
            this.timestamp = null;
            this.fileInfoList = list;
        }

        public BackupRecord() { }
        

        /*if path is null return directories in root, else return all subdirectories in "path" directory*/
        public List<Item> GetDirectories(string path)
        {
            List<Item> directoryList = new List<Item>();
            
            if (path == null || path.Length == 0)
                path = "\\";
            
            //Console.WriteLine("#############");
            //Console.WriteLine("Folders in \"" + path + "\" :");

            foreach (myFileInfo fileInfo in fileInfoList)
            {
                /*find directories in "path" directory
                - sub directory path must begin with "path/"
                - sub directory path must be longer than "path" directory one
                */
                string directoryPath = fileInfo.RelativePath;
                //(fix)relativePath in myFileInfo is null for files in the root folder
                if (directoryPath == null || directoryPath.Length <= 0)
                    directoryPath = @"\";


                string comparePath= path + @"\";
                if (path.Equals(@"\"))
                    comparePath = @"\";

                if (directoryPath.StartsWith(comparePath) && directoryPath.Length > path.Length)
                {
                    //find first occourence of "\", needed to find the name of the subfolder
                    int endSubDirName = directoryPath.IndexOf(@"\", path.Length + 1);
                    if (endSubDirName != -1)
                        endSubDirName -= 1;
                    else
                        endSubDirName = directoryPath.Length - 1;
                        
                    //int end = directoryPath.Length - 1;
                    string subDirName;

                    if (path.Length <= 1)   //fix when path="\"
                        subDirName = directoryPath.Substring(path.Length, endSubDirName - path.Length + 1);
                    else
                        subDirName = directoryPath.Substring(path.Length + 1, endSubDirName - path.Length);

                    DirectoryItem subDir = new DirectoryItem
                    {
                        ItemPath = directoryPath.Substring(0, path.Length),
                        ItemName = subDirName,
                        ItemChecked = false
                    };


                    //Console.WriteLine("Name: "+ subDir.Name);
                    //Console.WriteLine("Path: "+ subDir.Path);
                    //Console.WriteLine("");

                    //add subfolders only once
                    if(directoryList.Find(x => x.ItemName.Equals(subDir.ItemName) && x.ItemPath.Equals(subDir.ItemPath)) == null)
                        directoryList.Add(subDir);
                }

            }

            //foreach (Item i in directoryList)
            //    Console.WriteLine("\t-" + i.ItemName);

            return directoryList;
        }
        


        public List<FileItem> GetFiles(string path)
        {
            List<FileItem> fileList = new List<FileItem>();

            if (path == null || path.Length == 0)
                path = "\\";

            //Console.WriteLine("#############");
            //Console.WriteLine("Files in \"" + path + "\" :");

            foreach (myFileInfo fileInfo in fileInfoList)
            {
                /*find directories in "path" directory
                - sub directory path must begin with "path"
                - sub directory path must be longer than "path" directory one
                */
                string directoryPath = fileInfo.RelativePath;
                //(fix)relativePath in myFileInfo is null for files in the root folder
                if (directoryPath == null || directoryPath.Length <= 0)
                    directoryPath = @"\";
                //Console.WriteLine("directoryPath: " + directoryPath);

                if (directoryPath.Equals(path))
                {
                    FileItem file = new FileItem
                    {
                        ItemPath = directoryPath.Substring(0, path.Length),
                        ItemName = fileInfo.Name,
                        ItemChecked = false,
                        FileInfo = fileInfo
                    };
                    

                    //Console.WriteLine("Name: " + file.Name);
                    //Console.WriteLine("Path: " + file.Path);
                    //Console.WriteLine("");

                    //add subfolders only once
                    if (fileList.Find(x => x.ItemName.Equals(file.ItemName) && x.ItemPath.Equals(file.ItemPath)) == null)
                        fileList.Add(file);
                }

            }

            //foreach (Item i in fileList)
            //    Console.WriteLine("\t-" + i.ItemName);

            return fileList;
        }

        public DateTime getDateTime()
        {
            DateTime dt = DateTime.ParseExact(timestamp, "yyyyMMddHHmmssffff", CultureInfo.InvariantCulture);

            return dt;
        }

        public string getDateString()
        {
            DateTime d = getDateTime();
            return d.Day + "/" + d.Month + "/" + d.Year;
        }

        public string getTimeString()
        {
            DateTime d = getDateTime();
            string h= d.Hour.ToString();
            string min= d.Minute.ToString();
            string sec = d.Second.ToString();

            if (d.Hour < 10)
                h = "0" + h;
            if (d.Minute < 10)
                min = "0" + min;
            if (d.Second < 10)
                sec = "0" + sec;

            return h + ":" + min + ":" + sec;
        }
    }
}

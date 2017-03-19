using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClientTest
{
    public class ItemProvider
    {
        public static List<Item> GetItems(BackupRecord br, string path)
        {
            var items = new List<Item>();
            

            foreach (var directory in br.GetDirectories(path))
            {
                DirectoryItem item;
                if(path.Equals(@"\") || path.Length <= 0)   //fix when path="\"
                {
                    item = new DirectoryItem
                    {
                        ItemName = directory.ItemName,
                        ItemPath = directory.ItemPath,
                        ItemChecked = false,
                        Items = GetItems(br, directory.ItemPath + directory.ItemName)
                    };
                }
                else
                {
                    item = new DirectoryItem
                    {
                        ItemName = directory.ItemName,
                        ItemPath = directory.ItemPath,
                        ItemChecked = false,
                        Items = GetItems(br, directory.ItemPath + @"\" + directory.ItemName)
                    };
                }
                

                items.Add(item);
            }


            foreach (FileItem file in br.GetFiles(path))
            {
                FileItem item = new FileItem
                {
                    ItemName = file.ItemName,
                    ItemPath = file.ItemPath,
                    ItemChecked = false,
                    FileInfo = file.FileInfo
                };


                items.Add(item);
            }
            

            return items;
        }

        /*get all checked files from entire list of files (tree view)*/
        public static List<myFileInfo> GetCheckedFileInfoList(List<Item> itemList)
        {
            if (itemList == null)
                throw new Exception("No datacontext setted!");

            List<myFileInfo> checkedFileInfoList = new List<myFileInfo>();
            List<Item> checkedFileList = new List<Item>();

            //check in the root first
            foreach (Item i in itemList)
            {
                if (typeof(DirectoryItem) == i.GetType())
                    checkedFileList.AddRange(getCheckedFilesRec((DirectoryItem)i));
                else if (i.ItemChecked)
                    checkedFileList.Add(i);
            }

            /**/
            //Console.WriteLine();
            //Console.WriteLine("file checkati:");


            foreach (Item i in checkedFileList)
            {
                myFileInfo info = ((FileItem)i).FileInfo;
                checkedFileInfoList.Add(((FileItem)i).FileInfo);
                //Console.WriteLine("\t" + ((FileItem)i).FileInfo.Name);
            }
                
            
            return checkedFileInfoList;
        }

        /*get all checked files starting from a Directory*/
        private static List<Item> getCheckedFilesRec(DirectoryItem directory)
        {
            List<Item> checkedFileList = new List<Item>();
            
            foreach(Item i in directory.Items)
            {
                if(typeof(DirectoryItem) == i.GetType())
                    checkedFileList.AddRange(getCheckedFilesRec((DirectoryItem)i));
                else if(i.ItemChecked)
                    checkedFileList.Add(i);
            }

            return checkedFileList;
        }
    }
}

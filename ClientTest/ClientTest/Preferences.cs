using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClientTest
{
    public static class Preferences
    {
        public static void DeleteAll()
        {
            Preferences.UserID.Delete();
            Preferences.SessionID.Delete();
            Preferences.BackupFolder.Delete();
        }

        public static class BackupFolder
        {
            public static string GetFolder()
            {
                return Properties.Settings.Default.BackupFolder;
            }

            public static void SetFolder(string folder)
            {
                Properties.Settings.Default.BackupFolder = folder;
                Properties.Settings.Default.Save();
            }

            public static bool IsSetted()
            {
                if (Properties.Settings.Default.BackupFolder == null || Properties.Settings.Default.BackupFolder.Length <= 0)
                    return false;

                return true;
            }

            public static void Delete()
            {
                Properties.Settings.Default.BackupFolder = null;
                Properties.Settings.Default.Save();
            }
        }

        public static class SessionID
        {
            public static string GetValue()
            {
                return Properties.Settings.Default.SessionID;
            }

            public static void SetValue(string value)
            {
                Properties.Settings.Default.SessionID = value;
                Properties.Settings.Default.Save();
            }

            public static bool IsSetted()
            {
                if (Properties.Settings.Default.SessionID == null || Properties.Settings.Default.SessionID.Length <= 0)
                    return false;

                return true;
            }

            public static void Delete()
            {
                Properties.Settings.Default.SessionID = null;
                Properties.Settings.Default.Save();
            }
        }

        public static class UserID
        {
            public static string GetValue()
            {
                return Properties.Settings.Default.UserID;
            }

            public static void SetValue(string value)
            {
                Properties.Settings.Default.UserID = value;
                Properties.Settings.Default.Save();
            }

            public static bool IsSetted()
            {
                if (Properties.Settings.Default.UserID == null || Properties.Settings.Default.UserID.Length <= 0)
                    return false;

                return true;
            }

            public static void Delete()
            {
                Properties.Settings.Default.UserID = null;
                Properties.Settings.Default.Save();
            }
        }
    }
}

using MahApps.Metro.Controls.Dialogs;
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ClientTest
{
    
    class BackupListItem : ListBoxItem
    {
        private MainWindow window;
        private MetroDialogSettings backupDialogSettings;
        public BackupRecord backupRecord { get; set; }


        public event PropertyChangedEventHandler PropertyChanged;
        
        public string BackupDate
        {
            get
            {
                return (string)GetValue(BackupDateProperty);
            }
            set
            {
                SetValue(BackupDateProperty, value);
            }
        }
        
        public static readonly DependencyProperty BackupDateProperty = DependencyProperty.Register("BackupDate", typeof(string), typeof(BackupListItem), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsParentMeasure));


        public string BackupTime
        {
            get
            {
                return (string)GetValue(BackupTimeProperty);
            }
            set
            {
                SetValue(BackupTimeProperty, value);
            }
        }

        public static readonly DependencyProperty BackupTimeProperty = DependencyProperty.Register("BackupTime", typeof(string), typeof(BackupListItem), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsParentMeasure));


        public bool IsAdvanced
        {
            get
            {
                return (bool)GetValue(IsAdvancedProperty);
            }
            set
            {
                SetValue(IsAdvancedProperty, value);
            }
        }

        public static readonly DependencyProperty IsAdvancedProperty = DependencyProperty.Register("IsAdvanced", typeof(bool), typeof(BackupListItem), new FrameworkPropertyMetadata(null));
        

        public BackupListItem()
        {
        }
        
        public BackupListItem(BackupRecord backupRecord, MainWindow window, bool advanced)
        {
            this.backupRecord = backupRecord;
            this.BackupDate = "Backup " + backupRecord.getDateString();
            this.BackupTime = backupRecord.getTimeString();
            this.window = window;
            this.IsAdvanced = advanced;

            this.Cursor = Cursors.Hand;
            

            //Backup dialog confirm settings
            backupDialogSettings = new MetroDialogSettings();
            backupDialogSettings.NegativeButtonText = "Annulla";
        }


        protected override async void OnSelected(RoutedEventArgs e)
        {
            base.OnSelected(e);
            if (IsAdvanced)
                await Task.Run(() => showBackupTreeView(backupRecord));
            else
            {
                if (Interlocked.Read(ref MainWindow.backgroundRunning) == MainWindow.TRUE)
                {
                    window.showAlertDialog(MainWindow.ALERT_DIALOG_TITLE, MainWindow.ALERT_DIALOG_MESSAGE);
                    return;
                }

                bool result = await window.showConfirmDialog("Ripristino Backup", buildDialogMessage(BackupDate, BackupTime));

                if (result == true)
                    window.retreiveBackupFiles(backupRecord.fileInfoList);
                
            }
        }

        internal void showBackupTreeView(BackupRecord backupRecord)
        {

            Application.Current.Dispatcher.Invoke(new Action(() =>
            {   
                //show loading
                window.BackupViewProgressBar.Visibility = Visibility.Visible;

                window.DataContext = ItemProvider.GetItems(backupRecord, @"\");

                //hide loading
                window.BackupViewProgressBar.Visibility = Visibility.Hidden;
            }));
        }


        private string buildDialogMessage(string backupDate, string backupTime)
        {
            return "Vuoi ripristinare "+ this.backupRecord.fileInfoList.Count +" file contenuti nel Backup del " + backupRecord.getDateString() + " (" + backupTime + ")?";
        }

    }
}

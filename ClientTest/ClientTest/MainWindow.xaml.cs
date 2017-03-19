using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.IO;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;

namespace ClientTest
{

    /// <summary>
    /// Logica di interazione per MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        const int INITIAL_DELAY_MINUTES = 1;
        const int PERIODIC_MINUTES = 15;
        // cancel token
        CancellationTokenSource source = new CancellationTokenSource();


        const int TAB_WIDTH_DIFF = 250;
        const int TAB_HEIGHT_DIFF = 80;
        
        private double startWindowMinWidth;
        private double startWindowMinHeight;

        //TRUE: background process running
        public bool logout;
        public static long backgroundRunning;
        public const long TRUE = 0;
        public const long FALSE = 1;
        readonly object locker = new object();
        
        
        //alert dialog
        public const string ALERT_DIALOG_TITLE = "Attendere...";
        public const string ALERT_DIALOG_MESSAGE = "Operazione di backup/ripristino in corso.";

        MetroDialogSettings backupDialogSettings;
        MetroDialogSettings alertDialogSettings;
        ObservableCollection<BackupListItem> simpleObsBackupCollection;
        ObservableCollection<BackupListItem> advObsBackupCollection;
        
        public int BackupFolder { get; private set; }


        /************************************ custom flyout closeCommand stuff ******************************************/
        private ICommand hiButtonCommand;

        private bool canExecute = true;

        public bool CanExecute
        {
            get
            {
                return this.canExecute;
            }

            set
            {
                if (this.canExecute == value)
                {
                    return;
                }

                this.canExecute = value;
            }
        }
        
        public ICommand HiButtonCommand
        {
            get
            {
                return hiButtonCommand;
            }
            set
            {
                hiButtonCommand = value;
            }
        }

        //custom flyout CloseCommand
        public void CloseFlyout(object obj)
        {
            Console.WriteLine("Icommand eseguito");

            //check if folder is valid and save preferences
            string selectedFolder = FolderTextBlock.Text;
            if (!Preferences.BackupFolder.IsSetted())
            {
                optionErrorMessage.Visibility = Visibility.Visible;
                optionInfoMessage.Visibility = Visibility.Hidden;
                concurrentErrorMessage.Visibility = Visibility.Hidden;
                return;
            }
            else
            {
                optionErrorMessage.Visibility = Visibility.Hidden;
                optionInfoMessage.Visibility = Visibility.Hidden;
                concurrentErrorMessage.Visibility = Visibility.Hidden;

                resetCurrentBackupFolder();
                optionFlyout.IsOpen = false;
            }
                
        }

        private void optionFlyout_IsOpenChanged(object sender, RoutedEventArgs e)
        {
            if(Preferences.BackupFolder.IsSetted())
            {
                optionErrorMessage.Visibility = Visibility.Hidden;
                optionInfoMessage.Visibility = Visibility.Hidden;
            }
            
        }
        /*****************************************************************************************************************/

        public MainWindow()
        {
            InitializeComponent();

            //background progress
            backgroundRunning = FALSE;

            logout = false;
            //dialogs settings
            backupDialogSettings = new MetroDialogSettings();
            backupDialogSettings.NegativeButtonText = "Annulla";

            alertDialogSettings = new MetroDialogSettings();
            
            Storyboard storyboard = new Storyboard();
            storyboard.Duration = new Duration(TimeSpan.FromSeconds(2.0));

            DoubleAnimation translateAnimation = new DoubleAnimation()
            {
                From = 10,
                To = -25,
                Duration = storyboard.Duration
            };

            Storyboard.SetTarget(translateAnimation, arrowImage);
            Storyboard.SetTargetProperty(translateAnimation, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));
            storyboard.RepeatBehavior = RepeatBehavior.Forever;

            storyboard.Children.Add(translateAnimation);
            Resources.Add("Storyboard", storyboard);

            //dynamic backup list
            simpleObsBackupCollection = new ObservableCollection<BackupListItem>();
            advObsBackupCollection = new ObservableCollection<BackupListItem>();
            simpleBackupList.ItemsSource = simpleObsBackupCollection;
            advancedBackupList.ItemsSource = advObsBackupCollection;


            //custom flyout closeCommand
            HiButtonCommand = new RelayCommand(CloseFlyout, param => this.canExecute);
            optionFlyout.CloseCommand= HiButtonCommand;
            optionFlyout.CloseCommandParameter = "ciaone";

            //set username on user tile
            userTile.Title += Preferences.UserID.GetValue();
        }


        //event fired when window is ready for interaction
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("opening window - backup folder: " + Preferences.BackupFolder.GetFolder());

            //check if backup folder property is setted
            if (!Preferences.BackupFolder.IsSetted())
            {
                optionFlyout.IsOpen = true;
                optionInfoMessage.Visibility = Visibility.Visible;
                
            }
            else
            {
                optionInfoMessage.Visibility = Visibility.Hidden;
                FolderTextBlock.Text = Preferences.BackupFolder.GetFolder();
            }
                


            Task.Run(() => updateBackupList());

            //save window dimensions
            this.startWindowMinWidth = this.MinWidth;
            this.startWindowMinHeight = this.MinHeight;
            //this.MaxWidth = this.MinWidth;
            //this.MaxHeight= this.MinHeight;

            //dont allow full screen window
            this.ResizeMode = ResizeMode.CanMinimize;

            Console.WriteLine("min width: " + this.startWindowMinWidth);
            Console.WriteLine("min height: " + this.startWindowMinHeight);


            //Console.WriteLine("Avvio del servizio di background automatico..");
            var dueTime = TimeSpan.FromMinutes(INITIAL_DELAY_MINUTES);
            var interval = TimeSpan.FromMinutes(PERIODIC_MINUTES);
            CancellationToken token = source.Token;
            RunPeriodicAsync(OnTick, dueTime, interval, token);



        }

        private async void OnTick()
        {
            bool autoBackground = true;
            await Task.Run(() => newBackupProcedureAsync(autoBackground));

        }

        // The `onTick` method will be called periodically unless cancelled.
        private static async Task RunPeriodicAsync(Action onTick, TimeSpan dueTime, TimeSpan interval, CancellationToken token)
        {

            // Initial wait time before we begin the periodic loop.
            if (dueTime > TimeSpan.Zero)
                await Task.Delay(dueTime, token);

            // Repeat this loop until cancelled.
            while (!token.IsCancellationRequested)
            {
                // Call our onTick function.
                onTick?.Invoke();

                // Wait to repeat again.
                if (interval > TimeSpan.Zero)
                    await Task.Delay(interval, token);
            }
        }

        /*
            retreive only selected files
            */
        private async void getSelectedBackupFiles(object sender, RoutedEventArgs e)
        {
            try
            {
                advancedGetBackupButton.IsEnabled = false;

                //done as first operation to avoid immediate change of DataContext selecting another backup
                List<myFileInfo> selectedFiles = ItemProvider.GetCheckedFileInfoList((List<Item>)DataContext);

                if(selectedFiles.Count <= 0)
                {
                    advancedGetBackupButton.IsEnabled = true;
                    return;
                }

                //some process is running in background...
                if (Interlocked.Read(ref backgroundRunning) == TRUE)
                {
                    showAlertDialog(ALERT_DIALOG_TITLE, ALERT_DIALOG_MESSAGE);
                    return;
                }
                
                bool result= await showConfirmDialog("Ripristino file...", "Sei sicuro di voler ripristinare " + selectedFiles.LongCount<myFileInfo>() + " file?");

                if(result)
                {
                    await Task.Run(() => retreiveBackupFilesTask(selectedFiles));

                    Task.Run(() => updateBackupList());
                }
                    
            }
            catch (Exception ex)
            {
                //update UI
                Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    UpdateProgressText.Text = "Errore nella selezione dei file";
                }));
            }

            advancedGetBackupButton.IsEnabled = true;

        }




        /*
        method that retreive selected backup files (all files if called from simple view backupItems)
        */
        public void retreiveBackupFiles(List<myFileInfo> requestedFilesList)
        {
            Task.Run(() => retreiveBackupFilesTask(requestedFilesList));
        }

        internal void retreiveBackupFilesTask(List<myFileInfo> requestedFilesList)
        {
            if (requestedFilesList.LongCount<myFileInfo>() <= 0)
                return;

            if (Interlocked.Read(ref backgroundRunning) == TRUE)
                return;

            Interlocked.Exchange(ref backgroundRunning, TRUE);

            TcpClient client = null;
            SslStream sslStream = null;
            
            try
            {
                //update UI
                Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    UpdateProgressText.Text = "Operazione di recupero in corso...";
                }));

                /* Create a TCP/IP client socket and connect to the server */
                Client.ConnectServer(Properties.Settings.Default.machineName, Properties.Settings.Default.serverCertificateName, Properties.Settings.Default.PORT, out client, out sslStream);

                /* Retrieve backup from the server */
                Client.GetBackup(sslStream, Preferences.UserID.GetValue(), Preferences.SessionID.GetValue(), requestedFilesList, this);

                //update UI
                Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    UpdateProgressText.Text = "Operazione di recupero terminata";
                }));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Errore nell'esecuzione del backup. " + ex.Message);
                Console.WriteLine(ex.StackTrace);

                //update UI
                Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    UpdateProgressText.Text = "Errore durante il recupero dei file";
                }));

            }
            finally
            {
                if (sslStream != null)
                    sslStream.Close();
                if (client != null)
                    client.Close();

                lock(locker)
                {
                    Interlocked.Exchange(ref backgroundRunning, FALSE);
                    System.Threading.Monitor.PulseAll(locker);
                }

                //update UI
                Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    UpdateProgressBar.Visibility = Visibility.Hidden;
                }));
                
            }

        }


        /*
        show an alert dialog
            */
        public void showAlertDialog(string title, string message)
        {
            this.ShowMessageAsync(title, message, MessageDialogStyle.Affirmative);
        }

        
        /*
        show a confirm dialog
            */
        public async Task<bool> showConfirmDialog(string title, string message)
        {
            MessageDialogResult controller = await this.ShowMessageAsync(title, message, MessageDialogStyle.AffirmativeAndNegative, backupDialogSettings);

            if (controller.Equals(MessageDialogResult.Affirmative))
                return true;
            
            return false;
        }




        private async void newBackupTile(object sender, RoutedEventArgs e)
        {
            //metodo asincrono
            bool autoBackground = false;
            bool result= await Task.Run(() => newBackupProcedureAsync(autoBackground));

            if(result)
                Task.Run(() => updateBackupList());
        }

        /*
        create a new backup (should use in a new thread)
            */
        internal bool newBackupProcedureAsync(bool autoBackground)
        {
            if (Interlocked.Read(ref backgroundRunning) == TRUE)
            {
                if(!autoBackground)
                    Application.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        showAlertDialog(ALERT_DIALOG_TITLE, ALERT_DIALOG_MESSAGE);
                    }));

                return false;
            }

            Interlocked.Exchange(ref backgroundRunning, TRUE);

            //Console.WriteLine("Inizio operazione di background automatico in corso...");
            bool result;

            //update UI
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                if (!autoBackground)
                    UpdateProgressText.Text = "Operazione di backup in corso...";
                else
                    UpdateProgressText.Text = "Operazione di backup automatico in corso...";

                //start animation before disabling Tile!
                arrowImage.Visibility = Visibility.Visible;
                ((Storyboard)Resources["Storyboard"]).Begin();

                BackupTile.IsEnabled = false;
            }));


            TcpClient client = null;
            SslStream sslStream = null;

            //Start doing work
            Console.WriteLine("Inizio operazione di backup...");

            try
            {
                /* Create a TCP/IP client socket and connect to the server */
                Client.ConnectServer(Properties.Settings.Default.machineName, Properties.Settings.Default.serverCertificateName, Properties.Settings.Default.PORT, out client, out sslStream);

                /* Create the new backup on the server */
                bool closeConnection = true;
                Client.NewBackup(sslStream, Preferences.UserID.GetValue(), Preferences.SessionID.GetValue(), closeConnection, this);
            
                //update UI - backup completed
                Application.Current.Dispatcher.Invoke(new Action(() => {
                    if (!autoBackground)
                        UpdateProgressText.Text = "Backup completato";
                    else
                        UpdateProgressText.Text = "Backup automatico completato";

                }));
                
                result = true;
            }
            catch (EmptyBackupFolderException e)
            {
                //update UI - error
                Application.Current.Dispatcher.Invoke(new Action(() => {
                    UpdateProgressText.Text = "Impossibile effettuare il backup di una cartella vuota";
                }));

                result = false;
            }
            catch (Exception e)
            {
                Console.WriteLine("Errore nell'esecuzione del backup. " + e.Message);
                Console.WriteLine(e.StackTrace);

                //update UI - error
                Application.Current.Dispatcher.Invoke(new Action(() => {
                    UpdateProgressText.Text = "Errore durante il backup";
                }));

                result = false;
            }
            finally
            {
                if (sslStream != null)
                    sslStream.Close();
                if (client != null)
                    client.Close();

                //update UI
                Application.Current.Dispatcher.Invoke(new Action(() => {
                    ((Storyboard)Resources["Storyboard"]).Stop();
                    BackupTile.IsEnabled = true;
                    arrowImage.Visibility = Visibility.Hidden;
                    UpdateProgressBar.Visibility = Visibility.Hidden;
                }));

                lock (locker)
                {
                    Interlocked.Exchange(ref backgroundRunning, FALSE);
                    System.Threading.Monitor.PulseAll(locker);
                }
                
            }

            return result;

        }
        

        private void getBackupListButton(object sender, RoutedEventArgs e)
        {
            NoBackupImage.Visibility = Visibility.Hidden;
            NoBackupText.Visibility = Visibility.Hidden;

            BackupListProgressBar.Visibility = Visibility.Visible;
            BackupListProgressBar.IsIndeterminate = true;
            
            Task.Run(() => updateBackupList());
        }


        internal void updateBackupList()
        {
            if (Interlocked.Read(ref backgroundRunning) == TRUE)
            {
                Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    showAlertDialog(ALERT_DIALOG_TITLE, ALERT_DIALOG_MESSAGE);
                }));
                return;
            }
                

            Interlocked.Exchange(ref backgroundRunning, TRUE);
            
            TcpClient client = null;
            SslStream sslStream = null;
            //string challenge = null;

            //show loading
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                //clear tree view
                DataContext = null;

                //UpdateProgressText.Text = "Recupero della lista di backup...";

                BackupListProgressBar.IsIndeterminate = true;
                BackupListProgressBar.Visibility = Visibility.Visible;

                BackupListAdvProgressBar.IsIndeterminate = true;
                BackupListAdvProgressBar.Visibility = Visibility.Visible;

                noBackupAdvanced.Visibility = Visibility.Hidden;
                NoBackupImage.Visibility = Visibility.Hidden;
                NoBackupText.Visibility = Visibility.Hidden;
                RetryText.Visibility = Visibility.Hidden;
            }));

            try
            {
                /* Create a TCP/IP client socket and connect to the server */
                Client.ConnectServer(Properties.Settings.Default.machineName, Properties.Settings.Default.serverCertificateName, Properties.Settings.Default.PORT, out client, out sslStream);

                /* Request backup list from the server */
                List<BackupRecord> backupList = Client.GetBackupList(sslStream, Preferences.UserID.GetValue(), Preferences.SessionID.GetValue(), null);

                //no backup records
                if (backupList.Count <= 0)
                {
                    Application.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        UpdateProgressText.Text = "";

                        //clear backup lists
                        simpleObsBackupCollection.Clear();
                        advObsBackupCollection.Clear();

                        //refresh UI
                        BackupListProgressBar.IsIndeterminate = false;
                        BackupListProgressBar.Visibility = Visibility.Hidden;

                        BackupListAdvProgressBar.IsIndeterminate = false;
                        BackupListAdvProgressBar.Visibility = Visibility.Hidden;

                        //UpdateProgressText.Text = "Lista di backup aggiornata";

                        noBackupAdvanced.Visibility = Visibility.Visible;
                        NoBackupImage.Visibility = Visibility.Visible;
                        NoBackupText.Visibility = Visibility.Visible;
                        RetryText.Visibility = Visibility.Visible;

                        //last backup labels
                        TileLastBackupLabel.Visibility = Visibility.Hidden;
                        TileLastBackupDate.Text = "";
                        TileLastBackupDate.Visibility = Visibility.Hidden;
                    }));

                    lock (locker)
                    {
                        Interlocked.Exchange(ref backgroundRunning, FALSE);
                        System.Threading.Monitor.PulseAll(locker);
                    }
                    return;
                }
            
            
                //show available backups
                Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    //clear backup lists
                    simpleObsBackupCollection.Clear();
                    advObsBackupCollection.Clear();

                    //refresh UI
                    BackupListProgressBar.IsIndeterminate = false;
                    BackupListProgressBar.Visibility = Visibility.Hidden;

                    BackupListAdvProgressBar.IsIndeterminate = false;
                    BackupListAdvProgressBar.Visibility = Visibility.Hidden;

                    foreach (BackupRecord record in backupList)
                    {
                        simpleObsBackupCollection.Add(new BackupListItem(record, this, false));
                        advObsBackupCollection.Add(new BackupListItem(record, this, true));
                    }

                    //UpdateProgressText.Text = "Lista di backup aggiornata";

                    //update tile last backup info
                    TileLastBackupLabel.Visibility = Visibility.Visible;
                    TileLastBackupDate.Visibility= Visibility.Visible;
                    TileLastBackupDate.Text = simpleObsBackupCollection.First<BackupListItem>().backupRecord.getDateString();
                }));
                Console.WriteLine("task terminato!");
            
            }
            catch(SessionAuthenticationException sae)
            {
                Preferences.DeleteAll();
                Console.WriteLine("Errore nell'autenticazione con il server. Messaggio: " + sae.Message);
                Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    LoginWindow l = new LoginWindow("Operazione fallita. Qualcun altro potrebbe aver effettuato l'accesso da un altro dispositivo.");
                    l.Show();
                    Close();
                }));
            }
            catch (Exception e)
            {
                //UI update
                Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    //show error on status bar
                    UpdateProgressText.Text = "Errore durante la ricezione della lista di backup";

                    //clear backup lists
                    simpleObsBackupCollection.Clear();
                    advObsBackupCollection.Clear();

                    //refresh UI
                    BackupListProgressBar.IsIndeterminate = false;
                    BackupListProgressBar.Visibility = Visibility.Hidden;

                    BackupListAdvProgressBar.IsIndeterminate = false;
                    BackupListAdvProgressBar.Visibility = Visibility.Hidden;

                    noBackupAdvanced.Visibility = Visibility.Visible;
                    NoBackupImage.Visibility = Visibility.Visible;
                    NoBackupText.Visibility = Visibility.Visible;
                    RetryText.Visibility = Visibility.Visible;
                }));

                Console.WriteLine("Errore nell recupero della lista di backup. " + e.Message);
                Console.WriteLine(e.StackTrace);
            }
            finally
            {
                if (sslStream != null)
                    sslStream.Close();
                if (client != null)
                    client.Close();

                lock (locker)
                {
                    Interlocked.Exchange(ref backgroundRunning, FALSE);
                    System.Threading.Monitor.PulseAll(locker);
                }
            }

        }
        

        private void openBackupFolderButton(object sender, RoutedEventArgs e)
        {
            if (!Preferences.BackupFolder.IsSetted())
                Preferences.BackupFolder.SetFolder(@"C:\");

            System.Diagnostics.Process.Start("explorer.exe", Preferences.BackupFolder.GetFolder());
        }

        private void checkedFiles(object sender, RoutedEventArgs e)
        {
            try
            {
                ItemProvider.GetCheckedFileInfoList((List<Item>)DataContext);
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private void simpleTabSelected(object sender, RoutedEventArgs e)
        {
            if(this.startWindowMinWidth > 0)
            {
                this.WindowState = WindowState.Normal;
                this.ResizeMode = ResizeMode.CanMinimize;
                this.Width = this.MaxWidth= this.MinWidth = this.startWindowMinWidth;
                this.Height = this.MaxHeight = this.MinHeight = this.startWindowMinHeight;
                
                
            }
                
        }

        private void advancedTabSelected(object sender, RoutedEventArgs e)
        {
            this.MinWidth = this.startWindowMinWidth + TAB_WIDTH_DIFF;

            //unclock window resizing
            this.MaxWidth= Double.PositiveInfinity;
            this.MaxHeight = Double.PositiveInfinity;
            this.ResizeMode = ResizeMode.CanResize;
        }
        
        

        private void optionTile(object sender, RoutedEventArgs e)
        {
            optionFlyout.IsOpen = true;
        }


        
        private void searchFolderButton(object sender, RoutedEventArgs e)
        {
            string actualBackupFolder = FolderTextBlock.Text;
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.IsFolderPicker = true;

            //show actual backup folder or "C:\"
            if (actualBackupFolder == null || actualBackupFolder.Length <= 0)
                actualBackupFolder = @"C:\";

            dialog.InitialDirectory = actualBackupFolder;

            CommonFileDialogResult result = dialog.ShowDialog();
            
            //ok pressed
            if (result.Equals(CommonFileDialogResult.Ok))
                FolderTextBlock.Text = dialog.FileName;
                
        }
        

        private void applyOptionButton(object sender, RoutedEventArgs e)
        {
            if (Interlocked.Read(ref backgroundRunning) == TRUE)
            {
                optionInfoMessage.Visibility = Visibility.Hidden;
                optionErrorMessage.Visibility = Visibility.Hidden;
                concurrentErrorMessage.Visibility = Visibility.Visible;
                return;
            }

            //check if folder is valid and save preferences
            string selectedFolder = FolderTextBlock.Text;
            if (selectedFolder == null || selectedFolder.Length <= 0 || !Directory.Exists(selectedFolder))
            {
                optionInfoMessage.Visibility = Visibility.Hidden;
                optionErrorMessage.Visibility = Visibility.Visible;
                concurrentErrorMessage.Visibility = Visibility.Hidden;
                return;
            }

            Preferences.BackupFolder.SetFolder(selectedFolder);
            optionInfoMessage.Visibility = Visibility.Hidden;
            optionErrorMessage.Visibility = Visibility.Hidden;
            concurrentErrorMessage.Visibility = Visibility.Hidden;

            optionFlyout.IsOpen = false;
        }
        
        
        private void cancelOptionButton(object sender, RoutedEventArgs e)
        {
            resetCurrentBackupFolder();
        }
        

        private void resetCurrentBackupFolder()
        {
            if (!Preferences.BackupFolder.IsSetted())
                FolderTextBlock.Text = "";
            else
                FolderTextBlock.Text = Preferences.BackupFolder.GetFolder();


        }


        /*
            invoked when click on "close application" button
            ATTENTION: e.cancel mantain
            */
        private async void MetroWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            //Console.WriteLine("Closing windows....");
            
            if (Interlocked.Read(ref backgroundRunning) == TRUE)
            {
                //cancel window closing
                e.Cancel = true;
                
                this.ShowMetroDialogAsync(new WaitDialog());

                //start thread which waits other threads ending
                await Task.Run(() => waitTasks());
                Close();
                Console.WriteLine("Closing windows....");
                return;
                    
            }
            else
            {
                //if user requests logout, show again login window
                if(logout)
                {
                    logout = false;
                    //if logout fail, dont close the main window, else show login window
                    if (!logoutProcedure(Preferences.UserID.GetValue()))
                    {
                        e.Cancel = true;
                        this.ShowMessageAsync("Attenzione", "Impossibile effettuare il logout. Riprovare più tardi.");
                        return;
                    }
                    else
                    {
                        e.Cancel = false;

                        //delete preferences
                        Preferences.DeleteAll();

                        LoginWindow login = new LoginWindow();
                        login.Show();
                    }
                }
                else
                    e.Cancel = false;
            }

            // Close Background Periodic Thread
            source.Cancel();


        }

        internal void waitTasks()
        {
            lock(locker)
            {
                //wait for background thread to close
                while (Interlocked.Read(ref backgroundRunning) == TRUE)
                {
                    //Console.WriteLine("waiting for processes....");
                    Console.WriteLine("waiting for processes....");
                    System.Threading.Monitor.Wait(locker);
                }
            }
            

            Console.WriteLine("wait thread completed");

        }


        private async void logoutTile(object sender, RoutedEventArgs e)
        {
            if (Interlocked.Read(ref backgroundRunning) == TRUE)
            {
                showAlertDialog(ALERT_DIALOG_TITLE, ALERT_DIALOG_MESSAGE);
                return;
            }


            MessageDialogResult result = await this.ShowMessageAsync("Conferma", "Sei sicuro di voler effettuare il logout?", MessageDialogStyle.AffirmativeAndNegative, backupDialogSettings);
            if (result.Equals(MessageDialogResult.Affirmative))
            {
                logout = true;
                Close();
            }
            
        }

        /**
         * return:
         *  - false if logout is not performed
         */
        internal bool logoutProcedure(string clientID)
        {
            TcpClient client = null;
            SslStream sslStream = null;
            bool result = false;

            if (clientID == null)
                return false;

            try
            {
                /* Create a TCP/IP client socket and connect to the server */
                Client.ConnectServer(Properties.Settings.Default.machineName, Properties.Settings.Default.serverCertificateName, Properties.Settings.Default.PORT, out client, out sslStream);

                /* Start login procedure */
                result = Client.Logout(sslStream, clientID);
            }
            catch (Exception e)
            {
                Console.WriteLine("Eccezione inaspettata in logoutProcedure(). Messaggio " + e.Message);
                Console.WriteLine(e.StackTrace);
                result = false;
            }
            finally
            {
                if (sslStream != null)
                    sslStream.Close();
                if (client != null)
                    client.Close();
            }

            return result;
        }

        private void RetryText_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            Task.Run(() => updateBackupList());
        }

        private void RetryUpdateBackupTile(object sender, RoutedEventArgs e)
        {
            Task.Run(() => updateBackupList());
        }
    }
}

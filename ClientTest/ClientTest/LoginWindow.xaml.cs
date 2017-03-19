using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using System;
using System.Net.Security;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Input;

namespace ClientTest
{
    /// <summary>
    /// Logica di interazione per LoginWindow.xaml
    /// </summary>
    public partial class LoginWindow : MetroWindow
    {

        /// Determines whether the username meets conditions.
        /// Username conditions:
        /// Must be 1 to 16 character in length
        /// Must start with letter a-zA-Z
        /// May contain letters, numbers or '.','-' or '_'
        /// Must not end in '.','-','._' or '-_' 
        private static Regex allowedRegEx = new Regex(@"^(?=[a-zA-Z])[-\w.]{0,15}([a-zA-Z\d]|(?<![-.])_)$", RegexOptions.Compiled);

        public LoginWindow(string error)
        {
            InitializeComponent();

            //Show error on window open
            if(error != null)
            {
                loginErrorText.Text = error;
                loginErrorText.Visibility = Visibility.Visible;
                loginErrorIcon.Visibility = Visibility.Visible;
            }
            

            //set
            Loaded += (sender, e) => MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            
            if (Preferences.UserID.IsSetted() && Preferences.SessionID.IsSetted())
            {
                MainWindow window = new MainWindow();
                window.Show();
                this.Close();
            }
        }

        public LoginWindow()
        {
            InitializeComponent();

            //set
            Loaded += (sender, e) => MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            
            if (Preferences.UserID.IsSetted() && Preferences.SessionID.IsSetted())
            {
                MainWindow window = new MainWindow();
                window.Show();
                this.Close();
            }
        }
                
        private async void loginButton(object sender, RoutedEventArgs e)
        {
            string error = null;
            //disable login button, error text and icon, show progress ring while login
            login.IsEnabled = false;
            loginProgressRing.Visibility = Visibility.Visible;
            loginErrorText.Visibility = Visibility.Hidden;
            loginErrorIcon.Visibility = Visibility.Hidden;
            usernameTextBox.IsEnabled = false;
            passwordTextBox.IsEnabled = false;
            
            string username = usernameTextBox.Text;
            string password = passwordTextBox.Password;
            if (!checkCredential(username, allowedRegEx) || !checkCredential(password, allowedRegEx))
            {
                //enable login button, hide progress ring and show error message
                login.IsEnabled = true;
                loginProgressRing.Visibility = Visibility.Hidden;
                loginErrorIcon.Visibility = Visibility.Visible;
                usernameTextBox.IsEnabled = true;
                passwordTextBox.IsEnabled = true;

                loginErrorText.Text = "Username o password sintatticamente non validi";
                loginErrorText.Visibility = Visibility.Visible;

                return;
            }


            int loginResult = await Task.Run(() => loginProcedureAsync(username, password));


            switch (loginResult)
            {
                case 0: //login successful
                    MainWindow main = new MainWindow();
                    main.Show();
                    this.Close();

                    
                    return;

                case 1:
                    error = "Username e/o password errati";
                    break;
                case 2:
                    error = "L'utente risulta già loggato.";
                    MetroDialogSettings settings = new MetroDialogSettings();
                    settings.DialogMessageFontSize = 14;
                    settings.DialogTitleFontSize = 20;
                    MessageDialogResult result= await this.ShowMessageAsync("Vuoi forzare il login?","L'utente risulta già loggato. (Tutti gli altri dispositivi collegati verranno disconnessi)", MessageDialogStyle.AffirmativeAndNegative, settings);
                    if(result.Equals(MessageDialogResult.Affirmative))
                    {
                        // logout
                        if (!logoutProcedure(username))
                            Console.WriteLine("Errore durante il logout forzato");
                            //throw new Exception("Errore nel logout");

                        //virtual click on login button
                        login.IsEnabled = true;
                        ButtonAutomationPeer peer = new ButtonAutomationPeer(login);
                        IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                        invokeProv.Invoke();
                    }

                    break;
                case 3:
                    error = "Errore inatteso sul server.";
                    break;
                case 4:
                    error = "Errore durante l'operazione di login. Controlla la connessione al server.";
                    break;
            }
            //enable login button, hide progress ring and show error message
            login.IsEnabled = true;
            loginProgressRing.Visibility = Visibility.Hidden;
            loginErrorIcon.Visibility = Visibility.Visible;
            usernameTextBox.IsEnabled = true;
            passwordTextBox.IsEnabled = true;

            loginErrorText.Text = error;
            loginErrorText.Visibility = Visibility.Visible;
            
        }

        private bool checkCredential(string input, Regex regex)
        {
            if (string.IsNullOrEmpty(input) || !regex.IsMatch(input))
                return false;
            return true;
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
        /**
         * 
         * return:
         *  4: generic error in the client
         *  3: generic error in the server
         *  2: already logged
         *  1: wrong username or password
         *  0: login successful
         */
        internal int loginProcedureAsync(string clientID, string password)
        {
            TcpClient client = null;
            SslStream sslStream = null;
            //string challenge = null;
            int result = 4; // initialize the result with the worst condition
            
            if (clientID == null || password == null || clientID.Length <= 0 || password.Length <= 0)
                return 1;
            
            try
            {
                /* Create a TCP/IP client socket and connect to the server */
                Client.ConnectServer(Properties.Settings.Default.machineName, Properties.Settings.Default.serverCertificateName, Properties.Settings.Default.PORT, out client, out sslStream/*, out challenge*/);

                /* Start login procedure */
                result = Client.Login(sslStream/*, challenge*/, clientID, password);
            }
            catch (Exception e)
            {
                Console.WriteLine("Eccezione inaspettata in loginProcedureAsync(). Messaggio " + e.Message);
                Console.WriteLine(e.StackTrace);
                result = 4; 
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

        internal void sleepThread()
        {
            Thread.Sleep(2000);
        }

        private void passwordTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if(e.Key == Key.Enter)
            {
                //virtual click on login button
                ButtonAutomationPeer peer = new ButtonAutomationPeer(login);
                IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                invokeProv.Invoke();
            }
        }

       
    }
}

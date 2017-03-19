using System;
using System.Collections;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using System.Security.Cryptography.X509Certificates;
using System.IO;
using System.Security.Cryptography;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Linq;
using System.Windows;

namespace ClientTest
{
    public class Client
    {
        const int BUFFER_DIM = 10000000;
        const string POST_BACKUP = "POST_BACKUP"; // Message sent by client to server when start a new backup phase
        const string END_BACKUP = "END_BACKUP"; // Message sent by server to client when the backup is finished (in download or upload)
        const string GET_BACKUP = "GET_BACKUP"; // Message sent by client to server when requires to restore a backup
        const string LIST_BACKUPS = "LIST_BACKUPS"; // Message sent by client to server when requires the list of backups related to a clientID
        const string LOGIN = "LOGIN"; // Message sent by client to server when requires to log in
        const string LOGIN_OK = "LOGIN_OK"; // Message sent by server to client when login credential are right
        const string LOGIN_CREDENTIALS_ERR = "LOGIN_CREDENTIALS_ERR"; // Message sent by server to client when login credential are wrong
        const string LOGIN_GENERIC_ERR = "LOGIN_GENERIC_ERROR"; // Message sent by server to client when there is a generic error in the login procedure
        const string LOGIN_ALREADY_LOGGED = "LOGIN_ALREADY_LOGGED";// Message sent by server to client when that user is already logged
        const string AUTHENTICATION_OK = "AUTHENTICATION_OK"; // Message sent by the server to the client when the session authentication has completed successfully
        const string AUTHENTICATION_ERR = "AUTHENTICATION_ERR"; // Message sent by the server to the client when the session authentication has not completed successfully
        const string LOGOUT = "LOGOUT"; // Message sent by client to server when requires to log out
        const string LOGOUT_OK = "LOGOUT_OK"; // Message sent by server to client when log out successfully
        const string LOGOUT_ERR = "LOGOUT_ERR"; // Message sent by server to client when log out fails
        const string FILE_LIST = "FILE_LIST"; // Message sent by server to client when there are some file to backup. It is followed by the file list.
        const string END_REQUEST = "END_REQUEST"; // Message sent by client to server when the interaction is finished and the connection can be closed

        
        private static Hashtable certificateErrors = new Hashtable();

        /**
          * Method that creates a TCP/IP client socket and connect to the server (with authentication).  It also receive the challenge for the connection and send the client_ID
          * It returns true if it succeded, false otherwise.
         */ 
        public static void ConnectServer(string serverAddress, string serverName, int port, out TcpClient socket, out SslStream sslStream) // throws Exception
        {
            socket = null;
            sslStream = null;
            try
            {
                // serverAddress is the IP address of the host running the server application.
                socket = new TcpClient(serverAddress, port);

                /* Authenticate Client */
                // Create an SSL stream that will close the client's stream.
                sslStream = new SslStream(socket.GetStream(), false, new RemoteCertificateValidationCallback(ValidateServerCertificate), null);

                // The server name must match the name on the server certificate.
                sslStream.AuthenticateAsClient(serverName);
                
                // Set timeouts for the read and write to 10 seconds.
                sslStream.ReadTimeout = 10000;
                sslStream.WriteTimeout = 10000;
                

                if (socket == null || sslStream == null /*|| challenge == null*/)
                    throw new Exception("Output parameters have unexpected values");
            }
            catch (SocketException se)
            {
                Console.WriteLine(se.StackTrace);
                throw new Exception("Socket Exception in connectServer(). Message: '" + se.Message + "'. Error code: " + se.ErrorCode + "'");
            }
            catch (AuthenticationException ae)
            {
                throw new Exception("Authentication Exception in connectServer(). Message: '" + ae.Message + "'");
                
            }
            catch (Exception e)
            {
                
                throw new Exception("Unexpected exception in connectServer(). Message: '" + e.Message + "'. ");
            }
        }

        /**
         * Method that log out to the server
         * 
         * return:
         *  true: log out ok
         *  false: log out failed
         *  throw Exception : generic error in the client
         */
        public static bool Logout(SslStream sslStream, string clientID)
        {
            bool result = false;
            try
            {
                if (sslStream == null || clientID == null)
                    throw new Exception("[LOGOUT] Invalid input parameters");

                /* Send LOGOUT request to the server: "LOGOUT?0" */
                string requestMessage = LOGOUT + "?" /*+ challenge*/ + "0";
                Console.WriteLine(">Client: " + requestMessage);
                requestMessage += "\r\n";
                sslStream.Write(Encoding.UTF8.GetBytes(requestMessage));
                sslStream.Flush();

                /* Receive ACK:0 */
                string responseMessage = ReadMessage(sslStream, "\r\n");
                Console.WriteLine(">Server: " + responseMessage);
                if (!responseMessage.Equals("ACK:0"))
                    //Console.WriteLine("Invio dell'ultimo messaggio al server non è andato a buon fine.");
                    throw new Exception("Ricezione ACK 0 fallita. L'invio della richiesta al server non è andato a buon fine ");
                
                /* Send ClientID: "ClientID" */
                string credentialsMessage = clientID;
                Console.WriteLine(">Client (clientID): " + credentialsMessage);
                credentialsMessage += "\r\n";
                sslStream.Write(Encoding.UTF8.GetBytes(credentialsMessage));
                sslStream.Flush();

                /* Receive Server Response */
                responseMessage = ReadMessage(sslStream, "\r\n");
                Console.WriteLine(">Server: " + responseMessage);
                switch (responseMessage)
                {
                    case LOGOUT_OK: // Logout Successful                     
                        result = true;
                        break;

                    case LOGOUT_ERR: // Logout failed server side       
                        result = false;
                        break;

                    default: // Unexpected answer from the server
                        throw new Exception("Unexpected answer message received from the server");
                        //break;
                        
                }

                /* Send END_REQUEST to the server: "END_REQUEST?0" */
                string closingMessage = END_REQUEST + "?" + "0";
                Console.WriteLine(">Client: " + closingMessage);
                closingMessage += "\r\n";
                sslStream.Write(Encoding.UTF8.GetBytes(closingMessage));
                sslStream.Flush();

                return result;

            }
            catch (IOException ioe)
            {
                throw new Exception("IOException in Logout(). Message: '" + ioe.Message + "'.");
            }
            catch (Exception e)
            {
                throw new Exception("Unexpected exception in Logout(). Message: '" + e.Message + "'");
            }
        }

        /**
         * Method that log in to the server
         * 
         * return:
         *  3: generic error in the server
         *  2: already logged
         *  1: wrong username or password
         *  0: login successful
         *  throw Exception : generic error in the client
         */
        public static int Login(SslStream sslStream, string clientID, string password) // throw exception
        {
            int returnValue = 3;
            try
            {
                if (sslStream == null || clientID == null || password == null/*|| challenge == null*/)
                    throw new Exception("[LOGIN] Invalid input parameters");

                /* Send LOGIN request to the server: "LOGIN?0" */
                string requestMessage = LOGIN + "?" /*+ challenge*/ + "0";
                Console.WriteLine(">Client: " + requestMessage);
                requestMessage += "\r\n";
                sslStream.Write(Encoding.UTF8.GetBytes(requestMessage));
                sslStream.Flush();

                /* Receive ACK:0 */
                string responseMessage = ReadMessage(sslStream, "\r\n");
                Console.WriteLine(">Server: " + responseMessage);
                if (!responseMessage.Equals("ACK:0"))
                    //Console.WriteLine("Invio dell'ultimo messaggio al server non è andato a buon fine.");
                    throw new Exception("Ricezione ACK 0 fallita. L'invio della richiesta al server non è andato a buon fine ");

                /* compute sha1 password*/
                string sha1Psw = computeSHA1String(password);
                if (sha1Psw == null)
                    throw new Exception("Errore nel calcolo della password in sha1");


                /* Send ClientID AND sha1 Password to the server: "ClientID?sha1Psw" */
                string credentialsMessage = clientID + "?" + sha1Psw;
                Console.WriteLine(">Client (password): " + credentialsMessage);
                credentialsMessage += "\r\n";
                sslStream.Write(Encoding.UTF8.GetBytes(credentialsMessage));
                sslStream.Flush();

                /* Receive Server Response */
                responseMessage = ReadMessage(sslStream, "\r\n");
                Console.WriteLine(">Server: " + responseMessage);
                switch (responseMessage)
                {
                    case LOGIN_OK: // Login Successful
                        /* send ACK:1 */
                        sslStream.Write(Encoding.UTF8.GetBytes("ACK:1\r\n"));
                        sslStream.Flush();
                        Console.WriteLine(">Client: ACK:1");

                        /* receive session ID */
                        string sessionIDMessage = ReadMessage(sslStream, "\r\n");
                        Console.WriteLine(">Server (sessionID): " + sessionIDMessage);

                        // Set session ID and Client ID
                        Preferences.UserID.SetValue(clientID);
                        Preferences.SessionID.SetValue(sessionIDMessage);

                        returnValue = 0;
                        break;

                    case LOGIN_CREDENTIALS_ERR: // Username or/and Password wrong       
                        returnValue = 1;
                        break;

                    case LOGIN_ALREADY_LOGGED: // Clientd already logged in
                        returnValue = 2;
                        break;

                    case LOGIN_GENERIC_ERR: // Generic error in the server
                        returnValue = 3;
                        break;
                    default: // Unexpected answer from the server
                        throw new Exception("Unexpected answer message received from the server");
                        //break;

                }

                /* Send END_REQUEST to the server: "END_REQUEST?0" */
                string closingMessage = END_REQUEST + "?" + "0";
                Console.WriteLine(">Client: " + closingMessage);
                closingMessage += "\r\n";
                sslStream.Write(Encoding.UTF8.GetBytes(closingMessage));
                sslStream.Flush();

                return returnValue;

            }
            catch (IOException ioe)
            {
                throw new Exception("IOException in login(). Message: '" + ioe.Message + "'.");
            }
            catch (Exception e)
            {
                throw new Exception("Unexpected exception in login(). Message: '" + e.Message + "'");
            }
        }

        /**
         * Method that retrieves from the server the list of backups related to the client 'clientID'
         */
        public static List<BackupRecord> GetBackupList(SslStream sslStream, string clientID, string sessionID, string timestamp) // throw SessionAuthenticationException
        {
            //se timestamp è null, ritorna tutti i backup relativi al clientID
            List<BackupRecord> backupList = new List<BackupRecord>();

            try
            {
                if (sessionID == null || sslStream == null || clientID == null)
                    throw new Exception("Invalid input parameters");

                /* Send LIST_BACKUPS to the server: "LIST_BACKUPS?0" */
                string requestMessage = LIST_BACKUPS + "?" + "0";
                Console.WriteLine(">Client: " + requestMessage);
                requestMessage += "\r\n";
                sslStream.Write(Encoding.UTF8.GetBytes(requestMessage));
                sslStream.Flush();

                /* Receive ACK:0 */
                string responseMessage = ReadMessage(sslStream, "\r\n");
                Console.WriteLine(">Server: " + responseMessage);
                if (!responseMessage.Equals("ACK:0"))
                    //Console.WriteLine("Invio dell'ultimo messaggio al server non è andato a buon fine.");
                    throw new Exception("Ricezione ACK 0 fallita. L'invio della richiesta al server non è andato a buon fine ");

                /* Send ClientID AND SessionID */
                string authenticationMessage = clientID + "?" + sessionID;
                Console.WriteLine(">Client: " + authenticationMessage);
                authenticationMessage += "\r\n";
                sslStream.Write(Encoding.UTF8.GetBytes(authenticationMessage));
                sslStream.Flush();

                /* Receive Server Response */
                responseMessage = ReadMessage(sslStream, "\r\n");
                Console.WriteLine(">Server: " + responseMessage);
                switch (responseMessage)
                {
                    case AUTHENTICATION_OK: // Client ID and Session ID are OK
                        break;
                    case AUTHENTICATION_ERR: // Client ID and Session ID are wrong
                        throw new SessionAuthenticationException("Client ID or Session ID are wrong");                        
                    default: // Unexpected answer from the server
                        throw new Exception("Unexpected answer message received from the server");
                }
                

                if (timestamp != null)
                    /* Send timestamp AND Challenge:1 to the server: "timestamp?sessionID:1" */
                    requestMessage = timestamp + "?" + sessionID + ":1";
                else
                    /* Send NULL AND Challenge:1 to the server: "NULL?sessionID:1" */
                    requestMessage = "NULL" + "?" + sessionID + ":1";

                Console.WriteLine(">Client (timestamp): " + requestMessage);
                requestMessage += "\r\n";
                sslStream.Write(Encoding.UTF8.GetBytes(requestMessage));
                sslStream.Flush();

                /* Receive json backup list */
                string jsonList = ReadMessage(sslStream, "\r\n");
                //Console.WriteLine(">Server (json list): " + jsonList);

                /* Send ACK:1 */
                sslStream.Write(Encoding.UTF8.GetBytes("ACK:1\r\n"));
                sslStream.Flush();
                Console.WriteLine(">Client: ACK:1");

                /* process json list */
                backupList = JsonConvert.DeserializeObject<List<BackupRecord>>(jsonList);
                if (backupList == null)
                    throw new Exception("Error while parsing the json list received");

                if (backupList.Count > 1)
                    //order list by timestamp
                    backupList = backupList.OrderByDescending(x => x.timestamp).ToList<BackupRecord>();

                /* Send END_REQUEST to the server: "END_REQUEST?0" */
                string closingMessage = END_REQUEST + "?" /*+ challenge*/ + "0";
                Console.WriteLine(">Client: " + closingMessage);
                closingMessage += "\r\n";
                sslStream.Write(Encoding.UTF8.GetBytes(closingMessage));
                sslStream.Flush();

                return backupList;

            }
            catch(SessionAuthenticationException sae)
            {

                /* Send END_REQUEST to the server: "END_REQUEST?0" */
                string endMessage = END_REQUEST + "?" + "0";
                Console.WriteLine(">Client: " + endMessage);
                endMessage += "\r\n";
                sslStream.Write(Encoding.UTF8.GetBytes(endMessage));
                sslStream.Flush();

                throw sae;
            }
            catch (IOException ioe)
            {
                Console.WriteLine(ioe.StackTrace);
                throw new Exception("IOException in getBackupList(). Message: '" + ioe.Message + "'.");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
                throw new Exception("Unexpected exception in getBackupList(). Message: '" + e.Message + "'");
            }
            

        }

        /**
         * Method that creates a backup of the current working directory
         */
        public static void NewBackup(SslStream sslStream, string clientID, string sessionID, bool closeConnection, MainWindow window)
        {
            
            string timestamp = null; // timestamp of the current session of backup

            try
            {
                if (sslStream == null || clientID == null || sessionID == null)
                    throw new Exception("Invalid input parameters");

                /* Create the screening of the CLIENT_DIRECTORY */
                if (!Preferences.BackupFolder.IsSetted())
                    throw new Exception("Client backup directory is not set");

                string jsonScreening = createScreening(Preferences.BackupFolder.GetFolder(), out timestamp);
                if (jsonScreening == null || timestamp == null)
                    throw new Exception("Errore nella creazione dello screening");
                
                /* Send POST_BACKUP to the server: "POST_BACKUP?0" */
                string requestMessage = POST_BACKUP + "?" + "0";
                Console.WriteLine(">Client: " + requestMessage);
                requestMessage += "\r\n";
                sslStream.Write(Encoding.UTF8.GetBytes(requestMessage));
                sslStream.Flush();

                /* Receive ACK:0 */
                string responseMessage = ReadMessage(sslStream, "\r\n");
                Console.WriteLine(">Server: " + responseMessage);
                if (!responseMessage.Equals("ACK:0"))
                    throw new Exception("Ricezione ACK 0 fallita. L'invio della richiesta al server non è andato a buon fine ");

                /* Send ClientID AND SessionID */
                string authenticationMessage = clientID + "?" + sessionID;
                Console.WriteLine(">Client: " + authenticationMessage);
                authenticationMessage += "\r\n";
                sslStream.Write(Encoding.UTF8.GetBytes(authenticationMessage));
                sslStream.Flush();

                /* Receive Server Response */
                responseMessage = ReadMessage(sslStream, "\r\n");
                Console.WriteLine(">Server: " + responseMessage);
                switch (responseMessage)
                {
                    case AUTHENTICATION_OK: // Client ID and Session ID are OK
                        break;
                    case AUTHENTICATION_ERR: // Client ID and Session ID are wrong
                        throw new SessionAuthenticationException("Client ID or Session ID are wrong");
                    default: // Unexpected answer from the server
                        throw new Exception("Unexpected answer message received from the server");
                }

                /* Send json screening to the server */
                requestMessage = jsonScreening + "\r\n";
                sslStream.Write(Encoding.UTF8.GetBytes(requestMessage));
                sslStream.Flush();
                Console.WriteLine(">Client  (json screening): " + jsonScreening);

                /* Send timestamp of the screening to the server */
                requestMessage = timestamp + "\r\n";
                sslStream.Write(Encoding.UTF8.GetBytes(requestMessage));
                sslStream.Flush();
                Console.WriteLine(">Client (timestamp screening): " + timestamp);

                /* Receive ACK:1 */
                responseMessage = ReadMessage(sslStream, "\r\n");
                Console.WriteLine(">Server: " + responseMessage);
                if (!responseMessage.Equals("ACK:1"))
                    //Console.WriteLine("Invio dell'ultimo messaggio al server non è andato a buon fine.");
                    throw new Exception("Ricezione ACK 1 fallita. L'invio dello screening al server non è andato a buon fine ");

                /* Process upload requests from the server (if any) */
                // Receive Message
                string recvMessage = ReadMessage(sslStream, "\r\n");
                Console.WriteLine(">Server: " + recvMessage);
                switch (recvMessage)
                {
                    case FILE_LIST: // The server will sent the file list related to the file that need to be uploaded                      
                        uploadFiles(sslStream, sessionID, window);
                        break;
                    case END_BACKUP: // No file to upload
                        break;
                    default:
                        // Malformed message
                        throw new Exception("Errore. Comando del server non riconosciuto");
                        break;

                }

                if (closeConnection)
                {
                    // Ask server to close connection 

                    /* Send END_REQUEST to the server: "END_REQUEST?0" */
                    string endMessage = END_REQUEST + "?" + "0";
                    Console.WriteLine(">Client: " + endMessage);
                    endMessage += "\r\n";
                    sslStream.Write(Encoding.UTF8.GetBytes(endMessage));
                    sslStream.Flush();
                }
                //return true; // backup completed successfully

            }
            catch (EmptyBackupFolderException ebfe)
            {
                throw ebfe;
            }
            catch (SessionAuthenticationException sae)
            {

                /* Send END_REQUEST to the server: "END_REQUEST?0" */
                string endMessage = END_REQUEST + "?" + "0";
                Console.WriteLine(">Client: " + endMessage);
                endMessage += "\r\n";
                sslStream.Write(Encoding.UTF8.GetBytes(endMessage));
                sslStream.Flush();

                throw sae;
            }
            catch (IOException ioe)
            {
                throw new Exception("IOException in NewBackup(). Message: " + ioe.Message);
            }
            catch (Exception e)
            {
                throw new Exception("Unexpected exception in NewBackup(). Message: " + e.Message);
            }
        }

        /**
         * Method that asks to the server to restore the files listed in "jsonRequestBackup" string
         */
        public static void GetBackup(SslStream sslStream, string clientID, string sessionID, List<myFileInfo> fileList, MainWindow window)
        {

            try
            {
                if (sslStream == null || fileList == null)
                {
                    throw new Exception("Invalid input parameters");
                    //Console.WriteLine("Invalid server Name/Address");
                    //return false;
                }
                
                /* Select the files that are actually to download from the server (donwload only files different from the current ones) */
                List<myFileInfo> filteredFileList = null;
                filteredFileList = filterFileList(fileList);
                if (filteredFileList == null)
                    throw new Exception("Qualcosa non ha funzionato nel metodo fileterFileList()");

                Console.WriteLine("Numero di file che saranno richiesti al server: " + filteredFileList.Count);

                if (filteredFileList.Count == 0)
                {
                    /* Send END_REQUEST to the server: "END_REQUEST?0" */
                    string closingMessage = END_REQUEST + "?" + "0";
                    Console.WriteLine(">Client: " + closingMessage);
                    closingMessage += "\r\n";
                    sslStream.Write(Encoding.UTF8.GetBytes(closingMessage));
                    sslStream.Flush();
                    
                    return; // The files in the current directory are all equals to files contained in the requested backup
                }


                try
                {
                    Console.WriteLine("Creazione di un nuovo backup...");
                    // Before getting the requested backup, create a new backup
                    bool closeConnection = false;
                    Client.NewBackup(sslStream, clientID, sessionID, closeConnection, window);
                    Console.WriteLine(">Client - Nuovo backup creato.");
                }
                catch(EmptyBackupFolderException ebfe)
                {
                    //ignore exception
                }
                

                Console.WriteLine(">Client - Ripristino del backup richiesto....");
                /* Send GET_BACKUP to the server: "GET_BACKUP?0" */
                string requestMessage = GET_BACKUP + "?" + "0";
                Console.WriteLine(">Client: " + requestMessage);
                requestMessage += "\r\n";
                sslStream.Write(Encoding.UTF8.GetBytes(requestMessage));
                sslStream.Flush();

                /* Receive ACK:0 */
                string responseMessage = ReadMessage(sslStream, "\r\n");
                Console.WriteLine(">Server: " + responseMessage);
                if (!responseMessage.Equals("ACK:0"))
                    throw new Exception("Ricezione ACK 0 fallita. L'invio della richiesta al server non è andato a buon fine ");

                /* Send ClientID AND SessionID */
                string authenticationMessage = clientID + "?" + sessionID;
                Console.WriteLine(">Client: " + authenticationMessage);
                authenticationMessage += "\r\n";
                sslStream.Write(Encoding.UTF8.GetBytes(authenticationMessage));
                sslStream.Flush();

                /* Receive Server Response */
                responseMessage = ReadMessage(sslStream, "\r\n");
                Console.WriteLine(">Server: " + responseMessage);
                switch (responseMessage)
                {
                    case AUTHENTICATION_OK: // Client ID and Session ID are OK
                        break;
                    case AUTHENTICATION_ERR: // Client ID and Session ID are wrong
                        throw new SessionAuthenticationException("Client ID or Session ID are wrong");
                    default: // Unexpected answer from the server
                        throw new Exception("Unexpected answer message received from the server");
                }

                /* Send json list of file to download to the server */
                string jsonFilteredList = JsonConvert.SerializeObject(filteredFileList);
                requestMessage = jsonFilteredList + "\r\n";
                sslStream.Write(Encoding.UTF8.GetBytes(requestMessage));
                sslStream.Flush();
                Console.WriteLine(">Client  (json filtered list): " + jsonFilteredList);

                /* Receive ACK:1 */
                responseMessage = ReadMessage(sslStream, "\r\n");
                Console.WriteLine(">Server: " + responseMessage);
                if (!responseMessage.Equals("ACK:1"))
                    //Console.WriteLine("Invio dell'ultimo messaggio al server non è andato a buon fine.");
                    throw new Exception("Ricezione ACK 1 fallita. L'invio della richiesta al server non è andato a buon fine ");


                /* Download files */
                while (filteredFileList.Count > 0)
                {
                    /* Receive filename (ClientPath + Name) */
                    string fileRelativePath = ReadMessage(sslStream, "\r\n"); //"RELATIVE_PATH/NAME"
                    Console.WriteLine(">Server: " + fileRelativePath);
                    
                    string fileName = Path.GetFileName(fileRelativePath);
                    string relativePath = Path.GetDirectoryName(fileRelativePath);

                    /* Search file in the list */
                    myFileInfo itemFile = filteredFileList.Find(x => x.Name.Equals(fileName) && x.RelativePath.Equals(relativePath)); 
                    if (itemFile == null || itemFile.Name == null)
                        // Server is sending a file that has not been requested
                        throw new Exception("File not requested has been sent");


                    //reduce realtivePath string for UI
                    string UIpath = itemFile.RelativePath;
                    if (UIpath.Length > 20)
                        UIpath = "..." + UIpath.Substring(UIpath.Length - 20);

                    //update UI
                    Application.Current.Dispatcher.Invoke(new Action(() => {
                        window.UpdateProgressText.Text = "Downloading: \"" + UIpath + @"\" + itemFile.Name + "\"";
                        window.UpdateProgressBar.Visibility = Visibility.Visible;
                        window.UpdateProgressBar.Value = 0;
                    }));


                    /* File founded in the list. Send ACK?sessionID:2 */
                    string sendMessage = "ACK" + "?" + sessionID + ":2";
                    Console.WriteLine(">Client: " + sendMessage);
                    sslStream.Write(Encoding.UTF8.GetBytes(sendMessage + "\r\n"));
                    sslStream.Flush();

                    /* Receive file size */
                    string sizeString = ReadMessage(sslStream, "\r\n"); // "size"
                    Console.WriteLine(">Server (File Size in byte): " + sizeString);
                    long fileSize = long.Parse(sizeString);

                    //Console.WriteLine("Filesize ricevuta : " + fileSize + " Byte");

                    /* Send ACK?sessionID:3 */
                    sendMessage = "ACK" + "?" + sessionID + ":3";
                    sslStream.Write(Encoding.UTF8.GetBytes(sendMessage + "\r\n"));
                    sslStream.Flush();
                    Console.WriteLine(">Client: " + sendMessage);

                    /* Receive File */
                    if (!Preferences.BackupFolder.IsSetted())
                        throw new Exception("Client backup directory is not set");

                    string localFilePath = Path.Combine(Preferences.BackupFolder.GetFolder(), fileRelativePath.TrimStart(Path.DirectorySeparatorChar)); // Full Filename: "CLIENT_DIRECTORY\RELATIVE_PATH_CLIENT\FILE_NAME"
                    Console.WriteLine("Sovrascrittura del file: " + localFilePath);
                    if (!receiveFile(sslStream, localFilePath, fileSize, window))
                        //Console.WriteLine("Errore durante la ricezione del file");
                        throw new Exception("Errore durante la ricezione del file.");

                    /* Send ACK?sessionID:4 */
                    sendMessage = "ACK" + "?" + sessionID + ":4";
                    sslStream.Write(Encoding.UTF8.GetBytes(sendMessage + "\r\n"));
                    sslStream.Flush();
                    Console.WriteLine(">Client: " + sendMessage);

                    /* Receive Checksum */
                    string checksumReceived = ReadMessage(sslStream, "\r\n"); // "Checksum"
                    Console.WriteLine(">Server: " + checksumReceived);
                    
                    string checksumComputed = computeSHA1Checksum(localFilePath);
                    if (checksumComputed == null)
                        throw new Exception("Errore nel calcolo del checksum lato server");

                    //Console.WriteLine("Checksum del file '" + Path.GetFileName(localFilePath) + "': " + checksumComputed);

                    // Verify checksum received
                    if (!checksumReceived.Equals(checksumComputed))
                        throw new Exception("Checksum ricevuto diverso dal checksum calcolato");
                    

                    /* Send ACK?sessionID:5 */
                    sendMessage = "ACK" + "?" + sessionID + ":5";
                    sslStream.Write(Encoding.UTF8.GetBytes(sendMessage + "\r\n"));
                    sslStream.Flush();
                    Console.WriteLine(">Client: " + sendMessage);

                    /*  Transfer successfully completed. Remove file from the list of file to download */
                    filteredFileList.Remove(itemFile);
                }

                /* Receive END_BACKUP message */
                string endMessageRcv = ReadMessage(sslStream, "\r\n");
                Console.WriteLine(">Server: " + endMessageRcv);
                if (!endMessageRcv.Equals(END_BACKUP))
                    Console.WriteLine("Qualcosa è andato storto durante il download dei file");

                /* Send END_REQUEST to the server: "END_REQUEST?0" */
                string endMessage = END_REQUEST + "?" + "0";
                Console.WriteLine(">Client: " + endMessage);
                endMessage += "\r\n";
                sslStream.Write(Encoding.UTF8.GetBytes(endMessage));
                sslStream.Flush();
            }
            catch (SessionAuthenticationException sae)
            {

                /* Send END_REQUEST to the server: "END_REQUEST?0" */
                string endMessage = END_REQUEST + "?" + "0";
                Console.WriteLine(">Client: " + endMessage);
                endMessage += "\r\n";
                sslStream.Write(Encoding.UTF8.GetBytes(endMessage));
                sslStream.Flush();

                throw sae;
            }
            catch (IOException ioe)
            {
                throw new Exception("IOException in GetBackup(). Message: '" + ioe.Message + "'.");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
                throw new Exception("Unexpected exception in GetBackup(). Message: '" + e.Message + "'");
            }
        }

        /**
         * Method that receives a file and return 'true' if the transfer is completed successfully, 'false' otherwise
         */
        private static bool receiveFile(SslStream inputStream, string filePath, long fileSize, MainWindow window)
        {
            int recBytes = 0;
            long leftBytes = 0;
            int toRead = 0;
            byte[] buff = new byte[BUFFER_DIM];
            FileStream fs = null;

            try
            {
                // Create Directory if it doesn't exist
                if (!Directory.Exists(Path.GetDirectoryName(filePath)))
                    Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                
                //create empty file or override the existing one
                fs = new FileStream(filePath, FileMode.Create , FileAccess.Write);

                for (leftBytes = fileSize; leftBytes > 0;)
                {
                    //verifico se devo leggere un numero di byte inferiore alla lunghezza del buffer
                    toRead = (int)Math.Min(leftBytes, BUFFER_DIM);

                    //update UI
                    Application.Current.Dispatcher.Invoke(new Action(() => {
                        window.UpdateProgressBar.Value = ((fileSize - leftBytes) * 100) / fileSize;
                    }));

                    recBytes = inputStream.Read(buff, 0, toRead);

                    //verifico se ci sono errori nella ricezione
                    if (recBytes > 0)
                    {
                        //aggiorno il numero di bytes rimasti da leggere
                        leftBytes -= recBytes;

                        fs.Write(buff, 0, recBytes);

                        //Console.WriteLine("\n\t# Download: " + (fileSize - leftBytes) +" / "+fileSize+ " Byte");
                    }
                    else if (recBytes == 0)
                    {
                        //errore di connessione
                        throw new Exception("Connection Error");
                    }

                    Array.Clear(buff, 0, buff.Length);
                }
                Console.WriteLine("# Download File Completato: " + (fileSize - leftBytes) + "/" + fileSize + " Byte");
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in receiveFile(). Error Message: " + e.Message);
                //Console.WriteLine("Stack Trace: " + e.StackTrace);
                return false;
            }
            finally
            {
                // Nel finally si entra sempre, anche quando ci sono i return nel try o nel catch
                if (fs != null)
                {
                    fs.Flush();
                    fs.Close();
                }
            }
            return true;
        }

        /**
         * Method that gets an input list and returns a new list that contains only info related to file with different checksum compared to the list
         */
        private static List<myFileInfo> filterFileList(List<myFileInfo> inputList) // throws Exception
        {
            List<myFileInfo> outputList = new List<myFileInfo>();

            foreach(myFileInfo mfi in inputList)
            {
                // Create local file path
                if (!Preferences.BackupFolder.IsSetted())
                    throw new Exception("Client backup directory is not set");

                string pathFile = Path.Combine(Preferences.BackupFolder.GetFolder(), mfi.RelativePath.TrimStart(Path.DirectorySeparatorChar), mfi.Name.TrimStart(Path.DirectorySeparatorChar)); // CLIENT_DIRECTORY\RELATIVE_PATH\FILENAME
                if (!File.Exists(pathFile))
                {
                    // The file doesn't exists locally, hence it need to be downloaded: add this item to the output list
                    outputList.Add(mfi);
                    continue;
                }
                // Compute checksum 
                string checksumComputed = computeSHA1Checksum(pathFile);
                if (checksumComputed == null)
                    //Console.WriteLine("Errore nel calcolo del checksum");
                    throw new Exception("Errore nel calcolo del checksum");

                // Compare Checksums
                if (!checksumComputed.Equals(mfi.Checksum))
                    outputList.Add(mfi); // Checkusms are different: add this item to the output list
            }

            return outputList;
        }
            
        /**
         * Method that uploads all the file required to the server in order to create a new backup
         */
        private static void uploadFiles(SslStream sslStream, string sessionID, MainWindow window)
        {
            /* Receive the list of file to transfer */
            string filesToUploadString = ReadMessage(sslStream, "\r\n");
            Console.WriteLine(">Server: " + filesToUploadString);

            /* Deserialize the string received */
            List<myFileInfo> fileToUpload = null;
            fileToUpload = JsonConvert.DeserializeObject<List<myFileInfo>>(filesToUploadString);
            if(fileToUpload == null)
                throw new Exception("Error while parsing the json string received");
                
            Console.WriteLine("File da inviare al server: " + fileToUpload.Count);

            /* Process list of files */
            foreach(myFileInfo mf in fileToUpload)
            {
                //reduce realtivePath string for UI
                string UIpath = mf.RelativePath;
                if (UIpath.Length > 20)
                    UIpath = "..." + UIpath.Substring(UIpath.Length - 20);

                //update UI
                Application.Current.Dispatcher.Invoke(new Action(() => {
                    window.UpdateProgressText.Text = "Uploading: \"" + UIpath + @"\" + mf.Name + "\"";
                    window.UpdateProgressBar.Visibility = Visibility.Visible;
                    window.UpdateProgressBar.Value = 0;
                }));
                
                if (!Preferences.BackupFolder.IsSetted())
                    throw new Exception("Client backup directory is not set");

                string filePath = Path.Combine(Preferences.BackupFolder.GetFolder(), mf.RelativePath.TrimStart(Path.DirectorySeparatorChar), mf.Name.TrimStart(Path.DirectorySeparatorChar));
                // Try to access the file
                if (!File.Exists(filePath))
                    throw new Exception("Error: Il file richiesto dal server non esiste. filepath: " + filePath);


                /* Send filename (RelativePath + Name) AND sessionID to the server: "PATH/NAME?sessionID:1" */
                string filenameMessage = Path.Combine(mf.RelativePath, mf.Name.TrimStart(Path.DirectorySeparatorChar)) + "?" + sessionID + ":1";
                Console.WriteLine(">Client: " + filenameMessage);
                filenameMessage += "\r\n";
                sslStream.Write(Encoding.UTF8.GetBytes(filenameMessage));
                sslStream.Flush();

                /* Receive ACK:1 */
                string messageRcv = ReadMessage(sslStream, "\r\n");
                Console.WriteLine(">Server: " + messageRcv);
                if (!messageRcv.Equals("ACK:1"))
                    //Console.WriteLine("Invio dell'ultimo messaggio al server non è andato a buon fine.");
                    throw new Exception("Ricezione ACK:1 fallita. L'invio al server del nome del file non è andato a buon fine ");


                /* Send file size AND Challenge:2 to the server: "size?Challenge:2 */
                FileInfo fi = new FileInfo(filePath);
                long fileSize = fi.Length;
                string sizeMessage = fileSize.ToString() + "?" + sessionID + ":2";
                Console.WriteLine(">Client: " + sizeMessage);
                sizeMessage += "\r\n";
                sslStream.Write(Encoding.UTF8.GetBytes(sizeMessage));
                sslStream.Flush();

                /* Receive ACK:2 */
                messageRcv = ReadMessage(sslStream, "\r\n");
                Console.WriteLine(">Server: " + messageRcv);
                if (!messageRcv.Equals("ACK:2"))
                    throw new Exception("Ricezione ACK:2 fallita. L'invio al server della dimensione del file non è andato a buon fine ");


                /* Send File */
                if (!sendFile(sslStream, filePath, fileSize, window))
                    throw new Exception("Errore durante l'invio del file");

                Console.WriteLine("File inviato.");

                /* Receive ACK:3 */
                messageRcv = ReadMessage(sslStream, "\r\n");
                Console.WriteLine(">Server: " + messageRcv);
                if (!messageRcv.Equals("ACK:3"))
                    throw new Exception("Ricezione ACK:3 fallita. L'invio del file al server non è andato a buon fine ");

                /* Send Checksum AND Challenge:3 */
                string checksum = computeSHA1Checksum(filePath);
                if (checksum == null)
                    throw new Exception("Errore nel calcolo del checksum");

                string checksumMessage = checksum + "?" + sessionID + ":3";
                Console.WriteLine(">Client: " + checksumMessage);
                checksumMessage += "\r\n";
                sslStream.Write(Encoding.UTF8.GetBytes(checksumMessage));
                sslStream.Flush();

                /* Receive ACK:4 */
                messageRcv = ReadMessage(sslStream, "\r\n");
                Console.WriteLine(">Server: " + messageRcv);
                if (!messageRcv.Equals("ACK:4"))
                    throw new Exception("Ricezione ACK:4 fallita. L'invio del checksum al server non è andato a buon fine ");

                //File uploaded. 
                //Process the next file.

            }

            /* Receive END_BACKUP message */
            string endMessageRcv = ReadMessage(sslStream, "\r\n");
            Console.WriteLine(">Server: " + endMessageRcv);
            if (!endMessageRcv.Equals(END_BACKUP))
                //is not necessary to handle the exception (the upload is done correctly)
                Console.WriteLine("Messagio END_BACKUP del server non ricevuto");
        }
 
        /**
         * Method that scans the given folder and return a json screen containing the file list with their info and the timestamp of the screening
         */
        private static string createScreening(string folderPath, out string timeStamp)
        {
            string json = null;

            try
            {
                if (!Preferences.BackupFolder.IsSetted())
                    throw new Exception("Client backup directory is not set");

                List<myFileInfo> fileList = new List<myFileInfo>();
                foreach (string f in Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories))
                {
                    FileInfo file = new FileInfo(f);
                    myFileInfo fileInfo = new myFileInfo();
                    fileInfo.Name = Path.GetFileName(f);
                    fileInfo.RelativePath = getSubPath(Preferences.BackupFolder.GetFolder(), Path.GetFullPath(f));
                    fileInfo.Size = file.Length;
                    fileInfo.TimeStamp = file.LastWriteTime.ToString("yyyyMMddHHmmssffff");
                    string checksum = computeSHA1Checksum(file.FullName);
                    if (checksum != null)
                        fileInfo.Checksum = checksum;
                    //Console.WriteLine("Checksum del file '" + fileInfo.Name + "': "+checksum);
                    fileList.Add(fileInfo);
                }

                // save the timestamp of the screening
                timeStamp = DateTime.Now.ToString("yyyyMMddHHmmssffff");

                if (fileList.Count < 1)
                    throw new EmptyBackupFolderException("La cartella che si vuole sincronizzare è vuota");
                else
                    json = JsonConvert.SerializeObject(fileList);
                

            }
            catch (EmptyBackupFolderException ebfe)
            {
                throw ebfe;
            }
            catch (Exception e)
            {
                Console.WriteLine("General Exception in scanDir(). Message: '" + e.Message + "'");
                Console.WriteLine(e.StackTrace);
                json = null;
                timeStamp = null;
            }

            return json;
        }


        /**
         * Method that return the relative path of the file given its full path and the dir to which the relative path is referred.
         * E.g. 
         *          File Full Path: "C:\Users\miche\Documents\provaClientSync\NuovaCartella\mioFile.txt"
         *          Directory Full Path: "C:\Users\miche\Documents\provaClientSync"
         *          Result:         "provaClientSync\NuovaCartella"
         */
        private static string getSubPath(string currentDirFullPath, string fileFullPath)
        {
            string relativePath = null;
            
            // Remove CLIENT_DIRECTORY path from fileFullPath. Result: "provaClientSync\NuovaCartella\mioFile.txt:
            relativePath = fileFullPath.Substring(currentDirFullPath.Length);

            //Console.WriteLine("Result: " + relativePath);
            
            // Remove file name from the relative path. Result: "provaClientSync\NuovaCartella"
            relativePath = relativePath.Remove(relativePath.IndexOf(Path.GetFileName(relativePath)) - 1, Path.GetFileName(relativePath).Length + 1); // remove also the "\" before the file name
                                                                                                                                                     
            //Console.WriteLine("Result after remove: " + relativePath);

            return relativePath;
        }



        /**
         *  The following method is invoked by the RemoteCertificateValidationDelegate.
         */
        public static bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
                return true;

            Console.WriteLine("Certificate error: {0}", sslPolicyErrors);

            // Do not allow this client to communicate with unauthenticated servers.
            return false;
        }


        /**
         * Metodo che invia il file specificato in input  e ritorna 'true' se l'invio è andato a buon fine, altrimenti 'false'
         */ 
        private static bool sendFile(SslStream outputStream, string filePath, long fileSize, MainWindow window)
        {
            FileStream fs = null;
            try
            {
                fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);

                long sentBytes = 0;
                int toSend = 0;
                byte[] data = new byte[BUFFER_DIM];

                while (sentBytes != fileSize)
                {
                    //se la dimensione del file è troppo grande toSend verrà inizializzato con BUFFER_DIM, quindi non ci sono problemi di perdita di informazioni da long a int
                    toSend = (int)Math.Min((fileSize - sentBytes), BUFFER_DIM);

                    //leggo parte del file nel buffer
                    toSend = fs.Read(data, 0, toSend);
                    
                    //invio i dati
                    outputStream.Write(data, 0, toSend);

                    sentBytes += toSend;

                    //update UI
                    Application.Current.Dispatcher.Invoke(new Action(() => {
                        window.UpdateProgressBar.Value = (sentBytes*100)/fileSize;
                    }));
                    

                    Array.Clear(data, 0, data.Length);
                }

                outputStream.Flush();

                

            }
            catch (FileNotFoundException fne)
            {
                Console.WriteLine("File non trovato!");
                Console.WriteLine(fne.StackTrace);
                return false;
            }
            catch (Exception e)
            {
                //Console.WriteLine("Eccezione generica durante l'invio del file");
                Console.WriteLine(e.StackTrace);
                return false;
            }
            finally
            {
                if (fs != null)
                {
                    fs.Flush();
                    fs.Close();
                }
            }


            return true;

        }

        
        /**
         *  Metodo che calcola il checksum MD5 a partire dal file il cui path è dato in input. 
         *  Ritorna il checksum se tutto va a buon fine, ritorna null in caso di problemi.
         */
        //private static string computeMD5Checksum(string filePath)
        //{
        //    StringBuilder checksumString = null;

        //    try
        //    { 
        //        using (MD5 md5 = MD5.Create())
        //        {
        //            //Console.WriteLine("Calcolo del checksum...");
        //            using (FileStream stream = File.OpenRead(filePath))
        //            {
        //                byte[] checksum = md5.ComputeHash(stream);
        //                //Console.WriteLine("Lunghezza del checksum: " + checksum.Length);

        //                // Create a new Stringbuilder to collect the bytes and create a string.
        //                checksumString = new StringBuilder();

        //                // Convert the byte array to hexadecimal string
        //                for (int i = 0; i < checksum.Length; i++)
        //                {
        //                    checksumString.Append(checksum[i].ToString("x2"));
        //                }

        //            }
        //        }
        //    }catch(Exception e)
        //    {
        //        Console.WriteLine("Exception in computeChecksum(). Stack Trace: ");
        //        Console.WriteLine(e.StackTrace);
        //        return null;
        //    }

        //    if (checksumString != null)
        //        return checksumString.ToString();
        //    else
        //        return null;
        //}

        /**
         *  Metodo che calcola il checksum SHA1 a partire dal file il cui path è dato in input. 
         *  Ritorna il checksum se tutto va a buon fine, ritorna null in caso di problemi.
         */
        private static string computeSHA1Checksum(string filePath)
        {
            StringBuilder formatted = null;
            try
            {
                using (FileStream fs = new FileStream(@filePath, FileMode.Open))
                using (BufferedStream bs = new BufferedStream(fs))
                {
                    using (SHA1Managed sha1 = new SHA1Managed())
                    {
                        byte[] hash = sha1.ComputeHash(bs);
                        formatted = new StringBuilder(2 * hash.Length);
                        foreach (byte b in hash)
                        {
                            formatted.AppendFormat("{0:X2}", b);
                        }

                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in computeChecksum('" + filePath + "'). Error Message: " + e.Message);
                //Console.WriteLine("Stack Trace: "+e.StackTrace);
                return null;
            }
            if (formatted != null)
                return formatted.ToString();
            else
                return null;
        }

        /**
        *  Metodo che calcola la stringa SHA1 a partire dalla stringa data in input.
        *  Ritorna la stringa sha1 se tutto va a buon fine, ritorna null in caso di problemi.
        */
        public static string computeSHA1String(string inputString)
        {
            StringBuilder sha1StringBuilder = null;
            try
            {
                using (SHA1Managed sha1 = new SHA1Managed())
                {
                    byte[] hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(inputString));
                    sha1StringBuilder = new StringBuilder(2 * hash.Length);
                    foreach (byte b in hash)
                    {
                        sha1StringBuilder.AppendFormat("{0:X2}", b);
                    }

                }

            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in computeSHA1String('" + inputString + "'). Error Message: " + e.Message);
                //Console.WriteLine("Stack Trace: "+e.StackTrace);
                return null;
            }
            if (sha1StringBuilder != null)
                return sha1StringBuilder.ToString();
            else
                return null;
        }
        /**
         *  Read the message sent by the server.
         *  The client signals the end of the message using the endLine marker.
         */
        private static string ReadMessage(SslStream sslStream, string endLine)
        {

            byte[] buffer = new byte[2048];
            StringBuilder messageData = new StringBuilder();
            int bytes = -1;
            do
            {
                // Read the server's test message.
                bytes = sslStream.Read(buffer, 0, buffer.Length);

                // Use Decoder class to convert from bytes to UTF8 in case a character spans two buffers.
                Decoder decoder = Encoding.UTF8.GetDecoder();
                char[] chars = new char[decoder.GetCharCount(buffer, 0, bytes)];
                decoder.GetChars(buffer, 0, bytes, chars, 0);
                messageData.Append(chars);
                
                // Check for endLine or an empty message.
                if (messageData.ToString().IndexOf(endLine) != -1)
                {
                    break;
                }
            } while (bytes != 0);

            //remove endLine
            string messageDataString = messageData.ToString();
            messageDataString = messageDataString.TrimEnd(endLine.ToCharArray());

            return messageDataString;
        }

        
    }
}
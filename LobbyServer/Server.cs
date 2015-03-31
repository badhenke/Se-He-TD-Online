using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Timers;


namespace LobbyServer
{
    class Server
    {


        //Path to accounts
        private DirectoryInfo accountsDir = new DirectoryInfo(@"C:\Users\henke\Dropbox\Windows Phone Projects\LobbyLogin\LobbyServer\accounts");
        private DirectoryInfo matchesDir = new DirectoryInfo(@"C:\Users\henke\Dropbox\Windows Phone Projects\LobbyLogin\LobbyServer\matches");
        private DirectoryInfo settingsDir = new DirectoryInfo(@"C:\Users\henke\Dropbox\Windows Phone Projects\LobbyLogin\LobbyServer\serversettings");
        
        private TcpListener tcpListener;
        private Thread listenThread;
        private ASCIIEncoding encoder;
        private int threadName, matchID, bufferSize = 4096;
        private string gameServerIP;

        //Avbildningar för att hålla koll på användares tråd och information från fil
        private Dictionary<int,Thread> threadDict;
        private Dictionary<string, User> activeUserList;

        //Avbildning för profilbilder
        private Dictionary<string, string[]> profileImageList;
        private Dictionary<string, string[]> profileImSendInfo;

        //Håll koll på personer som väntar för att få spela
        private List<string> waitingList = new List<string>();

        //konstruktor
        private Server()
        {
            this.tcpListener = new TcpListener(IPAddress.Any, 64444);
            this.listenThread = new Thread(new ThreadStart(listenForClients));
            this.listenThread.Start();

            encoder = new ASCIIEncoding();
            threadDict = new Dictionary<int,Thread>();
            threadName = 0;
            activeUserList = new Dictionary<string, User>();
            profileImageList = new Dictionary<string, string[]>();
            profileImSendInfo = new Dictionary<string, string[]>();

            loadServerSettings();

            Console.CancelKeyPress += Console_CancelKeyPress;

            Console.WriteLine("#### CLOSE APPLICATION WITH Ctrl + C, NOT WITH EXIT BUTTON #### \n");
        }

        void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            saveCurrentMatchID(matchID);
            Console.Beep();
        }

        //En separat tråd för att lyssna på nya anslutningar
        //Behövs egentligen inte ännu eftersom maintråden inte gör något annat
        private void listenForClients()
        {
            this.tcpListener.Start();

            while (true)
            {
                Console.WriteLine("Lyssnar efter Klienter");
                //Blockerar main tråden tills en ny anslutning har upprättats
                TcpClient client = this.tcpListener.AcceptTcpClient();

                //Skapa tråd för att ta hand om anslutningen med klienten
                //Spara referens till tråden i en threadList
                threadDict.Add(threadName, new Thread(new ParameterizedThreadStart(handleClientComm)));
                threadDict[threadName].Start(client);
                Console.WriteLine("Klienter aktiva: " + threadDict.Count);
            }
        }

        private static void onTimeoutEvent(Object source, ElapsedEventArgs e, ref  TcpClient tcpClient)
        {
            tcpClient.Close();
            
        }

        //Ger varje klientanslutning en egen lyssnartråd
        private void handleClientComm(object client)
        {
            Console.WriteLine("Ny tråd startad");

            TcpClient tcpClient = (TcpClient)client;
            string state = "waiting";
            NetworkStream clientStream = tcpClient.GetStream();
            int localThreadName = threadName;
            threadName++;
            byte[] message = new byte[bufferSize];
            int bytesRead;
            bool startThread = true;
            System.Timers.Timer timeoutTimer = new System.Timers.Timer(30000);
            timeoutTimer.Elapsed += (sender, e) => onTimeoutEvent(sender, e, ref tcpClient);
            timeoutTimer.Enabled = true;
            timeoutTimer.Start();

            //Verifiering för att Lyssnaren ska börja
            bytesRead = clientStream.Read(message, 0, bufferSize);

            
            //Om ej rätt nyckel
            if (encoder.GetString(message, 0, bytesRead) != "4s5c289d89d56d3f63dg8h3b85t")
            {
                startThread = false;
            }
            //Lyssnaren startar
            while (startThread)
            {
                bytesRead = 0;
                try
                {
                    //Blockerar tråden tills ett meddelande har mottagits
                    //Console.WriteLine("Lyssnar efter meddelande");
                    bytesRead = clientStream.Read(message, 0, bufferSize);
                }
                catch
                {
                    //socket error har inträffat
                    break;
                }
                if (bytesRead == 0)
                {
                    //Anslutningen till clienten har brutits
                    break;
                }
                //Meddelande har mottagits
                string encodedMessage = encoder.GetString(message, 0, bytesRead);
                handleMessage(tcpClient, encodedMessage, localThreadName, ref state, ref timeoutTimer);
            }
            
            //En anslutning till klient har dött
            tcpClient.Close();
            clientStream.Close();

            removeUserFromTcpClientInfo(tcpClient);
            
            threadDict.Remove(localThreadName);

            if(state.IndexOf("sendingImage")>=0)
            {
                string[] temp = state.Split(':');
                System.IO.File.Delete(accountsDir.FullName + "\\" + temp[1] + ".txt");
            }

            Console.WriteLine("Avsluta tråd");
            Console.WriteLine("Klienter aktiva: " + threadDict.Count);

        }

        //Hantera olika meddelanden
        private void handleMessage(TcpClient tcpClient, string encodedMessage, int localThreadName, ref string state, ref System.Timers.Timer timeoutTimer)
        {
            timeoutTimer.Stop();
            timeoutTimer.Start();

            string prefix = encodedMessage.Substring(0, encodedMessage.IndexOf(":"));
            string message = encodedMessage.Substring(encodedMessage.IndexOf(":")+1);
            
            //Console.WriteLine("\n State: " + state);
            if(message.IndexOf(prefix)>0)
            {
                send(tcpClient, "Retry:");
                Console.WriteLine("\n werid data. asking for a new send.    " + prefix );
                return;
            }

            //Om en klient vill logga in
            if (prefix == "Login")
            {
                string username = message.Substring(0, message.IndexOf(":"));
                string password = message.Substring(message.IndexOf(":") + 1);

                Console.WriteLine("Login try with username: " + username + " and password: " + password);

                if (userExist(username))
                {
                    User newUser = getUser(tcpClient, username);
                    if (newUser.password != password)
                    {
                        send(tcpClient, "Login:Denied");
                        Console.WriteLine("Login Denied");
                        return;
                    }
                    send(tcpClient, "Login:Success" + ":" + gameServerIP);
                    
                    if(!activeUserList.ContainsKey(username))
                        activeUserList.Add(username, newUser);

                    send(tcpClient, getUserInfo(username));
                    Console.WriteLine("Login Success");
                }
                else
                {
                    send(tcpClient, "Login:Denied");
                    Console.WriteLine("Login Denied");
                }
            }

            //Om en klient vill skapa ett nytt konto
            else if (prefix == "Create")
            {

                string username = message.Substring(0, message.IndexOf(":"));

                if (userExist(username))
                {
                    send(tcpClient, "Create:Denied");
                    Console.WriteLine("CreateNew Denied");
                    return;
                }

                string passwordAndEmailAndImage = message.Substring(message.IndexOf(":") + 1);
                string password = passwordAndEmailAndImage.Substring(0, passwordAndEmailAndImage.IndexOf(":"));
                string email = passwordAndEmailAndImage.Substring(passwordAndEmailAndImage.IndexOf(":") + 1);
                //string email = emailAndImage.Substring(0, emailAndImage.IndexOf(":"));
                //string image = emailAndImage.Substring(emailAndImage.IndexOf(":") + 1);


                state = "sendingImage:" + username;
                createAccountFile(username, password, email);

                send(tcpClient, "Create:Success");
                Console.WriteLine("CreateNew Success. " + "username: " + username);
            }

            //Om en klient vill ha information om en användare
            else if (prefix == "GetUserInfo")
            {

                activeUserList[message] = getUser(tcpClient, message);
                string m = getUserInfo(message);
                send(tcpClient, m);
                Console.WriteLine(m);
                
            }

            //Om en klient vill starta ett nytt spel mot en random
            else if (prefix == "NewGame")
            {
                if (!waitingList.Contains(message))
                {
                    if (waitingList.Count == 0)
                    {
                        waitingList.Add(message);
                        Console.WriteLine("NewGame: Sucess, added to waitingList: " + message);
                        send(tcpClient, "NewGame:Success:WaitingList");
                    }
                    else
                    {
                        List<string> tempPlayerList = new List<string>();
                        tempPlayerList.Add(waitingList[0]); tempPlayerList.Add(message); tempPlayerList.Sort();

                        Console.WriteLine("NewGame: Sucess Started " + tempPlayerList[0] + " vs " + tempPlayerList[1]);
                        createMatchFile(waitingList[0], message, matchID);
                        createNewGame(waitingList[0], message);
                        send(tcpClient, "NewGame:Success:Started");
                    }
                }
                else
                {
                    send(tcpClient, "NewGame:Denied");
                    Console.WriteLine("NewGame: Denied for user: " + message);
                }
            }
            else if (prefix == "StartSendImage")
            {
                Console.WriteLine(prefix + "   " + message);
                if (!profileImSendInfo.ContainsKey(message))
                {
                    Console.WriteLine("profile image send to " + message);
                    
                    string[] lines = System.IO.File.ReadAllLines(accountsDir.FullName + "\\" + message + ".txt");
                    string[] info = new string[4];
                    info[0] = lines[3];
                    info[1] = "" + 0;
                    int temp = (int)Convert.ToInt32(lines[3].Length) / 1750 + 1;
                    info[2] = "" + temp;
                    info[3] = "" + 0; ;
                    profileImSendInfo.Add(message, info);

                    
                    try
                    {
                        send(tcpClient, "SendProfileSize:" + info[2]);
                    }
                    catch
                    {
                        threadDict.Remove(localThreadName);
                        System.IO.File.Delete(accountsDir.FullName + "\\" + message + ".txt");
                        Console.WriteLine("Error on transfer. deleting user: " + message);
                    }
                }
                else 
                {
                    profileImSendInfo.Remove(message);
                }
            }
            else if (prefix == "imready")
            {
                int part = Convert.ToInt32(profileImSendInfo[message][1]);
                int nrOfparts = Convert.ToInt32(profileImSendInfo[message][2]);
                string spart = "";
                int i = Convert.ToInt32(profileImSendInfo[message][3]);

                if (part <= nrOfparts - 1)
                {
                    for (; i < profileImSendInfo[message][0].Length; i++)
                    {
                        spart = spart + profileImSendInfo[message][0][i];
                        if (spart.Length * sizeof(Char) >= 3500)
                        {

                            Console.WriteLine("Sending image to  " + message + "  , size=" + spart.Length + "  part:" + part);
                            send(tcpClient, "SendProfile:" + part + ":" + spart);
                            i++;
                            part++;
                            profileImSendInfo[message][1] = Convert.ToString(part);
                            profileImSendInfo[message][3] = Convert.ToString(i);
                            return; 
                        }
                    }

                    if (part == nrOfparts - 1)
                    {
                        Console.WriteLine("Sending image to  " + message + "  , size=" + spart.Length);
                        send(tcpClient, "SendProfile:" + part + ":" + spart);
                        part++;
                        profileImSendInfo[message][1] = Convert.ToString(part);
                        profileImSendInfo[message][3] = Convert.ToString(i);
                        

                    }
                }
                else if (part == nrOfparts)
                {
                    Console.WriteLine("Sending Done profile, ");
                    send(tcpClient, "DoneProfile:");
                    profileImSendInfo.Remove(message);
                }

            }
            else if(prefix=="SendProfileSize")
            {
                
                string[] msplit = message.Split(':');
                int size = (int)Convert.ToInt32(msplit[1])/1750;
                size++;
                String[] s = new String[size];
                profileImageList.Add(msplit[0], s);
                Console.WriteLine("Recieved profileimage size from: " + message + " number of parts " + size);
                try
                {
                    send(tcpClient, "imready:");
                }
                catch
                {
                    threadDict.Remove(localThreadName);
                    if (userExist(message))
                        System.IO.File.Delete(accountsDir.FullName + "\\" + message + ".txt");
                    Console.WriteLine("Error on transfer. deleting user: " + message);
                }
            }
            else if(prefix == "SendProfile")
            {
                string user = message.Substring(0, message.IndexOf(":"));
                string partanddata = message.Substring(message.IndexOf(":")+1);
                string part = partanddata.Substring(0, partanddata.IndexOf(":"));
                string data = partanddata.Substring(partanddata.IndexOf(":") + 1);

                profileImageList[user][Convert.ToInt32(part)] = data;
                Console.WriteLine("ADD DATA TO " + user + "   part: " + part + "      size: " + data.Length);
                try
                {
                    send(tcpClient, "imready:");
                }
                catch
                {
                    threadDict.Remove(localThreadName);
                    System.IO.File.Delete(accountsDir.FullName + "\\" + user + ".txt");
                    Console.WriteLine("Error on transfer. deleting user: " + user);
                }
            }
            else if (prefix == "DoneProfile")
            {
                string[] data = profileImageList[message];
                string image = "";
                for(int i=0; i<data.Length; i++)
                {
                    image += data[i];
                }

                saveImage(message, image);
                profileImageList.Remove(message);
                Console.WriteLine("Full ProfileImage recieved for " + message);
                state = "waiting";
            }
            else if (prefix == "Kill")
            {
                //En anslutning till klient har dött
                tcpClient.Close();

                removeUserFromTcpClientInfo(tcpClient);

                threadDict.Remove(localThreadName);

            }
        }
        private void saveImage(String username, string image)
        {
            string[] lines = System.IO.File.ReadAllLines(accountsDir.FullName + "\\" + username + ".txt");
            
            if(lines.Length == 3)
            {

                using (StreamWriter writer = new StreamWriter(accountsDir.FullName + "\\" + username + ".txt"))
                {
                    writer.WriteLine(lines[0]);
                    writer.WriteLine(lines[1]);
                    writer.WriteLine(lines[2]);
                    writer.WriteLine(image);
                }

            }
            else
            {
                using (StreamWriter writer = new StreamWriter(accountsDir.FullName + "\\" + username + ".txt"))
                {
                    writer.WriteLine(lines[0]);
                    writer.WriteLine(lines[1]);
                    writer.WriteLine(lines[2]);
                    writer.WriteLine(image);
                    writer.WriteLine(lines[4]);
                }
            }

        }
/*        private void sendImage(String username)
        {
            if (part <= nrOfparts - 1)
            {
                for (; i < base64Image.Length; i++)
                {
                    spart = spart + base64Image[i];
                    if (spart.Length * sizeof(Char) >= 3500)
                    {
                        CommonMethods.send(tcpClient, "SendProfile:" + username + ":" + part + ":" + spart);
                        spart = "";
                        part++;
                        return;
                    }
                }

                if (part == nrOfparts - 1)
                {

                    CommonMethods.send(tcpClient, "SendProfile:" + username + ":" + part + ":" + spart);
                    part++;


                }
            }
            else if (part == nrOfparts)
            {
                CommonMethods.send(tcpClient, "DoneProfile:" + username);
                showMessageBox("Account created", "You can now log in to you new account.");
                sendingImage = false;
            }
        }
*/
        //Skapa ett nytt spel
        private void createNewGame(string player1, string player2)
        {
            
            updateUserFileWithNewMatch(player1, player2, matchID);
            updateUserFileWithNewMatch(player2, player1, matchID);
            
            waitingList.Remove(player1);
            waitingList.Remove(player2);
            matchID++;
        }

        //Create MatchFile
        private void createMatchFile(string player1, string player2, int matchID)
        {
            using (StreamWriter writer = new StreamWriter(matchesDir.FullName + "\\" + matchID + ".txt"))
            {
                //default match info
                writer.WriteLine("" + player1 + ":" + player2);
                writer.WriteLine(player1 + ":");
                writer.WriteLine(player2 + ":");
                writer.WriteLine("1");
                writer.WriteLine("1");
                writer.WriteLine(player1 + ":");
                writer.WriteLine(player2 + ":");
            }
        }

        //Update UserFile
        private void updateUserFileWithNewMatch(string username, string opponent, int matchID)
        {

            User user = getUser(null, username);

            using (StreamWriter writer = new StreamWriter(accountsDir.FullName + "\\" + username + ".txt"))
            {
                writer.WriteLine(user.password);
                writer.WriteLine(user.email);
                writer.WriteLine(user.rating);
                writer.WriteLine(user.image);
                if (user.matches[0] != "")
                {
                    for (int i = 0; user.matches[i] != ""; i++)
                    {
                        writer.Write(user.matches[i] + ":");
                    }
                }
                writer.Write(opponent + "-" + matchID + "-1");

            }

            Console.WriteLine("updateUserFileWithNewMatch " + username + " vs " + opponent + " matchID: " + matchID);
        }

        //returnerar en kompakt string med användarInfo från Userfilen
        private string getUserInfo(string username)
        {
            User user = activeUserList[username];
            string matches = "";

            for (int i = 0; !user.matches[i].Equals(""); i++)
            {
                matches += ":" + user.matches[i];
            }
            return "GetUserInfo:" + user.username + ":" + user.email + ":" + user.rating + matches;
        }

        //Tar bort en User from listan, inparameter är en tcpClient
        private void removeUserFromTcpClientInfo(TcpClient tcpClient)
        {
            string key = null;
            foreach (KeyValuePair<string, User> user in activeUserList)
            {
                if (user.Value.tcpClient == tcpClient)
                {
                    key = user.Key;
                }
            }
            if (key != null)
                activeUserList.Remove(key);
        }

        //Returnerar en User baserat på username
        private User getUser(TcpClient tcpClientLocal, string userName)
        {
            try
            {
                string[] lines = System.IO.File.ReadAllLines(accountsDir.FullName + "\\" + userName + ".txt");
                string[] matches = new string[10];
                if (lines.Length == 5)
                {
                    string message = lines[4];
                    for (int i = 0; i < 10; i++)
                    {
                        if (message.IndexOf(":") >= 0)
                        {
                            matches[i] = message.Substring(0, message.IndexOf(":"));
                            message = message.Substring(message.IndexOf(":") + 1);
                        }
                        else
                        {
                            if (!message.Equals(""))
                            {
                                matches[i] = message;
                                message = "";

                            }
                            else
                            {
                                matches[i] = "";

                            }
                        }
                    }
                }
                else
                {
                    matches[0] = "";
                }
                return new User() { username = userName, password = lines[0], email = lines[1], rating = int.Parse(lines[2]), image = lines[3], matches = matches, tcpClient = tcpClientLocal };

            }
            catch
            {

            }
            return null;

        }

        //Skriver ett användar konto. username.txt. 
        //password
        //email
        private void createAccountFile(string username, string password, string email)
        {
            using (StreamWriter writer = new StreamWriter(accountsDir.FullName + "\\" + username + ".txt"))
            {
                writer.WriteLine(password);
                writer.WriteLine(email);
                writer.WriteLine(1500);
                //writer.WriteLine(image);
            }
        }
        
        //Kollar om en användare redan existerar
        private bool userExist(string username)
        {
                foreach (FileInfo file in accountsDir.GetFiles())
                {
                    if (file.Name.Substring(0, file.Name.Length - 4) == username)
                        return true;
                }
            
            return false;
        }

        //Skickar till klient
        private void send(TcpClient client, string s)
        {
            try
            {
                NetworkStream clientStream = client.GetStream();

                byte[] buffer = encoder.GetBytes(s);

                clientStream.Write(buffer, 0, buffer.Length);
                clientStream.Flush();
            }
            catch(Exception e)
            {
                client.Close();
                if (s.IndexOf("imready") == 0 || s.IndexOf("SendProfileSize")==0)
                    throw new Exception();
            }
        }

        //Loada server settings
        private void loadServerSettings()
        {
            string[] lines = System.IO.File.ReadAllLines(settingsDir.FullName + "\\" + "currentID.txt");
            matchID = int.Parse(lines[0]);
            lines = System.IO.File.ReadAllLines(settingsDir.FullName + "\\" + "gameServerIP.txt");
            gameServerIP = lines[0];
        }

        //Spara ner matchid
        private void saveCurrentMatchID(int id)
        {
            using (StreamWriter writer = new StreamWriter(settingsDir.FullName + "\\" + "currentID.txt"))
            {
                writer.Write(id);
            }
        }

        public static void Main(string[] arg)
        {
            new Server();

        }

    }


}




 


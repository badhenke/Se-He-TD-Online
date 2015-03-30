using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace SocketServer
{
    public class GameServer
    {
        //Path to accounts
        private DirectoryInfo accountsDir = new DirectoryInfo(@"C:\Users\henke\Dropbox\Windows Phone Projects\LobbyLogin\LobbyServer\accounts");
        private DirectoryInfo matchesDir = new DirectoryInfo(@"C:\Users\henke\Dropbox\Windows Phone Projects\LobbyLogin\LobbyServer\matches");
        private DirectoryInfo settingsDir = new DirectoryInfo(@"C:\Users\henke\Dropbox\Windows Phone Projects\LobbyLogin\LobbyServer\serversettings");

        private TcpListener tcpListener;
        private Thread listenThread;

        private int threadName;
        private Dictionary<int, Thread> threadDict;
        private ASCIIEncoding encoder;

        public GameServer()
        {

            this.tcpListener = new TcpListener(IPAddress.Any, 64445);
            this.listenThread = new Thread(new ThreadStart(listenForClients));
            this.listenThread.Start();

            encoder = new ASCIIEncoding();
            threadDict = new Dictionary<int, Thread>();
            threadName = 0;
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

        //Ger varje klientanslutning en egen lyssnartråd
        private void handleClientComm(object client)
        {
            Console.WriteLine("Ny tråd startad");

            TcpClient tcpClient = (TcpClient)client;
            NetworkStream clientStream = tcpClient.GetStream();
            int localThreadName = threadName;
            threadName++;


            byte[] message = new byte[4096];
            int bytesRead = 0;

            bool startThread = true;

            //Lyssnaren startar
            while (startThread)
            {
                bytesRead = 0;
                try
                {
                    //Blockerar tråden tills ett meddelande har mottagits
                    bytesRead = clientStream.Read(message, 0, 4096);

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
                
                handleMessage(tcpClient, encodedMessage, localThreadName);

            }

            //En anslutning till klient har dött
            tcpClient.Close();
            clientStream.Close();

            threadDict.Remove(localThreadName);

            Console.WriteLine("Avsluta tråd");
            Console.WriteLine("Klienter aktiva: " + threadDict.Count);

        }

        //Hantera Meddelanden från klienterna
        public void handleMessage(TcpClient tcpClient, string encodedMessage, int localThreadName)
        {
            string prefix = encodedMessage.Substring(0, encodedMessage.IndexOf(":"));
            string message = encodedMessage.Substring(encodedMessage.IndexOf(":") + 1);

            if (prefix == "Kill")
            {
                //En anslutning till klient har dött
                tcpClient.Close();
                threadDict.Remove(localThreadName);

            }
            else if (prefix == "GetMatchInfo")
            {

                string username = message.Substring(0, message.IndexOf(":"));
                string matchId = message.Substring(message.IndexOf(":") + 1);
                send(tcpClient, "GetMatchInfo:" + ifMyTurn(username, matchId) + ":"+ loadFromMatchFile(username, matchId));
                Console.WriteLine("GetMatchInfo message: " + message + " sending: " + ifMyTurn(username, matchId) + ":"+ loadFromMatchFile(username, matchId));
            }

            else if (prefix == "EndTurn")
            {
                string username = message.Substring(0, message.IndexOf(":"));
                string temp = message.Substring(message.IndexOf(":")+1);
                string matchId = temp.Substring(0, temp.IndexOf(":"));
                //monsterstring
                temp = temp.Substring(temp.IndexOf(":") + 1);

                appendToMatchFile(username,matchId,temp);

                updateTurnInUserFile(username, matchId);
                ifBothDoneChangeActive(username, matchId);
                send(tcpClient, "EndTurn:Done");
                Console.WriteLine("EndTurn Changed in userFile");


            }
            else if (prefix == "UpdateSimDone")
            {
                string username = message.Substring(0, message.IndexOf(":"));
                string matchId = message.Substring(message.IndexOf(":") + 1);
                UpdateSimDone(username, matchId);
                Console.WriteLine("user " + username + " has simulated");
            }
            else if (prefix == "LostGame")
            {
                string username = message.Substring(0, message.IndexOf(":"));
                string matchId = message.Substring(message.IndexOf(":") + 1);
                lostMatch(username, matchId);
                Console.WriteLine("user " + username + " lost a match! MatchId: " + matchId);
            }

            else
            {
                tcpClient.Close();
            }
        }

        //Ladda info om matchen
        private string loadMatchInfo(string matchId)
        {
            string[] lines = System.IO.File.ReadAllLines(matchesDir.FullName + "\\" + matchId +".txt");
            
            return lines[lines.Length-1];
        }

        //Kollar om en match redan existerar
        private bool matchExist(string matchId)
        {
            foreach (FileInfo file in matchesDir.GetFiles())
            {
                if (file.Name.Substring(0, file.Name.Length - 4) == matchId)
                    return true;
            }

            return false;
        }

        //Har båda gjort sina drag? Så skall bådas matcher bli aktiva igen
        private void ifBothDoneChangeActive(string username, string matchId)
        {
            string opponent = getOpponentsName(username, matchId);
            if (!ifMyTurn(username, matchId) && !ifMyTurn(opponent, matchId))
            {
                updateTurnInUserFile(username, matchId);
                updateTurnInUserFile(opponent, matchId);

                bothDoneChangeMatchFile(matchId);

                Console.WriteLine("Change in both files to active");

            }

        }

        //Tar reda på min motståndares namn, genom mitt username och matchId
        private string getOpponentsName(string username, string matchId)
        {
            string[] lines = System.IO.File.ReadAllLines(accountsDir.FullName + "\\" + username + ".txt");
            string temp = lines[4].Substring(0, lines[4].IndexOf("-" + matchId));

            string opponent=null;

            if (temp.Contains(":"))
            {
                opponent = temp.Substring(temp.LastIndexOf(":") + 1);
            }
            else
                opponent = temp;

            return opponent;
        }

        //Har jag ett drag att göra?
        private bool ifMyTurn (string username, string matchId)
        {
            string[] lines = System.IO.File.ReadAllLines(accountsDir.FullName + "\\" + username + ".txt");
            string first = lines[4].Substring(lines[4].IndexOf("-" + matchId));
            string turnStatus = null;

            if (first.Contains(":"))
            {
                string second = first.Substring(0, first.IndexOf(":"));
                turnStatus = second.Substring(second.Length - 1);
            }
            else
            {
                turnStatus = first.Substring(first.Length - 1);
            }

            if (turnStatus == "1")
                return true;
            else
                return false;

        }

        //Om jag har skickat in ett drag så skall det uppdateras i min UserFile
        private void updateTurnInUserFile(string username, string matchId)
        {
            string[] lines = System.IO.File.ReadAllLines(accountsDir.FullName + "\\" + username + ".txt");
            string search = "-" + matchId;
            string first = lines[4].Substring(0, lines[4].IndexOf(search));
            char[] second = lines[4].Substring(lines[4].IndexOf(search) , search.Length +2 ).ToCharArray();
            string third = lines[4].Substring(lines[4].IndexOf(search) + search.Length +2 );

            if (second[second.Length-1] == '1')
            {
                second[second.Length - 1] = '0';
            }
            else if (second[second.Length - 1] == '0')
            {
                second[second.Length - 1] = '1';
            }

            lines[3] = first + new string(second) + third;

            File.WriteAllLines(accountsDir.FullName + "\\" + username + ".txt", lines);
        }

        //Skickar till klient
        private void send(TcpClient client, string s)
        {
            NetworkStream clientStream = client.GetStream();

            byte[] buffer = encoder.GetBytes(s);

            clientStream.Write(buffer, 0, buffer.Length);
            clientStream.Flush();

        }

        //save to matchfile
        private void appendToMatchFile(string username, string matchId, string monsters)
        {
            string[] lines = System.IO.File.ReadAllLines(matchesDir.FullName + "\\" + matchId + ".txt");
            string player1, player2;
 
            player1 = lines[0].Substring(0, lines[0].IndexOf(":"));
            player2 = lines[0].Substring(lines[0].IndexOf(":") + 1);

            if (username == player1)
            {
                lines[5] = player1 + ":" + monsters;

            }
            else
            {
                lines[6] = player2 + ":" + monsters;

            }

            File.WriteAllLines(matchesDir.FullName + "\\" + matchId + ".txt", lines);

        }

        //loada matchfil
        private string loadFromMatchFile(string username, string matchId)
        {
            string[] lines = System.IO.File.ReadAllLines(matchesDir.FullName + "\\" + matchId + ".txt");
            string player1, player2, sendString;

            player1 = lines[0].Substring(0, lines[0].IndexOf(":"));
            player2 = lines[0].Substring(lines[0].IndexOf(":") + 1);

            if (username == player1)
            {
                sendString = lines[2].Substring(lines[2].IndexOf(":") + 1);

                if (int.Parse(lines[3]) == 1)
                    sendString = "True:" + sendString;
                else
                    sendString = "False:" + sendString;

            }
            else
            {
                sendString = lines[1].Substring(lines[1].IndexOf(":") + 1);

                if (int.Parse(lines[4]) == 1)
                    sendString = "True:" + sendString;
                else
                    sendString = "False:" + sendString;
            }
            
            return sendString;
        }

        private void bothDoneChangeMatchFile(string matchId)
        {
            string[] lines = System.IO.File.ReadAllLines(matchesDir.FullName + "\\" + matchId + ".txt");
            
            lines[3] = "" + 1;
            lines[4] = "" + 1;

            lines[1] = lines[5];
            lines[2] = lines[6];

            File.WriteAllLines(matchesDir.FullName + "\\" + matchId + ".txt", lines);

        }

        private void UpdateSimDone(string username, string matchId)
        {
            string[] lines = System.IO.File.ReadAllLines(matchesDir.FullName + "\\" + matchId + ".txt");
            string player1, player2;

            player1 = lines[0].Substring(0, lines[0].IndexOf(":"));
            player2 = lines[0].Substring(lines[0].IndexOf(":") + 1);

            if (username == player1)
            {
                lines[3] = "0";
            }
            else
            {
                lines[4] = "0";
            }

            File.WriteAllLines(matchesDir.FullName + "\\" + matchId + ".txt", lines);

        }

        //Förlorat en match
        private void lostMatch(string username, string matchId)
        {
            
            string opponentName = getOpponentsName(username, matchId);
            string[] lines1 = System.IO.File.ReadAllLines(accountsDir.FullName + "\\" + username + ".txt");
            string[] lines2 = System.IO.File.ReadAllLines(accountsDir.FullName + "\\" + getOpponentsName(username, matchId) + ".txt");

            int rating1 = int.Parse(lines1[2]) - 15;
            int rating2 = int.Parse(lines2[2]) + 15;

            lines1[2] = rating1.ToString();
            lines2[2] = rating2.ToString();
            string f1, f2temp, f2;
            if (lines1[4].IndexOf(":") > 0)
            {
                f1 = lines1[4].Substring(0, lines1[4].IndexOf(opponentName));
                f2temp = lines1[4].Substring(lines1[4].IndexOf(opponentName));

                if (f2temp.IndexOf(":") > 0)
                    f2 = f2temp.Substring(f2temp.IndexOf(":"));
                else
                    f2 = "";

                lines1[4] = f1.Substring(0, f1.Length - 1) + f2;
            }
            else
                lines1[4] = "";

            if (lines2[4].IndexOf(":") > 0)
            {
                f1 = lines2[4].Substring(0, lines2[4].IndexOf(username));
                f2temp = lines2[4].Substring(lines2[4].IndexOf(username));

                if (f2temp.IndexOf(":") > 0)
                    f2 = f2temp.Substring(f2temp.IndexOf(":"));
                else
                    f2 = "";

                lines2[4] = f1.Substring(0, f1.Length - 1) + f2;
            }
            else
                lines2[4] = "";
            

            File.WriteAllLines(accountsDir.FullName + "\\" + username + ".txt", lines1);
            File.WriteAllLines(accountsDir.FullName + "\\" + opponentName + ".txt", lines2);
            System.IO.File.Delete(matchesDir.FullName + "\\" + matchId + ".txt");
        }

        static void Main(string[] args)
        {
            new GameServer();

        }

    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using System.Threading;
using SocketEx;
using System.Windows.Shapes;
using System.IO.IsolatedStorage;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Reflection;
using System.Windows.Threading;


namespace LobbyLogin
{
    public partial class Game : PhoneApplicationPage
    {
                

        
        //functionality var
        private bool sendStatus = false, myTurn, runSimulation;
        private string matchName, matchId, username;
        private ProgressIndicator progressbar;
        private System.Text.UTF8Encoding encoding;
        private IsolatedStorageFile matchFile;

        //Connection variables
        private Thread listeningThread;
        private TcpClient tcpClient;

        private string gameState;
        private int money, income, life;

        public Game()
        {
            InitializeComponent();
            encoding = new System.Text.UTF8Encoding();
            matchFile = IsolatedStorageFile.GetUserStoreForApplication();
            gameState = "game";
        }

        private void PhoneApplicationPage_Loaded_1(object sender, RoutedEventArgs e)
        {
            string queryString = NavigationContext.QueryString["matchInfo"];
            matchId = queryString.Substring(0, queryString.IndexOf(":"));
            string secondQueryString = queryString.Substring(queryString.IndexOf(":") + 1);
            matchName = secondQueryString.Substring(0, secondQueryString.IndexOf(":"));
            username = secondQueryString.Substring(secondQueryString.IndexOf(":") + 1);

            moneyTextBlock.Text = "matchId: " + matchId;

            //progress bar, anonym klass btw
            progressbar = new ProgressIndicator
            {
                IsVisible = true,
                IsIndeterminate = true,
                Text = "Connecting to Game Server.."
            };
            SystemTray.SetProgressIndicator(this, progressbar);

            
            new Thread(delegate()
            {

                string ip = App.gameServerIP.Substring(0, App.gameServerIP.IndexOf(":"));
                int port = int.Parse(App.gameServerIP.Substring(App.gameServerIP.IndexOf(":") + 1));

                System.Diagnostics.Debug.WriteLine("Ansluter..");
                tcpClient = CommonMethods.createConnection(ip, port);
                System.Diagnostics.Debug.WriteLine("Ansluten!");
                
                //Skicka vilken match jag är på!
                CommonMethods.send(tcpClient, "GetMatchInfo:" + username + ":" + matchId);

                //Starta Lyssnartråd när socket anslutning har upprättats
                listeningThread = new Thread(new ThreadStart(listenerThread));
                listeningThread.Start();

            }).Start();

            loadFromStorage();
            
        }

        //"Lyssnar tråd"
        private void listenerThread()
        {
            System.Diagnostics.Debug.WriteLine("Lyssnartråd skapad");
            var stream = tcpClient.GetStream();

            byte[] message = new byte[4096];
            int bytesRead;

            while (true)
            {
                bytesRead = 0;

                try
                {
                    //Blockera tråd tills ett nytt meddelande har mottagits
                    //System.Diagnostics.Debug.WriteLine("Lyssnar efter meddelande");
                    bytesRead = stream.Read(message, 0, 4096);

                }
                catch
                {
                    //Socket error har inträffat
                    break;
                }

                if (bytesRead == 0)
                {

                    //Om anslutningen bryts
                    break;
                }
                //Koda om meddelandet
                string encodedMessage = encoding.GetString(message, 0, bytesRead);
                System.Diagnostics.Debug.WriteLine("Mottagit: " + encodedMessage);
                handleMessage(encodedMessage);
                
            }
            //Avsluta nätverksström och socket anslutning
            showMessageBox("Lost connection to server", "Connection Error!");
            System.Diagnostics.Debug.WriteLine("Avsluta lyssnartråd och socket");
            tcpClient.Dispose();
            tcpClient = null;
            stream.Close();
        }

        public void handleMessage(string encodedMessage)
        {
            string prefix = encodedMessage.Substring(0, encodedMessage.IndexOf(":"));
            string message = encodedMessage.Substring(encodedMessage.IndexOf(":") + 1);

            if (prefix == "GetMatchInfo")
            {
                //Nytt
                myTurn = Boolean.Parse(message.Substring(0, message.IndexOf(":")));
                string tempMessage = message.Substring(message.IndexOf(":") + 1);

                runSimulation = Boolean.Parse(tempMessage.Substring(0, tempMessage.IndexOf(":")));
                tempMessage = tempMessage.Substring(tempMessage.IndexOf(":") + 1);

                //Om senaste simulationen ej har visats, läs in monster och kör simulation
                if (runSimulation == true)
                {

                    gameState = "simulation";
                    Deployment.Current.Dispatcher.BeginInvoke(() =>
                    {
                        progressbar.Text = "Simulating..";
                        changeProgressbarVisible(true);
                        sendButton.IsEnabled = false;
                    });
                       

                }

                Deployment.Current.Dispatcher.BeginInvoke(() =>
                {

                    //skall alltid uppdateras
                    moneyTextBlock.Text = "Money: " + money;
                    IncomeTextBlock.Text = "Income: " + income;
                    LifeTextBlock.Text = "Life: " + life;

                    if (!myTurn)
                    {
                        sendButton.IsEnabled = false;
                    }


                });


     
            }
            else if (prefix == "EndTurn")
            {
                showMessageBox("Turn sent", "Callback from game server");
            }
            sendStatus = true;
            changeProgressbarVisible(false);
        }

        //#######SYNKADE#######
        //visa en messageBox
        private void showMessageBox(string caption, string message)
        {
            //Synkar anropet så båda trådarna kan använda den
            Deployment.Current.Dispatcher.BeginInvoke(() =>
            {
                MessageBox.Show(message, caption, MessageBoxButton.OK);
            });
        }

        //change progressbar
        private void changeProgressbarVisible(bool status)
        {
            Deployment.Current.Dispatcher.BeginInvoke(() =>
            {
                progressbar.IsVisible = status;
            });

        }

        //#####################

        private void PhoneApplicationPage_Unloaded_1(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Kill sent");
            if(tcpClient!=null)
                listeningThread.Abort();
            CommonMethods.send(tcpClient, "Kill:Me");
        }

        private void doneButtonClick(object sender, RoutedEventArgs e)
        {

            if (sendStatus && myTurn && gameState!="simulation")
            {
                string monsterStringToServer = "";

                CommonMethods.send(tcpClient, "EndTurn:" + username + ":" + matchId + ":" + monsterStringToServer);
                sendStatus = false;
                myTurn = false;

                progressbar.Text = "Sending & updating data..";
                changeProgressbarVisible(true);
            }
            else if (!sendStatus)
            {
                showMessageBox("Connection Error", "Connection to gameserver not yet establised!");
            }

        }

                //spara money,life,towers och monsters i minnet
        private void saveAllToStorage()
        {

            StreamWriter sw = new StreamWriter(new IsolatedStorageFileStream(username + matchId, FileMode.Create, matchFile));

            sw.WriteLine(money);
            sw.WriteLine(income);
            sw.WriteLine(life);

            sw.Close();

        }
        //Ladda data från minnet
        private void loadFromStorage()
        {
            //Skapa om match fil ej finns
            if (!matchFile.FileExists(username + matchId))
            {
                IsolatedStorageFileStream dataFile = matchFile.CreateFile(username + matchId);
                dataFile.Close();
                life = 2;
                money = 100;
                income = 0;
            }
            else
            {
                //Läs in matchfil
                StreamReader reader = new StreamReader(new IsolatedStorageFileStream(username + matchId, FileMode.Open, matchFile));
                money = int.Parse(reader.ReadLine());
                income = int.Parse(reader.ReadLine());
                life = int.Parse(reader.ReadLine());
                reader.Close();
            }

            
        }


        //hardware backbutton
        protected override void OnBackKeyPress(System.ComponentModel.CancelEventArgs e)
        {
            
            if (gameState.Equals("game"))
            {
                

                    saveAllToStorage();
                    //var s = "/Profile.xaml?username=" + username;
                    //NavigationService.Navigate(new Uri("/Profile.xaml?username=" + username, UriKind.Relative));
                    //listeningThread.Abort();
                    e.Cancel = false;
             
                
            }
            else if (gameState.Equals("towerpopup"))
            {
                gameState = "game";
                e.Cancel = true;
            }
            else if (gameState.Equals("monsterpopup"))
            {
                gameState = "game";
                e.Cancel = true;
            }
            else if (gameState.Equals("monsterinfopopup"))
            {
                gameState = "monsterpopup";
                e.Cancel = true;
            }
            else if (gameState.Equals("simulation"))
            {
                e.Cancel = false;
            }
            else if (gameState.Equals("lost"))
            {
                e.Cancel = false;
            }

            
        }

    }
}
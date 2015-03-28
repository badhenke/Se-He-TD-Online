using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using SocketEx;
using System.Threading;
using System.Windows.Media;
using System.IO;
using System.Windows.Media.Imaging;
using System.Text;
using System.Windows.Threading;
using System.IO.IsolatedStorage;

namespace LobbyLogin
{
    public partial class Profile : PhoneApplicationPage
    {

        private TcpClient tcpClient;
        private System.Text.UTF8Encoding encoding;
        private Thread listeningThread;
        private string username;
        private ProgressIndicator progressbar;
        private bool sendStatus, sendingImage, waiting4profile;
        private string[] profileImageArray;
        private int nrOfparts;
        DispatcherTimer timer = new DispatcherTimer();
        private System.IO.IsolatedStorage.IsolatedStorageFile localstore = System.IO.IsolatedStorage.IsolatedStorageFile.GetUserStoreForApplication();

        public Profile()
        {
            InitializeComponent();
            encoding = new System.Text.UTF8Encoding();

        }

        private void PhoneApplicationPage_Loaded_1(object sender, RoutedEventArgs e)
        {
            
            changePanoramaIsEnabled(false);
            username = NavigationContext.QueryString["username"];
            profilePanoramaItem.Header = username;
            sendStatus = false;

            //progress bar, anonym klass btw
            progressbar = new ProgressIndicator
            {
                IsIndeterminate = true,
                Text = "Loading userdata.."
            };
            SystemTray.SetProgressIndicator(this, progressbar);


            //Skapar en anonym tråd för att ansluta, så inte UI tråden stoppas.
            //När den har lyckats ansluta mot servern så kommer tcpClient referera till den 
            new Thread(delegate()
            {

                if (App.tcpClient == null)
                {
                    System.Diagnostics.Debug.WriteLine("Ansluter..");
                    App.tcpClient = CommonMethods.createConnection(App.lobbyServerIP, App.lobbyServerPort);
                    System.Diagnostics.Debug.WriteLine("Ansluten!");
                    tcpClient = App.tcpClient;
                    //Skicka en nyckel för att verifiera dig
                    CommonMethods.send(tcpClient, "4s5c289d89d56d3f63dg8h3b85t");
                }
                else
                {
                    tcpClient = App.tcpClient;
                }




                //Starta Lyssnartråd när socket anslutning har upprättats
                listeningThread = new Thread(new ThreadStart(listenerThread));
                listeningThread.Start();

                

            }).Start();

            waiting4profile = true;
            
            timer.Tick += timer_Tick;
            timer.Interval = new TimeSpan(00, 0, 0);
            timer.Start();

            
        }

        void timer_Tick(object sender, object e)
        {
            timer.Interval = new TimeSpan(00, 0, 2);
            if (waiting4profile)
            {
                updateUserInfo();
            }
            if(sendingImage)
            {
                CommonMethods.send(tcpClient, "StartSendImage:" + username);
            }
            
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

                handleMessage(encodedMessage);
                System.Diagnostics.Debug.WriteLine("Mottagit: " + encodedMessage);
            }
            //Avsluta nätverksström och socket anslutning
            showMessageBox("Lost connection to server", "Connection Error!");
            System.Diagnostics.Debug.WriteLine("Avsluta lyssnartråd och socket");
            tcpClient.Dispose();
            tcpClient = null;
            stream.Close();
        }

        //Hantera meddelanden
        private void handleMessage(string encodedMessage)
        {


            string prefix = encodedMessage.Substring(0, encodedMessage.IndexOf(":"));
            string message = encodedMessage.Substring(encodedMessage.IndexOf(":") + 1);

            
            //Fått userinfo av servern
            if (prefix == "GetUserInfo")
            {

                Deployment.Current.Dispatcher.BeginInvoke(() =>
                {
                    timer.Stop();
                });

                List<GamesListBoxItemObject> GameListBoxItemsList = new List<GamesListBoxItemObject>();
                string[] info = new string[10];

                int i = 0;
                for (; message != ""; i++)
                {
                    if (message.IndexOf(":") >= 0)
                    {
                        info[i] = message.Substring(0, message.IndexOf(":"));
                        message = message.Substring(message.IndexOf(":") + 1);
                    }
                    else
                    {
                        info[i] = message;
                        message = "";
                    }
                }

                for (int j = 3; j < i; j++)
                {
                    string opponent = info[j].Substring(0, info[j].IndexOf("-"));
                    int matchId = int.Parse(info[j].Substring(info[j].IndexOf("-") + 1, info[j].Length - info[j].IndexOf("-") - 3));
                    int turnStatus = int.Parse(info[j].Substring(info[j].LastIndexOf("-") + 1));
                    SolidColorBrush background = null;

                    // ? Tydligen är SolidColorBrush som är public static klass threadsafe, så måste skickas till UI Tråden
                    Deployment.Current.Dispatcher.BeginInvoke(() =>
                    {

                        if (turnStatus == 1)
                        {
                            background = new SolidColorBrush(Colors.Green);
                        }
                        else if (turnStatus == 0)
                        {
                            background = new SolidColorBrush(Colors.Red);
                        }

                        GameListBoxItemsList.Add(new GamesListBoxItemObject() { opponent = "You vs " + opponent, matchId = matchId, background = background });

                    });

                }


                //Uppdatera email och rating
                Deployment.Current.Dispatcher.BeginInvoke(() =>
                {
                    emailTextBlock.Text = info[1];
                    ratingTextBlock.Text = info[2];
                    //Uppdatera active games
                    gamesListbox.ItemsSource = GameListBoxItemsList;
                });

                waiting4profile = false;

                //Kontrollera ifall bild finns lokalt
                if (!localstore.FileExists(username+"img"))
                {
                    sendingImage = true;

                    Deployment.Current.Dispatcher.BeginInvoke(() =>
                    {
                        timer.Start();
                    });
                }
                else
                {

                    Deployment.Current.Dispatcher.BeginInvoke(() =>
                    {
                        StreamReader reader = new StreamReader(new IsolatedStorageFileStream(username + "img", FileMode.Open, localstore));
                        string image = reader.ReadLine();
                        reader.Close();
                        byte[] buffer = string2byte(image);
                        Stream s = new MemoryStream(buffer);
                        BitmapImage bmp2 = new BitmapImage();
                        bmp2.SetSource(s);
                        profileimage.Source = bmp2;
                    
                    });

                }

            }

            else if (prefix == "NewGame")
            {
                if (message == "Success:WaitingList")
                {
                    showMessageBox("New Game!", "You are now in waiting que!");
                }
                else if (message == "Denied")
                {
                    showMessageBox("New Game Error!", "You are already in the waiting list!");
                }
                else if (message == "Success:Started")
                {
                    showMessageBox("New Game Found!", "You have found a opponent to play with!");
                    updateUserInfo();
                }

            }
            else if (prefix == "SendProfileSize")
            {
                
                Deployment.Current.Dispatcher.BeginInvoke(() =>
                {
                    timer.Stop();
                });

                profileImageArray = new String[Convert.ToInt32(message)];
                nrOfparts = Convert.ToInt32(message);
                Deployment.Current.Dispatcher.BeginInvoke(() =>
                {
                    changeProgressbarText("Downloading data... " + 0 + "% done");
                });
               
                //Console.WriteLine("Recieved profileimage size from: " + message + " number of parts " + size);
                CommonMethods.send(tcpClient, "imready:"+username);
            }
            else if (prefix == "SendProfile")
            {

                string part = message.Substring(0, message.IndexOf(":"));
                string data = message.Substring(message.IndexOf(":") + 1);
                int ipart = Convert.ToInt32(part);

                Deployment.Current.Dispatcher.BeginInvoke(() =>
                {
                    changeProgressbarText("Downloading data... " + ipart + "% done");
                });

                profileImageArray[ipart] = data;
                CommonMethods.send(tcpClient, "imready:"+username);
            }
            else if (prefix == "DoneProfile")
            {

                string image = "";
                for(int i=0; i<profileImageArray.Length; i++)
                {
                    image += profileImageArray[i];
                }
                int size = image.Length;
                byte[] buffer = string2byte(image);

                Deployment.Current.Dispatcher.BeginInvoke(() =>
                {
                    changeProgressbarText("Downloading data... " + 100 + "% done");
                    Stream s = new MemoryStream(buffer);
                    BitmapImage bmp2 = new BitmapImage();
                    bmp2.SetSource(s);
                    profileimage.Source = bmp2;
                    
                });
                sendingImage = false;

                //Spara hemladdad bild lokalt med username
                StreamWriter sw = new StreamWriter(new IsolatedStorageFileStream(username + "img", FileMode.Create, localstore));
                sw.WriteLine(image);
                sw.Close();
            }



            if (!sendingImage)
            {
                changeProgressbarVisible(false);
                changePanoramaIsEnabled(true);
                sendStatus = true;
            }
        }
        private void PhoneApplicationPage_Unloaded_1(object sender, RoutedEventArgs e)
        {
            listeningThread.Abort();
        }

        private void updateUserInfo()
        {
            changeProgressbarText("Loading userinfo..");
            changeProgressbarVisible(true);
            changePanoramaIsEnabled(false);
            CommonMethods.send(tcpClient, "GetUserInfo:" + username);
        }

        //########## Synkade metoder ##########

        private void navigateToMatch(string matchName, int matchID)
        {
            Deployment.Current.Dispatcher.BeginInvoke(() =>
            {
                NavigationService.Navigate(new Uri("/Game.xaml?matchInfo=" + matchID + ":" + matchName + ":" + username, UriKind.Relative));
            });

        }

        private byte[] string2byte(string s)
        {
            string[] sp = s.Split(',');
            byte[] temp = new byte[s.Length];
            int index = 0;
            for (int i = 0; i < sp.Length; i++)
            {

                    //int t = System.Convert.ToInt32();
                byte byteValue; 
                string ts = Convert.ToString(sp[i]);
                bool result = Byte.TryParse(ts, out byteValue);
                if (result)
                {
                    temp[index] = byteValue;
                    index++;
                }
             
            }
            return temp;
        }


        //change panorama enable
        private void changePanoramaIsEnabled(bool status)
        {
            Deployment.Current.Dispatcher.BeginInvoke(() =>
            {
                panorama.IsEnabled = status;
            });
        }

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

        //change progressbar text
        private void changeProgressbarText(string text)
        {
            Deployment.Current.Dispatcher.BeginInvoke(() =>
            {
                progressbar.Text = text;
            });

        }

        //skicka kommando för att få användar information av servern
        private void AppbarUpdateClick(object sender, EventArgs e)
        {
            //if (!sendStatus)
            //    return;
            updateUserInfo();
            sendStatus = false;
        }

        //skicka kommando för nytt spel
        private void AppbarNewGameClick(object sender, EventArgs e)
        {
            if (!sendStatus)
                return;
            changeProgressbarText("Creating new game..");
            changeProgressbarVisible(true);
            changePanoramaIsEnabled(false);
            CommonMethods.send(tcpClient, "NewGame:" + username);
            sendStatus = false;
        }

        private void gamesListbox_DoubleTap(object sender, System.Windows.Input.GestureEventArgs e)
        {
        ListBoxItem selectedItem = (ListBoxItem) gamesListbox.ItemContainerGenerator.ContainerFromIndex(gamesListbox.SelectedIndex);
            if (selectedItem != null)
            {
                GamesListBoxItemObject t = (GamesListBoxItemObject)selectedItem.Content;
                string name = t.opponent;
                int id = t.matchId;
                //navigera till gamesidan
                navigateToMatch(name, id);
                CommonMethods.send(tcpClient, "Kill:Me");
                listeningThread.Abort();
                tcpClient = null;
                App.tcpClient = null;
            }
        }

        private void ApplicationBarMenuItem_Click_1(object sender, EventArgs e)
        {
            /*System.IO.IsolatedStorage.IsolatedStorageFile store = System.IO.IsolatedStorage.IsolatedStorageFile.GetUserStoreForApplication();
            try
            {

                if (store.FileExists("badhenke12"))
                {
                    store.DeleteFile("badhenke12");
                    showMessageBox("Deleted badhenke12", "");
                }
                else
                {
                    showMessageBox("Fil ej funnen", "");
                }


                if (store.FileExists("jeppe12"))
                {
                    store.DeleteFile("jeppe12");
                    showMessageBox("Deleted jeppe12", "");
                }
                else
                {
                    showMessageBox("Fil ej funnen", "");
                }

            }
            catch (System.IO.IsolatedStorage.IsolatedStorageException ex)
            {
                showMessageBox("Delete failed","");
            }*/
        }

        protected override void OnBackKeyPress(System.ComponentModel.CancelEventArgs e)
        {
            Application.Current.Terminate();
        }


    }
}
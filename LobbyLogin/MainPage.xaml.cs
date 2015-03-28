using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using LobbyLogin.Resources;
using SocketEx;
using System.Threading;
using Microsoft.Phone.Tasks;
using System.IO;
using System.Windows.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Input;
using System.Text;

//Debug meddelande: System.Diagnostics.Debug.WriteLine("App started");

namespace LobbyLogin
{
    public partial class MainPage : PhoneApplicationPage
    {

        private TcpClient tcpClient;
        private System.Text.UTF8Encoding encoding = new System.Text.UTF8Encoding();
        private bool sendStatus;
        private ProgressIndicator progressbar;
        private Thread listeningThread;
        private PhotoChooserTask photoChooserTask;
        private bool exitFromPhotoTask = false, sendingImage=false;
        private TranslateTransform move = new TranslateTransform();
        private BitmapImage profilebmp;
        private string base64Image;


        

        int part = 0;
        string spart = "";
        int i = 0;
        int nrOfparts;
        string username;

        // Constructor
        public MainPage()
        {
            InitializeComponent();

        }

        private void PhoneApplicationPage_Loaded_1(object sender, RoutedEventArgs e)
        {
            
            rectangle.RenderTransform = move;
            progressbar = new ProgressIndicator
            {
                IsVisible = true,
                IsIndeterminate = true,
                Text = "Connecting to server.." + " " + App.lobbyServerIP
            };
            SystemTray.SetProgressIndicator(this, progressbar);
            connect2server();


        }

        private void connect2server()
        {
            //tcpClient = null;
            //progress bar, anonym klass btw




            //Skapar en anonym tråd för att ansluta, så inte UI tråden stoppas.
            //När den har lyckats ansluta mot servern så kommer tcpClient referera till den 
            new Thread(delegate()
            {
                if (tcpClient == null)
                {
                    System.Diagnostics.Debug.WriteLine("Ansluter..");
                    App.tcpClient = CommonMethods.createConnection(App.lobbyServerIP, App.lobbyServerPort);
                    tcpClient = App.tcpClient;
                    //Skicka en nyckel för att verifiera dig
                    CommonMethods.send(tcpClient, "4s5c289d89d56d3f63dg8h3b85t");
                }
                else
                {
                    tcpClient = App.tcpClient;
                }

                System.Diagnostics.Debug.WriteLine("Ansluten");
                changeProgressbarVisible(false);

                //Starta Lyssnartråd när socket anslutning har upprättats
                listeningThread = new Thread(new ThreadStart(listenerThread));
                listeningThread.Start();

            }).Start();

            changePanoramaIsEnabled(true);
            sendStatus = true;


        }

        //Lyssnar tråd
        private void listenerThread()
        {
            bool listen = true;
            var stream = tcpClient.GetStream();

            if (stream == null)
                listen = false;

            byte[] message = new byte[4096];
            int bytesRead;

            while (listen)
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
            if (!exitFromPhotoTask)
            {
                showMessageBox("Lost connection to server", "Connection Error!");
            }
            tcpClient.Dispose();
            tcpClient = null;
            stream.Close();
            connect2server();
        }

        //Hantera meddelanden
        private void handleMessage(string encodedMessage)
        {
            string prefix = encodedMessage.Substring(0, encodedMessage.IndexOf(":"));
            string message = encodedMessage.Substring(encodedMessage.IndexOf(":") + 1);

            if (prefix == "Login")
            {
                if (message == "Denied")
                {
                    showMessageBox("Wrong username or password! If you don't have a account, please create one.", "Login error");
                }
                else if (message.Contains("Success"))
                {
                    App.gameServerIP = message.Substring(message.IndexOf(":") + 1);
                    System.Diagnostics.Debug.WriteLine(App.gameServerIP);
                    navigateToWithUsername("Profile");
                }
            }
            else if (prefix == "Create")
            {
                if (message == "Denied")
                {
                    showMessageBox("User already exist!", "Account error!");
                }
                else if (message == "Success")
                {
                    showMessageBox("Account soon finished", "Click ok to start send over profile image.");
                    clearRegisterFields();
                    sendingImage = true;
                    Deployment.Current.Dispatcher.BeginInvoke(() =>
                    {
                        panorama.DefaultItem = loginPanoramaItem;
                        sendProfileSize();
                    });

                    

                }
            }
            else if(prefix == "imready")
            {

                if (part <= nrOfparts - 1)
                {
                    for (; i < base64Image.Length; i++)
                    {
                        spart = spart + base64Image[i];
                        if (spart.Length * sizeof(Char) >= 3500)
                        {
                            Deployment.Current.Dispatcher.BeginInvoke(() =>
                            {
                                changeProgressbarText("Uploading data... " + part*100 / nrOfparts + "% done");
                            });
                            
                            CommonMethods.send(tcpClient, "SendProfile:" + username + ":" + part + ":" + spart);
                            spart = "";
                            part++;
                            i++;
                            return;
                        }
                    }

                    if (part == nrOfparts - 1)
                    {
                        Deployment.Current.Dispatcher.BeginInvoke(() =>
                        {
                            changeProgressbarText("Uploading data... " + part * 100 / nrOfparts + "% done");
                        });
                        CommonMethods.send(tcpClient, "SendProfile:" + username + ":" + part + ":" + spart);
                        part++;
                        

                    }
                }else if(part == nrOfparts)
                {
                    Deployment.Current.Dispatcher.BeginInvoke(() =>
                    {
                        changeProgressbarText("Uploading data... " + 100 + "% done");
                        profileimage.Source = new BitmapImage(new Uri("/icons/questionmark.png", UriKind.Relative));
                    });
                    CommonMethods.send(tcpClient, "DoneProfile:" + username);
                    showMessageBox("Account created", "You can now log in to you new account.");
                    sendingImage = false;
                }

            }




            if(!sendingImage)
            {
                changePanoramaIsEnabled(true);
                changeProgressbarVisible(false);
                sendStatus = true;
            }
        }

        private void sendProfileSize()
        {

            //Konvertera image till mindre 
            ScaleTransform st = new ScaleTransform()
            {
                ScaleX = 0.8,
                ScaleY = 0.8
            };

            WriteableBitmap wb = new WriteableBitmap(profileimage, st);
            MemoryStream ms = new MemoryStream();
            Byte[] imbuffer = null;
            using (ms = new MemoryStream())
            {
                wb.SaveJpeg(ms, 180, 180, 0, 100);
                imbuffer = ms.ToArray();
            }
            base64Image = byte2string(imbuffer);
            string test = imbuffer[0] + "";
            int size = base64Image.Length;
            nrOfparts = size / 1750 +1;
            //skicka bild
            CommonMethods.send(tcpClient, "SendProfileSize:" + usernameTextbox.Text + ":" + base64Image.Length);
            part = 0;
            spart = "";
            i = 0;
            username = usernameTextbox.Text;


        } 
        


        private string byte2string(byte[] b)
        {
            string temp = "";
            temp += b[0];
            for(int i=1; i< b.Length; i++)
            {
                
                temp += "," + b[i]; 

            }

            return temp;
        }

        //byter texten på progressbaren
        private void changeProgressbarText(string text)
        {
            progressbar.Text = text;
        }

        //########## Synkade metoder #########

        private void navigateToWithUsername(string pagename)
        {

            Deployment.Current.Dispatcher.BeginInvoke(() =>
            {
                var s = "/Profile.xaml?username=" + usernameTextbox.Text;
                NavigationService.Navigate(new Uri("/Profile.xaml?username=" + usernameTextbox.Text, UriKind.Relative));
                listeningThread.Abort();
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

        //clear all Registerfields
        private void clearRegisterFields()
        {

            Deployment.Current.Dispatcher.BeginInvoke(() =>
            {
                usernameTextbox.Text = registerUsernameTextbox.Text;
                passwordTextbox.Password = registerPasswordTextbox.Text;
                registerUsernameTextbox.Text = "";
                registerPasswordTextbox.Text = "";
                registerEmailTextbox.Text = "";
                registerIam13CheckBox.IsChecked = false;
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

        //change panorama enable
        private void changePanoramaIsEnabled(bool status)
        {
            Deployment.Current.Dispatcher.BeginInvoke(() =>
            {
                panorama.IsEnabled = status;
            });
        }

        //########## EVENTS ###########

        //Om tcpClient har någon referens till servern så visa meddelande, annars logga in!
        private void loginButton_Click(object sender, RoutedEventArgs e)
        {
            if (tcpClient == null)
            {
                showMessageBox("Can't connect to server right now", "Connection Error!");
                return;
            }

            //Måste även ha tillåtelse att skicka till servern
            if (!sendStatus)
            {
                return;
            }

            CommonMethods.send(tcpClient, "Login:" + usernameTextbox.Text + ":" + passwordTextbox.Password);
            changeProgressbarText("Logging in..");
            changeProgressbarVisible(true);
            sendStatus = false;
            changePanoramaIsEnabled(false);
        }

        private void newAccount_Click(object sender, RoutedEventArgs e)
        {
            if (tcpClient == null)
            {
                showMessageBox("Can't connect to server right now", "Connection Error!");
                return;
            }

            //Måste även ha tillåtelse att skicka till servern
            if (!sendStatus)
            {
                return;
            }

            //Måste vara över 13 år enl microsoft
            if (registerIam13CheckBox.IsChecked == true)
            {
                
                CommonMethods.send(tcpClient, "Create:" + registerUsernameTextbox.Text + ":" + registerPasswordTextbox.Text + ":" + registerEmailTextbox.Text);
                changeProgressbarText("Preparing account...");
                changeProgressbarVisible(true);
                sendStatus = false;
                changePanoramaIsEnabled(false);

            }
            else
            {
                showMessageBox("You have to be over 13 years old to play this game.", "");
            }

        }


        void photoChooserTask_Completed(object sender, PhotoResult e)
        {
            if (e.TaskResult == TaskResult.OK)
            {
                panorama.IsHitTestVisible = false;
                exitFromPhotoTask = true;
                profilebmp = new BitmapImage();
                profilebmp.SetSource(e.ChosenPhoto);
                setupImage.Source = profilebmp;
                imageStackPanel.Visibility = Visibility.Visible;


            }
            else
            {
                exitFromPhotoTask = true;
            }
        }



        void rect_ManipulationDelta(object sender, ManipulationDeltaEventArgs e)
        {

            move.X += e.DeltaManipulation.Translation.X;
            move.Y += e.DeltaManipulation.Translation.Y;

        }



        private void changeProfileTap(object sender, RoutedEventArgs e)
        {
            photoChooserTask = new PhotoChooserTask();
            photoChooserTask.Completed += new EventHandler<PhotoResult>(photoChooserTask_Completed);
            photoChooserTask.Show();


        }
        //Om man trycker på den fysiska bakåtknappen så ska appen avslutas
        protected override void OnBackKeyPress(System.ComponentModel.CancelEventArgs e)
        {
            Application.Current.Terminate();
            e.Cancel = true;
        }

        private void SetupImageCancel_Click(object sender, RoutedEventArgs e)
        {
            imageStackPanel.Visibility = Visibility.Collapsed;
            panorama.IsHitTestVisible = true;
        }

        private void SetupNext_Click(object sender, RoutedEventArgs e)
        {
            WriteableBitmap WB_CapturedImage;//for original image 
            WriteableBitmap WB_CroppedImage;//for cropped image 
            rectangle.Visibility = Visibility.Collapsed;
            WB_CapturedImage = new WriteableBitmap(imageStackPanel, null);

            using (MemoryStream stream = new MemoryStream())
            {

                WB_CapturedImage.SaveJpeg(stream, (int)setupImage.Width, (int)setupImage.Height, 0, 100);
            }

            rectangle.Visibility = Visibility.Visible;
            // Get the size of the source image 
            double originalImageWidth = WB_CapturedImage.PixelWidth;
            double originalImageHeight = WB_CapturedImage.PixelHeight;
            // Get the size of the image when it is displayed on the phone 
            double displayedWidth = setupImage.ActualWidth;
            double displayedHeight = setupImage.ActualHeight;
            // Calculate the ratio of the original image to the displayed image 
            double widthRatio = 1;// originalImageWidth / displayedWidth;
            double heightRatio = 1;// originalImageHeight / displayedHeight;

            RectangleGeometry geo = new RectangleGeometry();


            GeneralTransform gt = rectangle.TransformToVisual(imageStackPanel);
            Point p = gt.Transform(new Point(0, 0));
            geo.Rect = new Rect(p.X, p.Y, rectangle.Width, rectangle.Height);

            // Create a new WriteableBitmap. The size of the bitmap is the size of the cropping rectangle 
            // drawn by the user, multiplied by the image size ratio. 
            WB_CroppedImage = new WriteableBitmap((int)(widthRatio * 180), (int)(heightRatio * (180)));

            int xoffset = (int)((geo.Rect.X) * widthRatio);
            int yoffset = (int)((geo.Rect.Y) * heightRatio); 

   
                for (int i = 0; i < 32400-1; i++)
                {
                    int x = (int)((i % WB_CroppedImage.PixelWidth) + xoffset);
                    int y = (int)((i / WB_CroppedImage.PixelHeight) + yoffset);
                    int xx = y * WB_CapturedImage.PixelWidth + x;
                    WB_CroppedImage.Pixels[i] = WB_CapturedImage.Pixels[y * WB_CapturedImage.PixelWidth + x];
                }
      


                profileimage.Source = WB_CroppedImage;
                imageStackPanel.Visibility = Visibility.Collapsed;
                panorama.IsHitTestVisible = true;
            

        }
    }
}
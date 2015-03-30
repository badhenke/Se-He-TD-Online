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
                
        //temporary variables
        private Rectangle activeRect;
        private Tower activeTower;
        
        //functionality var
        private bool sendStatus = false, myTurn, runSimulation;
        private string matchName, matchId, username;
        private ProgressIndicator progressbar;
        private System.Text.UTF8Encoding encoding;
        private IsolatedStorageFile matchFile;
        private string gameState;

        //Connection variables
        private Thread listeningThread;
        private TcpClient tcpClient;

        //game variables
        private int money, life, income;
        private int[,] mapArray;

        //Lists
        private List<Tower> towerList;
        private List<Monster> simulationMonsterList, sendMonsterList;
        private List<Tower> TowerListBoxItemsList = new List<Tower>();
        private List<Monster> MonsterListBoxItemsList = new List<Monster>();
        private List<Position> walkPositions;

        private DispatcherTimer timer;

        public Game()
        {
            InitializeComponent();
            encoding = new System.Text.UTF8Encoding();
            matchFile = IsolatedStorageFile.GetUserStoreForApplication();

            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(100);

            timer.Tick += timer_Tick;


            //Lägg in alla torn i shoplistan
            TowerListBoxItemsList.Add(new ArrowTower() { });
            TowerListBoxItemsList.Add(new LongArrowTower() { });
            MonsterListBoxItemsList.Add(new Monster() { });
            MonsterListBoxItemsList.Add(new WeakMonster() { });

            towerListBox.ItemsSource = TowerListBoxItemsList;
            monsterListBox.ItemsSource = MonsterListBoxItemsList;
            monsterSendListBox.ItemsSource = sendMonsterList;

            //Spelar Listor
            towerList = new List<Tower>();
            simulationMonsterList = new List<Monster>();
            sendMonsterList = new List<Monster>();
           
            //set gamestate
            gameState = "game";


            //Initiera gångpunkter
            walkPositions = new List<Position>();

            walkPositions.Add(new Position(0, 60));
            walkPositions.Add(new Position(628, 60));
            walkPositions.Add(new Position(628, 290));
			walkPositions.Add(new Position(475, 290));
			walkPositions.Add(new Position(475, 185));
			walkPositions.Add(new Position(85, 185));
			walkPositions.Add(new Position(85, 420));
			walkPositions.Add(new Position(800, 420));
			
            mapArray = initializePositionsArray(walkPositions);
           
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
                        monsterButton.IsEnabled = false;

                        string monsterName;
                        int x = 60, y = 60, i = 0;
                        int index = tempMessage.IndexOf(",");

                        while (index > 0)
                        {
                            //Tar ut första monstret
                            monsterName = tempMessage.Substring(0, index);

                            Assembly assembly = Assembly.GetExecutingAssembly();
                            Monster tempMonster = assembly.CreateInstance("LobbyLogin." + monsterName) as Monster;


                            tempMonster.setPosition(new Position((-x * i), y));
                            tempMonster.setMap(mapArray);
                            tempMonster.setDxDy(1,0);
                            tempMonster.setCanvas(monsterCanvas);

                            //Lägg in i listan
                            simulationMonsterList.Add(tempMonster);

                            //Resterande monster med det första borttaget
                            tempMessage = tempMessage.Substring(index + 1);
                            index = tempMessage.IndexOf(",");
                            i++;
                        }//While


                        timer.Start();

                    });

                    //Uppdatera Income till money
                    money = money + income;
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
                        monsterButton.IsEnabled = false;
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
            System.Diagnostics.Debug.WriteLine(sendStatus + "," + myTurn);
            if (sendStatus && myTurn && gameState!="simulation")
            {
                string monsterStringToServer = "";

                foreach (Monster monster in sendMonsterList)
                {
                    monsterStringToServer += monster.getName() + ",";
                }
                CommonMethods.send(tcpClient, "EndTurn:" + username + ":" + matchId + ":" + monsterStringToServer);
                sendStatus = false;
                myTurn = false;

                progressbar.Text = "Sending & updating data..";
                changeProgressbarVisible(true);
                popUpMonsterAction.Visibility = System.Windows.Visibility.Collapsed;
            }
            else if (!sendStatus)
            {
                showMessageBox("Connection Error", "Connection to gameserver not yet establised!");
            }

        }

        //När tower rectangle blir tryckt
        private void Rectangle_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            Rectangle rectangle = (Rectangle)sender;
            int pos_x = Grid.GetColumn(rectangle);
            int pos_y = Grid.GetRow(rectangle);

            string s = (string)rectangle.Tag;
            if (s=="empty"  & sendStatus && !gameState.Equals("simulation") )
            {
                
                activeRect = rectangle;

                //popUpTowerAction1.Visibility = System.Windows.Visibility.Visible;
                popUpTowerAction1.Visibility = System.Windows.Visibility.Visible;

                //set gamestatus
                gameState = "towerpopup";

            }


        }

        //#### popUp tower ####

        private void popUpTower1CloseButtonClick(object sender, RoutedEventArgs e)
        {
            popUpTowerAction1.Visibility = System.Windows.Visibility.Collapsed;
            activeRect = null;

            //set gamestatus
            gameState = "game";
        }

        private void popUpTowerAction1ItemTap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            ListBoxItem selectedItem = (ListBoxItem)towerListBox.ItemContainerGenerator.ContainerFromIndex(towerListBox.SelectedIndex);
            

            if (selectedItem != null)
            {
                Tower selectedTower = (Tower)selectedItem.Content;
                
                activeTower = selectedTower;
                popUpTowerAction1TowerName.Text = "Tower name: " + selectedTower.getTowerName();
                popUpTowerAction1TowerCost.Text = "Cost: " + selectedTower.getCost();
                popUpTowerAction1TowerDamage.Text = "Damage: " + selectedTower.getDamage();
                popUpTowerAction1TowerRange.Text = "Range: " + selectedTower.getRange();
                popUpTowerAction1TowerNumOfShoots.Text = "# of Shoots: " + selectedTower.getNumberOfShoots();

            }
        }

        private void popUpTowerAction1BuildButtonClick(object sender, RoutedEventArgs e)
        {
            if (activeRect != null && activeTower != null && (money - activeTower.getCost()) >= 0)
            {
                popUpTowerAction1.Visibility = System.Windows.Visibility.Collapsed;

                string towerName = activeTower.getTowerName();

                Assembly assembly = Assembly.GetExecutingAssembly();
                Tower tempTower = assembly.CreateInstance("LobbyLogin." + towerName) as Tower;
                tempTower.setPosition(new Position(Grid.GetColumn(activeRect) * 45 + 22, Grid.GetRow(activeRect) * 45 + 22));

                towerList.Add(tempTower);

                activeRect.Fill = tempTower.getImageBrush();
                activeRect.Tag = "Built";

                //dra av i money
                money = money - activeTower.getCost();
                moneyTextBlock.Text = "Money: " + money;

            }
        }

        // #######

        //#### popUP monster ####

        private void popUpMonsterCloseButtonClick(object sender, RoutedEventArgs e)
        {
            popUpMonsterAction.Visibility = System.Windows.Visibility.Collapsed;

            //set gamestatus
            gameState = "game";
        }

        private void popUpMonsterActionItemTap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            ListBoxItem selectedItem = (ListBoxItem)monsterListBox.ItemContainerGenerator.ContainerFromIndex(monsterListBox.SelectedIndex);
            if (selectedItem != null)
            {
                Monster selectedMonster = (Monster)selectedItem.Content;

                popUpMonsterActionName.Text = "Name: " + selectedMonster.getName();
                popUpMonsterActionHp.Text = "Hp: " + selectedMonster.getHp();
                popUpMonsterActionCost.Text = "Cost: " + selectedMonster.getCost();
            }
        }

        private void popUpMonsterAddButtonClick(object sender, RoutedEventArgs e)
        {
            ListBoxItem selectedItem = (ListBoxItem)monsterListBox.ItemContainerGenerator.ContainerFromIndex(monsterListBox.SelectedIndex);
            if (selectedItem != null)
            {

                Monster tempMonster = (Monster)selectedItem.Content;

                if (sendStatus && (money - tempMonster.getCost()) >= 0)
                {
                    popUpTowerAction1.Visibility = System.Windows.Visibility.Collapsed;

                    Assembly assembly = Assembly.GetExecutingAssembly();
                    tempMonster = assembly.CreateInstance("LobbyLogin." + tempMonster.getName()) as Monster;

                    sendMonsterList.Add(tempMonster);

                    money = money - tempMonster.getCost();
                    moneyTextBlock.Text = "Money: " + money;

                    income = income + tempMonster.getIncome();
                    IncomeTextBlock.Text = "Income: " + income;

                    monsterSendListBox.ItemsSource = null;
                    monsterSendListBox.ItemsSource = sendMonsterList;

                    monsterListBox.SelectedIndex = -1;
                    monsterSendListBox.SelectedIndex = -1;

                }
            }
        }

        private void monsterButtonClick(object sender, RoutedEventArgs e)
        {
            if (sendStatus && !gameState.Equals("simulation"))
            {
                popUpMonsterAction.Visibility = System.Windows.Visibility.Visible;
                //set gamestatus
                gameState = "monsterpopup";
            }
        }
        
        private void popUpMonsterInfoCloseTap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            popUpMonsterInfo.Visibility = System.Windows.Visibility.Collapsed;

            //set gamestatus
            gameState = "monsterpopup";
        }

        private void infoMonsterButtonClick(object sender, RoutedEventArgs e)
        {
            ListBoxItem selectedItem = (ListBoxItem)monsterListBox.ItemContainerGenerator.ContainerFromIndex(monsterListBox.SelectedIndex);
            if (selectedItem != null)
            {
                popUpMonsterInfo.Visibility = System.Windows.Visibility.Visible;

                Monster selectedMonster = (Monster)selectedItem.Content;

                popUpMonsterActionName.Text = selectedMonster.getName();
                popUpMonsterActionHp.Text = "Hp: " + selectedMonster.getHp();
                popUpMonsterActionCost.Text = "Cost: " + selectedMonster.getCost();

                monsterListBox.SelectedIndex = -1;

                //set gamestatus
                gameState = "monsterinfopopup";

            }
        }

        private void popUpMonsterChangePosClick(object sender, RoutedEventArgs e)
        {
            Button activeButton = (Button)sender;
            
            if (sendMonsterList.Count >= 2)
            {
                int selectedIndex = monsterSendListBox.SelectedIndex;
                if ("down".Equals(activeButton.Tag) && selectedIndex + 1 < sendMonsterList.Count)
                {

                    Monster item = sendMonsterList[selectedIndex];
                    sendMonsterList.RemoveAt(selectedIndex);
                    sendMonsterList.Insert(selectedIndex + 1, item);

                    monsterSendListBox.ItemsSource = null;
                    monsterSendListBox.ItemsSource = sendMonsterList;

                    monsterSendListBox.SelectedIndex = -1;
                }
                else if ("up".Equals(activeButton.Tag) && selectedIndex - 1 >= 0)
                {
                    Monster item = sendMonsterList[selectedIndex];
                    sendMonsterList.RemoveAt(selectedIndex);
                    sendMonsterList.Insert(selectedIndex - 1, item);

                    monsterSendListBox.ItemsSource = null;
                    monsterSendListBox.ItemsSource = sendMonsterList;

                    monsterSendListBox.SelectedIndex = -1;
                }
            }
        }
        // ######

        //spara money,life,towers och monsters i minnet
        private void saveAllToStorage()
        {

            StreamWriter sw = new StreamWriter(new IsolatedStorageFileStream(username + matchId, FileMode.Create, matchFile));

            sw.WriteLine(money);
            sw.WriteLine(income);
            sw.WriteLine(life);

            string saveStringToFile = "";

            foreach (Tower tower in towerList)
            {
                saveStringToFile += tower.ToString();
            }

            //saveStringToFile += ":";

            //foreach (Monster monster in monsterList)
            //{
            //    saveStringToFile += monster.ToString();
            //}

            sw.WriteLine(saveStringToFile); //Wrting to the file
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
                string allTowersString = reader.ReadLine();
                reader.Close();

                string towerString;
                string towerName;
                int x, y;
                int ind = allTowersString.IndexOf(":");


                while (ind > 0)
                {
                    //Tar ut första tornet + pos
                    towerString = allTowersString.Substring(0, ind);

                    towerName = towerString.Substring(0, towerString.IndexOf("-"));
                    towerString = towerString.Substring(towerString.IndexOf("-") + 1);

                    x = int.Parse(towerString.Substring(0, towerString.IndexOf(",")));
                    
                    y = int.Parse(towerString.Substring(towerString.IndexOf(",")+1));
                    

                    Assembly assembly = Assembly.GetExecutingAssembly();
                    Tower tempTower = assembly.CreateInstance("LobbyLogin." + towerName) as Tower;
                    tempTower.setPosition(new Position(x,y));
                    //Lägg in i listan
                    towerList.Add(tempTower);
                    x = (x - 22) / 45;
                    y = (y - 22) / 45;
                    //Uppdatera de grafiska.
                    foreach (Object o in towerGrid.Children)
                    {
                        if (o is Rectangle)
                        {
                            Rectangle rectTemp = (Rectangle)o;
                            if ((int)rectTemp.GetValue(Grid.RowProperty) == y && (int)rectTemp.GetValue(Grid.ColumnProperty) == x)
                            {
                                rectTemp.Fill = tempTower.getImageBrush();
                                rectTemp.Tag = "Built";
                                break;
                            }
                        }

                    }

                    //Resterande torn med det första borttaget
                    allTowersString = allTowersString.Substring(ind+1);
                    ind = allTowersString.IndexOf(":");
                }
                
            }

            
        }

        void timer_Tick(object sender, EventArgs e)
        {
            if (!simulationMonsterList.Any<Monster>())
            {
                timer.Stop();
                changeProgressbarVisible(false);
                gameState = "game";
                CommonMethods.send(tcpClient, "UpdateSimDone:" + username + ":" + matchId);
                Deployment.Current.Dispatcher.BeginInvoke(() =>
                {
                    sendButton.IsEnabled = true;
                    monsterButton.IsEnabled = true;
                });
            }
            if (life <= 0)
            {
                timer.Stop();
                changeProgressbarVisible(false);
                gameState = "lost";
                showMessageBox("Lost", "You lost a match and will decrease in rating");
                CommonMethods.send(tcpClient, "LostGame:" + username + ":" + matchId);
            }

            Simulation();



        }

        private void Simulation()
        {
            Monster targetToRemove;
            
            updateMonsterPosition();

            foreach (Tower tower in towerList)
            {
                //Check if it's any monsters left in the list
                if (simulationMonsterList.Any<Monster>())
                {
                    //fixa, returna tru eller false.
                    tower.setTarget(simulationMonsterList);
                }
                else
                {
                    break;
                }

                tower.updateReloadCount();

                if (tower.checkTarget())
                {
                    targetToRemove = tower.shoot();
                        

                    if (targetToRemove != null)
                    {
                        //uppdatera pengar
                        money = money + targetToRemove.getWorth();
                        moneyTextBlock.Text = "Money: " + money;

                        simulationMonsterList.Remove(targetToRemove);
                        monsterCanvas.Children.Remove(targetToRemove.getImage());
                        
                    }
                }

            }//For Each Tower

                
        }

        private void updateMonsterPosition()
        {
            foreach (Monster monster in simulationMonsterList)
            {
                if (monster.walk())
                {
                    simulationMonsterList.Remove(monster);
                    monsterCanvas.Children.Remove(monster.getImage());
                    life--;
                    LifeTextBlock.Text = "Life: " + life;
                    break;
                }
            }
        }
        
        //Test
        private int[,] initializePositionsArray(List<Position> walkPositions)
        {


            Boolean first = true;
            Position lastPos = null;
            int numRows, row = 0;

            numRows = walkPositions.Count;

            int[,] tempArray = new int[numRows - 1, 4];

            foreach (Position pos in walkPositions)
            {
                if (first)
                {
                    lastPos = pos;
                    first = false;
                }
                else
                {
                    //Calculate the transformation coefficients
                    int x1, x2, y1, y2, z, kx, ky;
                    x1 = lastPos.getX();
                    x2 = pos.getX();
                    y1 = lastPos.getY();
                    y2 = pos.getY();
                    double t = Math.Sqrt(Math.Pow((x2 - x1), 2) + Math.Pow((y2 - y1), 2));
                    z = (int)t;

                    kx = (x2 - x1) / z;
                    ky = (y2 - y1) / z;

                    //Add to array
                    tempArray[row, 2] = kx;
                    tempArray[row, 3] = ky;
                    tempArray[row, 0] = x2;
                    tempArray[row, 1] = y2;

                    lastPos = pos;
                    row++;
                }

            }//Foreach

            return tempArray;

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
                popUpTowerAction1.Visibility = System.Windows.Visibility.Collapsed;
                gameState = "game";
                e.Cancel = true;
            }
            else if (gameState.Equals("monsterpopup"))
            {
                popUpMonsterAction.Visibility = System.Windows.Visibility.Collapsed;
                gameState = "game";
                e.Cancel = true;
            }
            else if (gameState.Equals("monsterinfopopup"))
            {
                popUpMonsterInfo.Visibility = System.Windows.Visibility.Collapsed;
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
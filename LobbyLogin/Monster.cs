using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace LobbyLogin
{
    class Monster
    {
        private Position pos, nextPos, lastPos;
        private int[,] mapArray;
        protected int hp = 40, cost = 20, income = 1, worth = 2, dx, dy, mapRow = 1, lastRow;
        private int speed = 4;
        private Canvas canv;
        public Image imgOrg = new Image();
        public Image img = new Image();
		public Image imgRot90 = new Image();
        public Image imgRot180 = new Image();
        public Image imgRot270 = new Image();

        public string monsterName {get; set;}

        private Rectangle rectangleHp;

        public Monster()
        {

            imgOrg.Source = new ImageSourceConverter().ConvertFromString("/towers/Untitled.png") as ImageSource;
            img = imgOrg;
            monsterName = "Monster";
            rectangleHp = new Rectangle();
            rectangleHp.Fill = new SolidColorBrush(Color.FromArgb(0, 1, 0, 0));
            rectangleHp.Height = 5;
            rectangleHp.Width = 40;

            //Rotate image
            /*
            RotateTransform rot = new RotateTransform();
            //90
            rot.Angle = 90;
            rot.CenterX = 22;
            rot.CenterY = 22;
            //imgRot90 = (WeakMonster)this.MemberwiseClone
            imgRot90.RenderTransform = rot;
            //180
            rot.Angle = 180;
            imgRot180 = imgOrg.me
            imgRot180.RenderTransform = rot;
            //270
            rot.Angle = 270;
            imgRot270 = imgOrg;
            imgRot270.RenderTransform = rot;
            */
        }

        public Monster(Canvas canv, Position startPoint, int dx, int dy)
        {
            this.canv = canv;
            imgOrg.Source = new ImageSourceConverter().ConvertFromString("/towers/Untitled.png") as ImageSource;
            img = imgOrg;
            canv.Children.Add(img);
            img.Visibility = System.Windows.Visibility.Collapsed;
            pos = startPoint;
            this.dx = dx*speed;
            this.dy = dy*speed;

            Canvas.SetLeft(img,startPoint.getX() -22);
            Canvas.SetTop(img, startPoint.getY() - 22);

            rectangleHp = new Rectangle();
            rectangleHp.Fill = new SolidColorBrush(Color.FromArgb(0, 1, 0, 0));
            rectangleHp.Height = 5;
            rectangleHp.Width = 40;
            canv.Children.Add(rectangleHp);
            Canvas.SetLeft(rectangleHp, startPoint.getX() - 22);
            Canvas.SetTop(rectangleHp, startPoint.getY() - 22);
           

        }

        public bool walk()
        {
            if (pos.equal(nextPos, speed/2))
            {
                nextPos = new Position(mapArray[mapRow, 0], mapArray[mapRow, 1]);
                setDxDy(mapArray[mapRow, 2],mapArray[mapRow, 3]);
                mapRow = mapRow + 1;
				
				if(dy > 1)
				{
                    //canv.Children.Remove(img);
                    img = imgOrg;
					//canv.Children.Add(img);
				}
			}



            this.pos.add(dx,dy);

            if (!pos.equal(lastPos, speed / 2))
            {
                Canvas.SetLeft(img, pos.getX() - 22);
                Canvas.SetTop(img, pos.getY() - 22);

                //hp
                Canvas.SetLeft(rectangleHp, Canvas.GetLeft(img) + speed);

                if (img.Visibility == System.Windows.Visibility.Collapsed && pos.getX() >= 0)
                    img.Visibility = System.Windows.Visibility.Visible;
                return false;
            }
            return true;
        }

        public void hit(int damage)
        {
            this.hp = this.hp - damage;
        }

        public int getCost()
        {
            return this.cost;
        }

        public int getHp()
        {
            return this.hp;
        }

        public Image getImage()
        {
            return this.img;
        }

        public int getIncome()
        {
            return this.income;
        }

        public int getWorth()
        {
            return this.worth;
        }

        public Position getPosition()
        {
            return this.pos;
        }
        
        public string getName()
        {
            return this.monsterName;
        }

        public void setPosition(Position pos)
        {
            this.pos = pos;
        }
		
		//Set all initial map properties for the monster
        public void setMap(int[,] map)
        {
            this.mapArray = map;
            nextPos = new Position(mapArray[0, 0], mapArray[0, 1]);
            lastRow = mapArray.GetLength(0);

            lastPos = new Position(mapArray[lastRow-1, 0], mapArray[lastRow-1, 1]); 
        }

        public void setDxDy(int kx,int ky)
        {
            this.dx = kx*speed;
            this.dy = ky*speed;
        }


            
            
        public void setCanvas(Canvas canv)
        {
            this.canv = canv;
            canv.Children.Add(img);
            img.Visibility = System.Windows.Visibility.Collapsed;
            Canvas.SetLeft(img, pos.getX() - 22);
            Canvas.SetTop(img, pos.getY() - 22);

        }

        //public override string ToString()
        //{
        //    return "Position = (" + pos.getX() + "," + pos.getY() + ") HP = " + this.hp; 
        //}
    }
}

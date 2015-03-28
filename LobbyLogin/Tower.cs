using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LobbyLogin
{
    class Tower
    {
        protected Position pos;
        public string towerName { get; set; }
        protected int cost;

        
        //Variabler för skott
        protected int damage;
        private int numberOfShoots = 1000;
        protected int range = 100;
        
        //Variabler för reloading
        private int reloadCount = 0;
        private int reloadTime = 2;
        
        //variabler för target
        private bool hasTarget = false;
        private Monster monsterTarget;

        //Konstruktorer
        public Tower()
        {
            towerName = "Tower";
        }

        public Tower(Position pos)
        {
            this.pos = pos;
            towerName = "Tower";
        }

        //Skjuter ett monster, om det dör returneras monstret
        public Monster shoot() 
        {
            if (canShoot())
            {
                this.monsterTarget.hit(this.damage);
                if (this.monsterTarget.getHp() <= 0)
                {
                    return monsterTarget;
                }

            }
            return null;
        }

        //Returnera om tornet har ett target eller inte
        public bool checkTarget()
        {
            return this.hasTarget;
        }

        //Kollar om den kan skjuta
        public bool canShoot()
        {
            //  Has shoots left,       Has reload
            if (numberOfShoots > 0 & reloadCount <= 0)
            {
                this.numberOfShoots--;
                this.reloadCount = this.reloadTime;
                return true;
            }

            return false;
        }

        //uppdaterar skott nedräkningnen
        public void updateReloadCount()
        {
            this.reloadCount--;
        }

        
        private double calcRange(Position monsterPos)
        {
            return Math.Pow((this.pos.getX() - monsterPos.getX()), 2) + Math.Pow(this.pos.getY() - monsterPos.getY(), 2); 
        }

        //Geters
        public int getCost()
        {
            return cost;
        }

        public int getDamage()
        {
            return damage;
        }

        public int getNumberOfShoots() 
        {
            return numberOfShoots;
        }
        
        public int getRange()
        {
            return range;
        }

        public string getTowerName()
        {
            return towerName;
        }

        public ImageBrush getImageBrush()
        {
            ImageBrush ib = new ImageBrush();
            ib.ImageSource = new BitmapImage(new Uri("/towers/towerpic.png", UriKind.Relative));
            return ib;
        }

        //Setters
        public void setPosition(Position pos)
        {
            this.pos = pos;
        }

        public void setTarget(List<Monster> monsterList)
        {
            foreach (Monster monster in monsterList)
            {

                if (calcRange(monster.getPosition()) > Math.Pow(this.range, 2) | monster.getHp() <= 0)
                {
                    this.hasTarget = false;
                }
                else
                {
                    this.monsterTarget = monster;
                    this.hasTarget = true;
                    break;
                }
            }
        }


        public override string ToString()
        {
            return towerName + "-" + pos.ToString() +":";
        }

    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LobbyLogin
{
    class Position
    {
        private int x;
        private int y;

        public Position(): this(0,0)
        {
      
        }

        public Position(int x, int y)
        {
            this.x = x;
            this.y = y;
        }
        
        public void add(int deltaX, int deltaY)
        {
            this.x = this.x + deltaX;
            this.y = this.y + deltaY;
        }

        public void addX(int deltaX)
        {
            this.x = this.x + deltaX;
        }

        public void addY(int deltaY)
        {
            this.y = this.y + deltaY;
        }

        public int getX()
        {
            return this.x;
        }
        public int getY()
        {
            return this.y;
        }

        public void setPosition(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        public bool equal(Position equalPos, int speed)
        {
            if (x >= equalPos.getX() - speed & x <= equalPos.getX() + speed)
            {
                if (y >= equalPos.getY() - speed & y <= equalPos.getY() + speed)
                {
                    return true;
                }
            }
            return false;

        }

        public override string ToString()
        {
            return getX() + "," + getY();
        }

    }
}

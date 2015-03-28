using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LobbyLogin
{
    class ArrowTower : Tower
    {

        public ArrowTower()
        {
            this.towerName = "ArrowTower";
            this.damage = 2;
            this.cost = 10;

        }

        public ArrowTower(Position pos)
        {
            this.pos = pos;

            this.towerName = "ArrowTower";
            this.damage = 2;
            this.cost = 10;

        }
    }
}

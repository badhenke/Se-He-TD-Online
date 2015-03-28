using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LobbyLogin
{
    class LongArrowTower : Tower
    {

        public LongArrowTower()
        {
            this.towerName = "LongArrowTower";
            this.damage = 2;
            this.cost = 15;
            this.range = 180;
        }

        public LongArrowTower(Position pos)
        {
            this.pos = pos;

            this.towerName = "LongArrowTower";
            this.damage = 2;
            this.cost = 15;
            this.range = 180;
        }
    }
}

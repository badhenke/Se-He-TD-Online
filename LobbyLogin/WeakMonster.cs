using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Shapes;

namespace LobbyLogin
{
    class WeakMonster : Monster
    {
        int maxHp;


        public WeakMonster() 
        {
            this.monsterName = "WeakMonster";
            this.img.Source = new ImageSourceConverter().ConvertFromString("/towers/weakmon.png") as ImageSource;
            this.hp = 20;
            this.cost = 10;
            maxHp = this.getHp();

        }

        public void decreaseHpBar(int damage)
        {


        }

    }
}

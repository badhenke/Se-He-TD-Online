using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace LobbyLogin
{
    class GamesListBoxItemObject
    {
        public string opponent { get; set; }
        public int matchId { get; set; }
        public SolidColorBrush background { get; set; }
    }
}

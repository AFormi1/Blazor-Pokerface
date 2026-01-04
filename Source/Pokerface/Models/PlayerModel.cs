using SQLite;
using System.ComponentModel.DataAnnotations;

namespace Pokerface.Models
{
    public class PlayerModel
    {      

        public string Name { get; set; } = string.Empty;
              
        public PokerCardModel Card1 { get; set; } = new ();
        public PokerCardModel Card2 { get; set; } = new ();

    }


}

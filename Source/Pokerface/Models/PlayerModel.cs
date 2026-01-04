using SQLite;
using System.ComponentModel.DataAnnotations;

namespace Pokerface.Models
{
    public class PlayerModel
    {      

        public int Id { get; set; }
        public int Chair { get; set; }
        public string Name { get; set; } = string.Empty;              
        public PokerCardModel Card1 { get; set; } = new ();
        public PokerCardModel Card2 { get; set; } = new ();

        public PlayerModel(int chair, string name)
        {
            Id = Guid.NewGuid().GetHashCode();

            if (Id < 0)
                Id *= -1;

            Chair = chair;
            Name = name;
        }

    }


}

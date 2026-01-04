
namespace Pokerface.Models
{
    public class PlayerModel
    {    
        public int Id { get; set; }
        public int Chair { get; set; }
        public string Name { get; set; } = string.Empty;
        public Card? Card1 { get; set; }
        public Card? Card2 { get; set; }

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

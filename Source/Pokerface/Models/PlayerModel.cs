
namespace Pokerface.Models
{
    public class PlayerModel
    {    
        public int Id { get; set; }
        public int Chair { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsNext { get; set; }
        public int CurrentBet { get; set; }
        public int RemainingBet { get; set; } = 100;
        public Card? Card1 { get; set; }
        public Card? Card2 { get; set; }

        public delegate void PlayerActionEventHandler(PlayerModel player, PlayerAction action);

        public event PlayerActionEventHandler? PlayerInput;

        public PlayerModel(int chair, string name)
        {
            Id = Guid.NewGuid().GetHashCode();

            if (Id < 0)
                Id *= -1;

            Chair = chair;
            Name = name;
        }

        public void TakeAction(PlayerAction action)
        {
            PlayerInput?.Invoke(this, action);
        }
    }
}

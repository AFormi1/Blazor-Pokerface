
namespace Pokerface.Models
{
    public class PlayerModel
    {    
        public int Id { get; set; }
        public int Chair { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsNext { get; set; }
        public int CurrentBet { get; set; }
        public int RemainingStack { get; set; } = 100;
        public bool HasFolded { get; set; }
        public bool IsSittingOut { get; set; }
        public bool HasPostedSmallBlind { get; set; }
        public bool HasPostedBigBlind { get; set; }
        public bool HasActedThisRound { get; set; }
        public bool AllIn { get; set; }
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

        public void TakeAction(ActionOption option)
        {
             PlayerAction action = new PlayerAction
                {
                    ActionType = option.ActionType,
                    CurrentBet = option.RequiresAmount ? option.SelectedAmount : 0
                };

            PlayerInput?.Invoke(this, action);
        }
    }
}

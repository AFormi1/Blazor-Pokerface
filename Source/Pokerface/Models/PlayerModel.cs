
using System.Numerics;
using System.Xml.Linq;

namespace Pokerface.Models
{
    public class PlayerModel
    {    
        public int Id { get; set; }
        public int Chair { get; set; }
        public string Name { get; set; } = string.Empty;
        public int RemainingStack { get; set; } = 100;
        public bool IsNext { get; set; }
        public int CurrentBet { get; set; }
        public bool HasFolded { get; set; }
        public bool IsSittingOut { get; set; }
        public bool IsLeaving { get; set; }
        public bool HasPostedAnte { get; set; }
        public bool HasPostedSmallBlind { get; set; }
        public bool HasPostedBigBlind { get; set; }
        public bool HasActedThisRound { get; set; }
        public bool AllIn { get; set; }
        public string? Result { get; set; }
        public Card? Card1 { get; set; }
        public Card? Card2 { get; set; }

        public delegate Task PlayerActionEventHandler(PlayerModel player, PlayerAction action);

        public event PlayerActionEventHandler? PlayerInput;
               

        public PlayerModel(int chair, string name)
        {
            Id = Guid.NewGuid().GetHashCode();

            if (Id < 0)
                Id *= -1;

            Chair = chair;
            Name = name;
        }

        //Constructor to set the winner
        public PlayerModel(PlayerModel winner, string winnerHand)
        {
            Name = winner.Name;
            Result = winnerHand;
        }

        public void TakeAction(ActionOption option)
        {
            // Determine the amount to commit for this action
            int amountToCommit = option.RequiredAmount > 0 ? option.SelectedAmount : 0;

            PlayerAction action = new PlayerAction
            {
                ActionType = option.ActionType,
                CurrentBet = amountToCommit
            };

            PlayerInput?.Invoke(this, action);
        }

        public void ResetRoundSettings()
        {
            Card1 = null;
            Card2 = null;

            CurrentBet = 0;
            HasFolded = false;
            HasActedThisRound = false;
            HasPostedSmallBlind = false;
            HasPostedBigBlind = false;
            HasPostedAnte = false;
            AllIn = false;
            IsSittingOut = false;
            IsNext = false;
            Result = string.Empty;
        }
    }
}

using Pokerface.Enums;

namespace Pokerface.Models
{
    public class GameContext
    {
        public int CurrentBet { get; set; }
        public int SmallBlind { get; set; } = 5;
        public int BigBlind { get; set; } = 10;
        public int MinBet { get; set; } = 5;
        public int MaxBet { get; set; } = 10000;
        public int Pot { get; set; }
        public int DealerIndex { get; set; }
        public int SmallBlindIndex { get; set; }
        public int BigBlindIndex { get; set; }
        public bool RoundLocked { get; set; }
        public bool RoundFinished { get; set; }
        public int CurrentPlayer { get; set; }
        public BettingRound CurrentRound { get; set; }

        public List<PlayerModel> Players { get; set; } = new List<PlayerModel>();
        public List<PlayerModel> TheWinners { get; set; } = new();

        public GameContext()
        {
            
        }
        public GameContext(List<PlayerModel> newPlayers, int lastDealerIndex)
        {
            Players = [.. newPlayers];
            RoundLocked = true;
            DealerIndex = (lastDealerIndex + 1) % Players.Count;
            SmallBlindIndex = (DealerIndex + 1) % Players.Count;
            BigBlindIndex = (DealerIndex + 2) % Players.Count;
            TheWinners = new();
        }

    }

}

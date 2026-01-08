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
        public PlayerModel?[] Players { get; set; } = new PlayerModel?[TableModel.MaxPlayers];
        public PlayerModel[] RealPlayers => [.. Players.Where(p => p != null).Select(p => p!)];
        public List<PlayerModel> TheWinners { get; set; } = new();
        public GameContext()
        {
            
        }
        public GameContext(PlayerModel?[] newPlayers, int lastDealerIndex)
        {
            Players = [.. newPlayers];
            RoundLocked = true;
            DealerIndex = (lastDealerIndex + 1) % RealPlayers.Length;
            SmallBlindIndex = (DealerIndex + 1) % RealPlayers.Length;
            BigBlindIndex = (DealerIndex + 2) % RealPlayers.Length;
            TheWinners = new();
        }




    }

}

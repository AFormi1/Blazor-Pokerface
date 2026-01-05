using Pokerface.Enums;

namespace Pokerface.Models
{
    public class GameContext
    {
        public int CurrentBet { get; set; }      
        public int SmallBlind { get; set; } = 5;
        public int BigBlind { get; set; } = 10;
        public int MaxBet { get; set; } = 10000;
        public int Pot { get; set; }

        public BettingRound CurrentRound { get; set; }

        public IReadOnlyList<PlayerModel> Players { get; init; } = [];
    }

}

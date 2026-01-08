using Pokerface.Enums;
using SQLite;
using System.ComponentModel.DataAnnotations;

namespace Pokerface.Models
{
    public class TableModel
    {
        #region DB Properties

        [PrimaryKey, Unique]
        public int Id { get; set; }

        [Required(ErrorMessage = "Name darf nicht leer oder bereits verwendet sein")]
        public string Name { get; set; } = string.Empty;
        public int MinBet { get; set; } = 5;
        public int SmallBlind { get; set; } = 5;
        public int BigBlind { get; set; } = 10;
        public int MaxBet { get; set; } = 10000;

        #endregion

        #region Runtime or Static Properties

        [Ignore]
        public static int MaxPlayers { get; private set; } = 8;
        [Ignore]
        public List<PlayerModel> Players { get; set; } = new List<PlayerModel>();
        [Ignore]
        public List<PlayerModel> TheWinners { get; set; } = new();
        [Ignore]
        public int CurrentPlayer { get; set; }
        [Ignore]
        public BettingRound CurrentRound { get; set; }
        [Ignore]
        public int CurrentPlayers { get; set;  }
        [Ignore]
        public int CurrentBet { get; set; }
        [Ignore]
        public int Pot { get; set; }
        [Ignore]
        public int DealerIndex { get; set; }
        [Ignore]
        public int SmallBlindIndex { get; set; }
        [Ignore]
        public int BigBlindIndex { get; set; }
        [Ignore]
        public bool RoundLocked { get; set; }
        [Ignore]
        public bool RoundFinished { get; set; }

        #endregion

        public TableModel()
        {

        }
        public void RestartRound(List<PlayerModel> newPlayers, int lastDealerIndex)
        {
            Players = [.. newPlayers];
            RoundLocked = true;
            DealerIndex = (lastDealerIndex + 1) % Players.Count;
            SmallBlindIndex = (DealerIndex + 1) % Players.Count;
            BigBlindIndex = (DealerIndex + 2) % Players.Count;
            TheWinners = new();
            CurrentRound = BettingRound.Ante;
            Pot = 0;
            CurrentBet = 0;
            RoundFinished = false;
            CurrentPlayer = 0;
            CurrentPlayers = 0;
        }
    }
}

using Pokerface.Enums;
using Pokerface.Services.DB;

namespace Pokerface.Models
{
    public class GameSessionModel
    {
        private readonly DbTableService? _dbTableService;

        public EventHandler? OnPlayerJoined;
        public EventHandler? OnGameChanged;

        public bool GameLocked { get; set; }

        public int Id { get; set; }

        public TableModel GameTable { get; set; } = new TableModel();

        public List<PlayerModel> Players { get; set; } = new List<PlayerModel>();

        public List<Card>? CardSet { get; set; }

        public GameContext GameSettings { get; set; } = new GameContext();

        public List<ActionOption> AvailableActions { get; private set; } = new();

        public GameSessionModel(DbTableService dbTableService)
        {
            _dbTableService = dbTableService;
        }

        public PlayerModel? GetPlayerById(int player)
        {
            return Players.Where(p => p.Id == player).FirstOrDefault();
        }

        public async Task AddPlayer(PlayerModel player)
        {
            if (_dbTableService == null)
                throw new ArgumentNullException("_dbTableService is null");

            if (Players.Count > GameTable.MaxUsers)
                throw new InvalidOperationException("Cannot add more players than the maximum allowed.");

            Players.Add(player);

            GameTable.CurrentUsers = Players.Count;

            //Update the TableModel in DB
            await _dbTableService.SaveItemAsync(GameTable);

            OnPlayerJoined?.Invoke(this, EventArgs.Empty);

        }

        public async Task RemovePlayer(PlayerModel player)
        {
            if (_dbTableService == null)
                throw new ArgumentNullException("_dbTableService is null");

            Players.Remove(player);

            GameTable.CurrentUsers = Players.Count;

            //Update the TableModel in DB
            await _dbTableService.SaveItemAsync(GameTable);

            OnPlayerJoined?.Invoke(this, EventArgs.Empty);

            //if players is zero, close the game session ???
            if (GameTable.CurrentUsers == 0)
                GameLocked = false;
        }


        private int CurrentPlayer = -1;
        private bool AllPlayersTookAction;

        public void StartGame()
        {
            //Reset the game
            CardSet = CardDeck.GenerateShuffledDeck();
            GameSettings = new();
            AvailableActions = new();

            //subscribe to all player actions
            foreach (var player in Players)
            {
                player.CurrentBet = 0;
                player.RemainingBet = 100;
                player.PlayerInput += OnPlayerActionComitted;
            }

            GameLocked = true;

            //start with the very first player
            if (!Players.Any())
                return;

            CurrentPlayer++;
            Players[CurrentPlayer].IsNext = true;

            // Compute available actions for that player
            UpdateAvailableActions(Players[CurrentPlayer]);

            //give every player two cards
            foreach (var player in Players)
            {
                player.Card1 = CardSet[0];
                CardSet.RemoveAt(0);
                player.Card2 = CardSet[0];
                CardSet.RemoveAt(0);
            }


            OnGameChanged?.Invoke(this, EventArgs.Empty);

            //waiting for player input by event callback
        }



        ////give every player two cards
        //foreach (var player in Players)
        //{
        //    player.Card1 = CardSet[0];
        //    CardSet.RemoveAt(0);
        //    player.Card2 = CardSet[0];
        //    CardSet.RemoveAt(0);

        //    player.IsNext = CurrentPlayer == current;

        //    current++;
        //}


        private void OnPlayerActionComitted(PlayerModel player, PlayerAction action)
        {
            if (!player.IsNext)
                return;

            // Apply action to the game state
            switch (action.ActionType)
            {
                case EnumPlayerAction.None:
                    break;

                case EnumPlayerAction.Fold:
                    player.HasFolded = true; // mark player as folded
                    break;

                case EnumPlayerAction.Check:
                    // nothing changes in bets
                    break;

                case EnumPlayerAction.Call:
                    int callAmount = GameSettings.CurrentBet - player.CurrentBet;
                    player.CurrentBet += callAmount;
                    player.RemainingBet -= callAmount;
                    GameSettings.Pot += callAmount;
                    break;

                case EnumPlayerAction.Bet:
                case EnumPlayerAction.Raise:
                case EnumPlayerAction.ReRaise:
                    player.CurrentBet += action.CurrentBet;
                    player.RemainingBet -= action.CurrentBet;
                    GameSettings.CurrentBet = Math.Max(GameSettings.CurrentBet, player.CurrentBet);
                    GameSettings.Pot += action.CurrentBet;
                    break;

                case EnumPlayerAction.AllIn:
                    player.CurrentBet += player.RemainingBet;
                    GameSettings.CurrentBet = Math.Max(GameSettings.CurrentBet, player.CurrentBet);
                    GameSettings.Pot += player.RemainingBet;
                    player.RemainingBet = 0;
                    break;

                case EnumPlayerAction.PostSmallBlind:
                    player.CurrentBet += GameSettings.SmallBlind;
                    player.RemainingBet -= GameSettings.SmallBlind;
                    GameSettings.Pot += GameSettings.SmallBlind;
                    break;

                case EnumPlayerAction.PostBigBlind:
                    player.CurrentBet += GameSettings.BigBlind;
                    player.RemainingBet -= GameSettings.BigBlind;
                    GameSettings.Pot += GameSettings.BigBlind;
                    break;

                case EnumPlayerAction.PostAnte:
                    player.CurrentBet += GameSettings.SmallBlind; // or ante amount
                    player.RemainingBet -= GameSettings.SmallBlind;
                    GameSettings.Pot += GameSettings.SmallBlind;
                    break;

                case EnumPlayerAction.SitOut:
                    player.IsSittingOut = true;
                    break;

                case EnumPlayerAction.SitIn:
                    player.IsSittingOut = false;
                    break;

                case EnumPlayerAction.Timeout:
                    // optional: handle timeout
                    break;
            }

            // Mark current player as done
            player.IsNext = false;

            // Move to the next active player
            do
            {
                CurrentPlayer = (CurrentPlayer + 1) % Players.Count;
            }
            while (Players[CurrentPlayer].HasFolded || Players[CurrentPlayer].IsSittingOut);

            var nextPlayer = Players[CurrentPlayer];
            nextPlayer.IsNext = true;

            // Compute available actions for the next player
            UpdateAvailableActions(nextPlayer);



            // Refresh UI
            OnGameChanged?.Invoke(this, EventArgs.Empty);
        }


        public void UpdateAvailableActions(PlayerModel player)
        {
            var actions = new List<ActionOption>();

            // Fold is always available
            actions.Add(new ActionOption(EnumPlayerAction.Fold));

            // Check / Call logic
            if (player.CurrentBet < GameSettings.CurrentBet)
                actions.Add(new ActionOption(EnumPlayerAction.Call));
            else
                actions.Add(new ActionOption(EnumPlayerAction.Check));

            // Bet / Raise logic
            if (GameSettings.CurrentBet == 0)
                actions.Add(new ActionOption(EnumPlayerAction.Bet, requiresAmount: true));
            else
                actions.Add(new ActionOption(EnumPlayerAction.Raise, requiresAmount: true));

            // All-in
            if (player.RemainingBet > 0)
                actions.Add(new ActionOption(EnumPlayerAction.AllIn));

            AvailableActions = actions;
        }


    }
}

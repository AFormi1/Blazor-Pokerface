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
        public List<Card>? CommunityCards { get; set; }

        public GameContext CurrentGame { get; set; } = new GameContext();

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
            CurrentGame = new();
            AvailableActions = new();

            CurrentGame.DealerIndex = (CurrentGame.DealerIndex + 1) % Players.Count;
            CurrentGame.SmallBlindIndex = (CurrentGame.DealerIndex + 1) % Players.Count;
            CurrentGame.BigBlindIndex = (CurrentGame.DealerIndex + 2) % Players.Count;


            //subscribe to all player actions
            foreach (var player in Players)
            {
                player.CurrentBet = 0;
                player.RemainingStack = 100;
                player.PlayerInput += OnPlayerActionComitted;
            }

            GameLocked = true;

            //start with the very first player
            if (!Players.Any())
                return;

            CurrentPlayer++;
            Players[CurrentPlayer].IsNext = true;

            // Give all players two cards
            foreach (var player in Players)
            {
                player.Card1 = CardSet[0];
                CardSet.RemoveAt(0);

                player.Card2 = CardSet[0];
                CardSet.RemoveAt(0);
            }


            // Compute available actions for that player
            UpdateAvailableActions(Players[CurrentPlayer]);


            OnGameChanged?.Invoke(this, EventArgs.Empty);

            //waiting for player input by event callback
        }


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
                    player.HasFolded = true;
                    player.HasActedThisRound = true;
                    break;

                case EnumPlayerAction.Check:
                    player.HasActedThisRound = true;
                    break;

                case EnumPlayerAction.Call:
                    int callAmount = CurrentGame.CurrentBet - player.CurrentBet;
                    player.CurrentBet += callAmount;
                    player.RemainingStack -= callAmount;
                    CurrentGame.Pot += callAmount;
                    player.HasActedThisRound = true;
                    break;

                case EnumPlayerAction.Bet:
                case EnumPlayerAction.Raise:
                case EnumPlayerAction.ReRaise:
                    player.CurrentBet += action.CurrentBet;
                    player.RemainingStack -= action.CurrentBet;
                    CurrentGame.CurrentBet = Math.Max(CurrentGame.CurrentBet, player.CurrentBet);
                    CurrentGame.Pot += action.CurrentBet;
                    player.HasActedThisRound = true;
                    break;

                case EnumPlayerAction.AllIn:
                    player.CurrentBet += player.RemainingStack;
                    CurrentGame.CurrentBet = Math.Max(CurrentGame.CurrentBet, player.CurrentBet);
                    CurrentGame.Pot += player.RemainingStack;
                    player.RemainingStack = 0;
                    player.AllIn = true;
                    player.HasActedThisRound = true;
                    break;

                case EnumPlayerAction.PostSmallBlind:
                    player.CurrentBet += CurrentGame.SmallBlind;
                    player.RemainingStack -= CurrentGame.SmallBlind;
                    CurrentGame.Pot += CurrentGame.SmallBlind;
                    player.HasPostedSmallBlind = true;
                    CurrentGame.CurrentBet = Math.Max(CurrentGame.CurrentBet, CurrentGame.SmallBlind);
                    // DO NOT mark HasActedThisRound yet
                    break;

                case EnumPlayerAction.PostBigBlind:
                    player.CurrentBet += CurrentGame.BigBlind;
                    player.RemainingStack -= CurrentGame.BigBlind;
                    CurrentGame.Pot += CurrentGame.BigBlind;
                    player.HasPostedBigBlind = true;
                    CurrentGame.CurrentBet = Math.Max(CurrentGame.CurrentBet, CurrentGame.BigBlind);
                    // DO NOT mark HasActedThisRound yet
                    break;

                case EnumPlayerAction.PostAnte:
                    player.CurrentBet += CurrentGame.SmallBlind; // or ante amount
                    player.RemainingStack -= CurrentGame.SmallBlind;
                    CurrentGame.Pot += CurrentGame.SmallBlind;
                    // DO NOT mark HasActedThisRound yet
                    break;

                case EnumPlayerAction.SitOut:
                    player.IsSittingOut = true;
                    player.HasActedThisRound = true;
                    break;

                case EnumPlayerAction.SitIn:
                    player.IsSittingOut = false;
                    break;

                case EnumPlayerAction.Timeout:
                    // optional: handle timeout
                    player.HasActedThisRound = true;
                    break;
            }

            // Mark current player as done
            player.IsNext = false;

            // Move to next active player
            do
            {
                CurrentPlayer = (CurrentPlayer + 1) % Players.Count;
            } while (Players[CurrentPlayer].HasFolded || Players[CurrentPlayer].IsSittingOut);

            var nextPlayer = Players[CurrentPlayer];
            nextPlayer.IsNext = true;

            // Compute available actions for the next player
            UpdateAvailableActions(nextPlayer);

            // Check if betting round is complete
            if (IsBettingRoundComplete())
            {
                AdvanceRound();
            }

            // Refresh UI
            OnGameChanged?.Invoke(this, EventArgs.Empty);
        }


        public void UpdateAvailableActions(PlayerModel player)
        {
            var actions = new List<ActionOption>();
            int playerIndex = Players.IndexOf(player);

            // Fold is always available
            actions.Add(new ActionOption(EnumPlayerAction.Fold));

            // --- PRE-FLOP BLINDS (manual via UI) ---
            if (CurrentGame.CurrentRound == BettingRound.PreFlop)
            {
                if (playerIndex == CurrentGame.SmallBlindIndex && !player.HasPostedSmallBlind)
                {
                    actions.Add(new ActionOption(EnumPlayerAction.PostSmallBlind));
                    AvailableActions = actions;
                    return;
                }

                if (playerIndex == CurrentGame.BigBlindIndex && !player.HasPostedBigBlind)
                {
                    actions.Add(new ActionOption(EnumPlayerAction.PostBigBlind));
                    AvailableActions = actions;
                    return;
                }
            }

            // --- NORMAL ACTIONS ---
            // Check / Call
            if (player.CurrentBet < CurrentGame.CurrentBet)
                actions.Add(new ActionOption(EnumPlayerAction.Call));
            else
                actions.Add(new ActionOption(EnumPlayerAction.Check));

            // Bet / Raise
            if (CurrentGame.CurrentBet == 0)
                actions.Add(new ActionOption(EnumPlayerAction.Bet, requiresAmount: true));
            else
                actions.Add(new ActionOption(EnumPlayerAction.Raise, requiresAmount: true));

            // All-in
            if (player.RemainingStack > 0)
                actions.Add(new ActionOption(EnumPlayerAction.AllIn));

            AvailableActions = actions;
        }


        private bool IsBettingRoundComplete()
        {
            var activePlayers = Players
                .Where(p => !p.HasFolded && !p.IsSittingOut)
                .ToList();

            // Everyone has acted or is all-in
            if (!activePlayers.All(p => p.HasActedThisRound || p.AllIn))
                return false;

            // Everyone has matched the bet or is all-in
            if (!activePlayers.All(p => p.CurrentBet == CurrentGame.CurrentBet || p.AllIn))
                return false;

            return true;
        }




        private void AdvanceRound()
        {
            // Reset bets
            foreach (var player in Players)
            {
                player.CurrentBet = 0;
            }

            CurrentGame.CurrentBet = 0;

            switch (CurrentGame.CurrentRound)
            {
                case BettingRound.PreFlop:
                    DealFlop();
                    CurrentGame.CurrentRound = BettingRound.Flop;
                    break;

                case BettingRound.Flop:
                    DealTurn();
                    CurrentGame.CurrentRound = BettingRound.Turn;
                    break;

                case BettingRound.Turn:
                    DealRiver();
                    CurrentGame.CurrentRound = BettingRound.River;
                    break;

                case BettingRound.River:
                    CurrentGame.CurrentRound = BettingRound.Showdown;
                    // winner logic later
                    return;
            }

            // First player to act is left of dealer (post-flop)
            CurrentPlayer = (CurrentGame.DealerIndex + 1) % Players.Count;
            Players[CurrentPlayer].IsNext = true;

            UpdateAvailableActions(Players[CurrentPlayer]);
        }


        private void DealFlop()
        {
            if (CardSet == null)
                return;

            BurnCard();
            CommunityCards ??= new List<Card>();

            CommunityCards.Add(CardSet[0]);
            CommunityCards.Add(CardSet[1]);
            CommunityCards.Add(CardSet[2]);

            CardSet.RemoveRange(0, 3);
        }

        private void DealTurn()
        {
            if (CardSet == null)
                return;

            BurnCard();
            CommunityCards!.Add(CardSet[0]);
            CardSet.RemoveAt(0);
        }

        private void DealRiver()
        {
            if (CardSet == null)
                return;

            BurnCard();
            CommunityCards!.Add(CardSet[0]);
            CardSet.RemoveAt(0);
        }

        private void BurnCard()
        {
            if (CardSet == null)
                return;

            CardSet.RemoveAt(0);
        }


    }
}

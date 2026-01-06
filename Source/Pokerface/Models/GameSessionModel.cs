using Pokerface.Components.Card;
using Pokerface.Enums;
using Pokerface.Services;
using Pokerface.Services.DB;
using System.Threading.Tasks;

namespace Pokerface.Models
{
    public class GameSessionModel : IAsyncDisposable
    {
        private readonly DbTableService? _dbTableService;

        public EventHandler? OnPlayerJoined;
        public EventHandler? OnGameChanged;
        public EventHandler? OnRoundFinished;

        public event Func<PlayerModel, Task> OnPlayerLost = player => Task.CompletedTask;
        public int Id { get; set; }

        public TableModel? GameTable { get; set; } = new TableModel();
        public List<PlayerModel>? PlayersPending { get; set; } = new List<PlayerModel>();
        public List<Card>? CardSet { get; set; }
        public List<Card>? CommunityCards { get; set; }
        public GameContext? CurrentGame { get; set; } = new GameContext();
        public List<ActionOption>? AvailableActions { get; private set; } = new();

        public GameSessionModel(DbTableService dbTableService)
        {
            _dbTableService = dbTableService;
        }

        public PlayerModel? GetPlayerById(int player)
        {
            return PlayersPending?.Where(p => p.Id == player).FirstOrDefault();
        }

        public async Task AddPlayer(PlayerModel player)
        {
            if (_dbTableService == null || PlayersPending == null || GameTable == null)
                throw new ArgumentNullException("null objects found in AddPlayer");

            PlayersPending.Add(player);

            GameTable.CurrentPlayers = PlayersPending.Count;

            //Update the TableModel in DB
            await _dbTableService.SaveItemAsync(GameTable);

            OnPlayerJoined?.Invoke(this, EventArgs.Empty);

        }

        public async Task RemovePlayer(PlayerModel player)
        {
            if (_dbTableService == null || PlayersPending == null || GameTable == null || CurrentGame == null)
                throw new ArgumentNullException("null objects found in RemovePlayer");

            // Mark as folded / sitting out
            player.HasFolded = true;
            player.IsSittingOut = true;

            // If the next player is sitting out or left, auto-fold
            if (player.IsNext)
            {
                await OnPlayerActionComitted(player, new PlayerAction { ActionType = EnumPlayerAction.Fold });
                return;
            }

            PlayersPending.Remove(player);
            GameTable.CurrentPlayers = PlayersPending.Count();

            // Update DB
            await _dbTableService.SaveItemAsync(GameTable);

            OnPlayerJoined?.Invoke(this, EventArgs.Empty);

            // If everyone left, close the session
            if (PlayersPending.All(p => p.IsSittingOut))
            {
                CurrentGame.RoundLocked = false;
                CurrentGame.RoundFinished = false;
            }
        }



        public async Task StartGame()
        {
            if (_dbTableService == null || PlayersPending == null || GameTable == null || CurrentGame == null)
                throw new ArgumentNullException("null objects found");

            //Reset the game
            CardSet = CardDeck.GenerateShuffledDeck();
            CommunityCards = new List<Card>();
            //Add all players from the list to the current game
            CurrentGame = new(PlayersPending);
            AvailableActions = new();

            //start with the very first player
            if (CurrentGame.Players.Count < 2)
                return;

            //subscribe to all player actions
            foreach (var player in CurrentGame.Players)
            {
                player.ResetRoundSettings();
                player.PlayerInput -= OnPlayerActionComitted;
                player.PlayerInput += OnPlayerActionComitted;

                player.Card1 = CardSet[0];
                CardSet.RemoveAt(0);

                player.Card2 = CardSet[0];
                CardSet.RemoveAt(0);
            }

            CurrentGame.CurrentPlayer = (CurrentGame.BigBlindIndex + 1) % CurrentGame.Players.Count;
            CurrentGame.Players[CurrentGame.CurrentPlayer].IsNext = true;
            UpdateAvailableActions(CurrentGame.Players[CurrentGame.CurrentPlayer]);

            OnGameChanged?.Invoke(this, EventArgs.Empty);

            //waiting for player input by event callback
        }


        private async Task OnPlayerActionComitted(PlayerModel player, PlayerAction action)
        {
            if (_dbTableService == null || PlayersPending == null || GameTable == null || CurrentGame == null)
                throw new ArgumentNullException("null objects found");

            if (!player.IsNext)
                return;

            // --- Apply player's action ---
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

                case EnumPlayerAction.SmallBlind:
                    player.CurrentBet += CurrentGame.SmallBlind;
                    player.RemainingStack -= CurrentGame.SmallBlind;
                    CurrentGame.Pot += CurrentGame.SmallBlind;
                    player.HasPostedSmallBlind = true;
                    CurrentGame.CurrentBet = Math.Max(CurrentGame.CurrentBet, CurrentGame.SmallBlind);
                    break;

                case EnumPlayerAction.BigBlind:
                    player.CurrentBet += CurrentGame.BigBlind;
                    player.RemainingStack -= CurrentGame.BigBlind;
                    CurrentGame.Pot += CurrentGame.BigBlind;
                    player.HasPostedBigBlind = true;
                    CurrentGame.CurrentBet = Math.Max(CurrentGame.CurrentBet, CurrentGame.BigBlind);
                    break;

                case EnumPlayerAction.PostAnte:
                    player.CurrentBet += CurrentGame.SmallBlind; // or ante amount
                    player.RemainingStack -= CurrentGame.SmallBlind;
                    CurrentGame.Pot += CurrentGame.SmallBlind;
                    break;

                case EnumPlayerAction.SitOut:
                    player.IsSittingOut = true;
                    player.HasActedThisRound = true;
                    break;

                case EnumPlayerAction.SitIn:
                    player.IsSittingOut = false;
                    break;

                case EnumPlayerAction.Timeout:
                    player.HasActedThisRound = true;
                    break;
            }

            // Mark current player as done
            player.IsNext = false;

            // --- Check if only one active player remains ---
            var activePlayers = CurrentGame.Players.Where(p => !p.HasFolded && !p.IsSittingOut).ToList();
            if (activePlayers.Count == 1)
            {
                // Only one player left: assign full pot and finish the round
                await CalculateWinner();
                return;
            }

            // --- Check if all remaining active players are all-in ---
            if (activePlayers.All(p => p.AllIn))
            {
                // Deal all remaining community cards immediately
                switch (CurrentGame.CurrentRound)
                {
                    case BettingRound.PreFlop:
                        GamePlayHelpers.DealFlop(CardSet, CommunityCards);
                        CurrentGame.CurrentRound = BettingRound.Flop;
                        goto case BettingRound.Flop;

                    case BettingRound.Flop:
                        GamePlayHelpers.DealTurn(CardSet, CommunityCards);
                        CurrentGame.CurrentRound = BettingRound.Turn;
                        goto case BettingRound.Turn;

                    case BettingRound.Turn:
                        GamePlayHelpers.DealRiver(CardSet, CommunityCards);
                        CurrentGame.CurrentRound = BettingRound.River;
                        goto case BettingRound.River;

                    case BettingRound.River:
                        CurrentGame.CurrentRound = BettingRound.Showdown;
                        await CalculateWinner();
                        return;
                }
            }

            // --- Normal turn rotation ---
            do
            {
                CurrentGame.CurrentPlayer = (CurrentGame.CurrentPlayer + 1) % CurrentGame.Players.Count;
            } while (CurrentGame.Players[CurrentGame.CurrentPlayer].HasFolded || CurrentGame.Players[CurrentGame.CurrentPlayer].IsSittingOut);

            var nextPlayer = CurrentGame.Players[CurrentGame.CurrentPlayer];
            nextPlayer.IsNext = true;

            // If the next player is sitting out or left, auto-fold
            if (nextPlayer.IsSittingOut)
            {
                await OnPlayerActionComitted(nextPlayer, new PlayerAction { ActionType = EnumPlayerAction.Fold });
                return;
            }

            UpdateAvailableActions(nextPlayer);

            // Check if betting round is complete
            if (IsBettingRoundComplete())
                await AdvanceRound();

            OnGameChanged?.Invoke(this, EventArgs.Empty);
        }


        public void UpdateAvailableActions(PlayerModel player)
        {
            if (_dbTableService == null || PlayersPending == null || GameTable == null || CurrentGame == null || AvailableActions == null)
                throw new ArgumentNullException("null objects found");

            AvailableActions.Clear();

            // Only active player's turn
            if (!player.IsNext)
                return;

            if (CurrentGame.CurrentRound == BettingRound.Showdown)
                return;

            var actions = new List<ActionOption>();
            int playerIndex = CurrentGame.Players.IndexOf(player);

            // Fold is always available
            actions.Add(new ActionOption(EnumPlayerAction.Fold, 0));

            // --- PRE-FLOP BLINDS ---
            if (CurrentGame.CurrentRound == BettingRound.PreFlop)
            {
                if (playerIndex == CurrentGame.SmallBlindIndex && !player.HasPostedSmallBlind)
                {
                    actions.Add(new ActionOption(EnumPlayerAction.SmallBlind, CurrentGame.SmallBlind));
                    AvailableActions = actions;
                    return;
                }

                if (playerIndex == CurrentGame.BigBlindIndex && !player.HasPostedBigBlind)
                {
                    actions.Add(new ActionOption(EnumPlayerAction.BigBlind, CurrentGame.BigBlind));
                    AvailableActions = actions;
                    return;
                }
            }

            int callAmount = CurrentGame.CurrentBet - player.CurrentBet;

            // --- Call / Check ---
            if (callAmount > 0)
            {
                if (player.RemainingStack >= callAmount)
                {
                    actions.Add(new ActionOption(EnumPlayerAction.Call, callAmount));
                }
                else
                {
                    // Player can't fully call, only all-in
                    actions.Add(new ActionOption(EnumPlayerAction.AllIn, player.RemainingStack));
                    AvailableActions = actions;
                    return;
                }
            }
            else
            {
                actions.Add(new ActionOption(EnumPlayerAction.Check, 0));
            }

            // --- Bet / Raise ---
            int minBetOrRaise = CurrentGame.CurrentBet == 0 ? CurrentGame.MinBet : Math.Max(CurrentGame.MinBet, callAmount);

            if (player.RemainingStack >= minBetOrRaise)
            {
                if (CurrentGame.CurrentBet == 0)
                    actions.Add(new ActionOption(EnumPlayerAction.Bet, minBetOrRaise));
                else
                    actions.Add(new ActionOption(EnumPlayerAction.Raise, minBetOrRaise));
            }
            else if (player.RemainingStack > 0)
            {
                // Player can't meet minimum bet/raise → All-in is the only option
                actions.Add(new ActionOption(EnumPlayerAction.AllIn, player.RemainingStack));
                AvailableActions = actions;
                return;
            }

            // --- All-in ---
            if (player.RemainingStack > 0 && !actions.Any(a => a.ActionType == EnumPlayerAction.AllIn))
                actions.Add(new ActionOption(EnumPlayerAction.AllIn, player.RemainingStack));

            AvailableActions = actions;
        }



        private async Task AdvanceRound()
        {
            if (_dbTableService == null || PlayersPending == null || GameTable == null || CurrentGame == null || AvailableActions == null)
                throw new ArgumentNullException("null objects found");

            // Clear per-round flags
            foreach (var p in CurrentGame.Players)
            {
                p.HasActedThisRound = false;
                p.CurrentBet = 0;
                p.IsNext = false;
            }

            CurrentGame.CurrentBet = 0;
            AvailableActions.Clear();

            switch (CurrentGame.CurrentRound)
            {
                case BettingRound.PreFlop:
                    GamePlayHelpers.DealFlop(CardSet, CommunityCards);
                    CurrentGame.CurrentRound = BettingRound.Flop;
                    break;

                case BettingRound.Flop:
                    GamePlayHelpers.DealTurn(CardSet, CommunityCards);
                    CurrentGame.CurrentRound = BettingRound.Turn;
                    break;

                case BettingRound.Turn:
                    GamePlayHelpers.DealRiver(CardSet, CommunityCards);
                    CurrentGame.CurrentRound = BettingRound.River;
                    break;

                case BettingRound.River:
                    CurrentGame.CurrentRound = BettingRound.Showdown;
                    await CalculateWinner();
                    return;
            }

            // Assign NEXT SINGLE player
            CurrentGame.CurrentPlayer = GetFirstActivePlayerAfterDealer();
            CurrentGame.Players[CurrentGame.CurrentPlayer].IsNext = true;
            UpdateAvailableActions(CurrentGame.Players[CurrentGame.CurrentPlayer]);
        }



        private int GetFirstActivePlayerAfterDealer()
        {
            if (_dbTableService == null || PlayersPending == null || GameTable == null || CurrentGame == null || AvailableActions == null)
                throw new ArgumentNullException("null objects found");

            if (CurrentGame.Players.Count < 2)
                return 0;

            int index = (CurrentGame.DealerIndex + 1) % CurrentGame.Players.Count;

            // Find first active player (not folded, not sitting out)
            for (int i = 0; i < CurrentGame.Players.Count; i++)
            {
                var player = CurrentGame.Players[index];
                if (!player.HasFolded && !player.IsSittingOut)
                    return index;

                index = (index + 1) % CurrentGame.Players.Count;
            }

            // fallback: return dealer if everyone else folded/sitting out
            return CurrentGame.DealerIndex;
        }


        private bool IsBettingRoundComplete()
        {
            if (_dbTableService == null || PlayersPending == null || GameTable == null || CurrentGame == null || AvailableActions == null)
                throw new ArgumentNullException("null objects found");

            var activePlayers = CurrentGame.Players
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


        private async Task CalculateWinner()
        {
            if (_dbTableService == null || PlayersPending == null || GameTable == null || CurrentGame == null || AvailableActions == null)
                throw new ArgumentNullException("null objects found");

            // Get all active players (not folded, not sitting out)
            var activePlayers = CurrentGame.Players.Where(p => !p.HasFolded && !p.IsSittingOut).ToList();

            // If no active players, everyone folded
            if (!activePlayers.Any())
            {
                foreach (var p in CurrentGame.Players)
                    p.Result = "Alle haben gefoldet.\nUnentschieden.";

                CurrentGame.RoundLocked = false;
                CurrentGame.RoundFinished = true;
                AvailableActions.Clear();
                OnRoundFinished?.Invoke(this, EventArgs.Empty);
                return;
            }

            // If only one active player remains, they automatically win the pot
            if (activePlayers.Count == 1)
            {
                var winner = activePlayers[0];
                winner.RemainingStack += CurrentGame.Pot;
                winner.Result = $"Alle anderen haben gefoldet.\nDu gewinnst den Pot: {CurrentGame.Pot}";

                foreach (var p in CurrentGame.Players)
                {
                    if (p != winner)
                        p.Result = "Du hast gefoldet.";

                    p.IsNext = false;
                }

                CurrentGame.TheWinners = new() { new PlayerModel(winner, "Gewinner durch Fold") };
                CurrentGame.RoundLocked = false;
                CurrentGame.RoundFinished = true;
                AvailableActions.Clear();
                OnRoundFinished?.Invoke(this, EventArgs.Empty);
                return;
            }

            // Wait until showdown
            if (CommunityCards == null || CommunityCards.Count < 5)
                return;

            // Evaluate best hand for each player
            var bestHands = new Dictionary<PlayerModel, (int Rank, List<int> Tie, string HandName)>();
            foreach (var p in activePlayers)
            {
                var fullHand = new List<Card> { p.Card1!, p.Card2! };
                fullHand.AddRange(CommunityCards);
                bestHands[p] = GamePlayHelpers.EvaluateBestHand(fullHand);
            }

            // Compare hands with tie-breakers
            int CompareHands((int Rank, List<int> Tie, string HandName) hand1,
                             (int Rank, List<int> Tie, string HandName) hand2)
            {
                if (hand1.Rank != hand2.Rank)
                    return hand1.Rank.CompareTo(hand2.Rank);

                // Compare tie lists element by element
                for (int i = 0; i < Math.Min(hand1.Tie.Count, hand2.Tie.Count); i++)
                {
                    if (hand1.Tie[i] != hand2.Tie[i])
                        return hand1.Tie[i].CompareTo(hand2.Tie[i]);
                }

                return 0; // completely equal hands
            }

            // Find top hand(s)
            var topPlayers = new List<PlayerModel>();
            (int Rank, List<int> Tie, string HandName) bestHand = (0, new List<int>(), "");

            foreach (var kv in bestHands)
            {
                if (bestHand.Rank == 0 || CompareHands(kv.Value, bestHand) > 0)
                {
                    topPlayers.Clear();
                    topPlayers.Add(kv.Key);
                    bestHand = kv.Value;
                }
                else if (CompareHands(kv.Value, bestHand) == 0)
                {
                    topPlayers.Add(kv.Key); // tie
                }
            }

            int potShare = CurrentGame.Pot / topPlayers.Count;
            CurrentGame.TheWinners = new();

            // Assign results and distribute pot
            foreach (var p in CurrentGame.Players)
            {
                var handName = bestHands.ContainsKey(p) ? bestHands[p].HandName : "Keine Hand";

                if (topPlayers.Contains(p))
                {
                    p.RemainingStack += potShare;
                    p.Result = topPlayers.Count > 1
                        ? $"Unentschieden!\nDeine beste Hand: {handName}\nGewonnener Pot: {potShare}"
                        : $"Du hast diese Runde gewonnen!\nDeine beste Hand: {handName}\nGewinnener Pot: {potShare}";

                    CurrentGame.TheWinners.Add(new PlayerModel(p, handName));
                }
                else
                {
                    p.Result = $"Du hast diese Runde verloren.\nDeine beste Hand: {handName}\nGewonnener Pot: 0";
                }

                p.IsNext = false;
            }

            CurrentGame.RoundLocked = false;
            CurrentGame.RoundFinished = true;
            AvailableActions.Clear();
            OnRoundFinished?.Invoke(this, EventArgs.Empty);

            // Ensure players can cover blinds for next game
            foreach (var player in CurrentGame.Players)
            {
                if (player.RemainingStack < CurrentGame.SmallBlind)
                {
                    //Kick out the player, but inform him                   
                    if (OnPlayerLost != null)
                    {
                        foreach (var handler in OnPlayerLost.GetInvocationList().Cast<Func<PlayerModel, Task>>())
                        {
                            //_ = Task.Run(async () => await handler(player));
                            await handler(player);
                        }
                    }
                    await RemovePlayer(player);
                }
            }
        }


        public async ValueTask DisposeAsync()
        {
            if (_dbTableService == null || PlayersPending == null || GameTable == null || CurrentGame == null || AvailableActions == null)
                throw new ArgumentNullException("null objects found");

            if (_dbTableService != null)
                await _dbTableService.SaveItemAsync(GameTable);

            GameTable = null;
            PlayersPending = null;
            CardSet = null;
            CommunityCards = null;
            CurrentGame = null;
            AvailableActions = null;

            return;
        }
    }
}

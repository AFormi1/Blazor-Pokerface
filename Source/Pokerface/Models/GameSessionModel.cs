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


        public delegate void PlayerLostEventHandler(PlayerModel player);
        public event PlayerLostEventHandler? OnPlayerLost;

        public event Action? OnSessionChanged;

        public int Id { get; set; }

        public TableModel? GameTable { get; set; } = new TableModel();
        public PlayerModel?[] PlayersPending { get; set; } = new PlayerModel?[TableModel.MaxPlayers];
        public PlayerModel[] RealPlayersPending => [.. PlayersPending.Where(p => p != null).Select(p => p!)];
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
            return RealPlayersPending.Where(p => p.Id == player).FirstOrDefault();
        }

        public async Task AddPlayer(PlayerModel player)
        {
            if (_dbTableService == null || PlayersPending == null || GameTable == null)
                throw new ArgumentNullException("null objects found in AddPlayer");

            for (int i = 0; i < PlayersPending.Length; i++)
            {
                //check for a three seat and add the player on this index
                if (PlayersPending[i] == null)
                {
                    PlayersPending[i] = player;
                    player.Chair = i;
                    GameTable.CurrentPlayers = RealPlayersPending.Length;

                    //Update the TableModel in DB
                    await _dbTableService.SaveItemAsync(GameTable);
                    break;
                }
            }  
            
            OnSessionChanged?.Invoke();
        }


        public async Task RemovePlayer(PlayerModel player)
        {
            if (_dbTableService == null || PlayersPending == null || GameTable == null || CurrentGame == null)
                throw new ArgumentNullException("null objects found in RemovePlayer");

            if (player.Chair < 0 || player.Chair >= PlayersPending.Length)
                throw new InvalidDataException("Chair must be between 0 and 7");

            // Mark as folded / sitting out
            player.HasFolded = true;
            player.IsSittingOut = true;

            // If the next player is sitting out or left, auto-fold
            if (player.IsNext)
                await OnPlayerActionComitted(player, new PlayerAction { ActionType = EnumPlayerAction.Fold });

            PlayersPending[player.Chair] = null;
            GameTable.CurrentPlayers = RealPlayersPending.Length;

            // Update DB
            await _dbTableService.SaveItemAsync(GameTable);

            // If everyone left, close the session
            if (RealPlayersPending.All(p => p.IsSittingOut))
            {
                CurrentGame.RoundLocked = false;
                CurrentGame.RoundFinished = false;
            }

            OnSessionChanged?.Invoke();
        }

        public async Task StartGame()
        {
            if (_dbTableService == null || PlayersPending == null || GameTable == null || CurrentGame == null)
                throw new ArgumentNullException("null objects found");

            // Reset the deck and community cards
            CardSet = CardDeck.GenerateShuffledDeck();
            CommunityCards = new List<Card>();

            // Initialize the game context
            int lastDealerIndex = CurrentGame != null ? CurrentGame.DealerIndex : 0;
            CurrentGame = new GameContext(PlayersPending, lastDealerIndex);
            AvailableActions = new();

            if (CurrentGame.RealPlayers.Length < 2)
                return;

            // Reset player round flags
            foreach (var player in CurrentGame.RealPlayers)
            {
                player.ResetRoundSettings();
                player.PlayerInput -= OnPlayerActionComitted;
                player.PlayerInput += OnPlayerActionComitted;
            }

            // Start with the Ante round
            CurrentGame.CurrentRound = BettingRound.Ante;

            // First active player posts ante
            CurrentGame.CurrentPlayer = GetFirstActivePlayerAfterDealer();
            CurrentGame.RealPlayers[CurrentGame.CurrentPlayer].IsNext = true;

            UpdateAvailableActions(CurrentGame.RealPlayers[CurrentGame.CurrentPlayer]);

            OnSessionChanged?.Invoke(); // Wait for player input for ante
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

                case EnumPlayerAction.PostAnte:
                    player.CurrentBet += CurrentGame.SmallBlind; // Ante equals SmallBlind
                    player.RemainingStack -= CurrentGame.SmallBlind;
                    CurrentGame.Pot += CurrentGame.SmallBlind;
                    player.HasPostedAnte = true;
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
            var activePlayers = CurrentGame.RealPlayers.Where(p => !p.HasFolded && !p.IsSittingOut).ToList();
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
                CurrentGame.CurrentPlayer = (CurrentGame.CurrentPlayer + 1) % CurrentGame.RealPlayers.Length;
            } while (CurrentGame.RealPlayers[CurrentGame.CurrentPlayer].HasFolded || CurrentGame.RealPlayers[CurrentGame.CurrentPlayer].IsSittingOut);

            var nextPlayer = CurrentGame.RealPlayers[CurrentGame.CurrentPlayer];
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

            OnSessionChanged?.Invoke();
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
            int playerIndex = CurrentGame.RealPlayers.IndexOf(player);

            // Fold is always available
            actions.Add(new ActionOption(EnumPlayerAction.Fold, 0));

            if (CurrentGame.CurrentRound == BettingRound.Ante)
            {
                actions.Add(new ActionOption(EnumPlayerAction.PostAnte, CurrentGame.SmallBlind));
                AvailableActions = actions;
                return;
            }

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
            foreach (var p in CurrentGame.RealPlayers)
            {
                p.HasActedThisRound = false;
                p.CurrentBet = 0;
                p.IsNext = false;
            }

            CurrentGame.CurrentBet = 0;
            AvailableActions.Clear();

            switch (CurrentGame.CurrentRound)
            {
                case BettingRound.Ante:
                    GamePlayHelpers.DealPlayerCards(CardSet, CurrentGame.RealPlayers);
                    CurrentGame.CurrentRound = BettingRound.PreFlop;
                    break;

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
            CurrentGame.RealPlayers[CurrentGame.CurrentPlayer].IsNext = true;
            UpdateAvailableActions(CurrentGame.RealPlayers[CurrentGame.CurrentPlayer]);

            OnSessionChanged?.Invoke();
        }


        private async Task UpdateLooserAndSession()
        {
            if (CurrentGame == null || CurrentGame.Players == null)
                return;

            // Notify UI immediately
            OnSessionChanged?.Invoke();

            // Ensure players can cover blinds for next game
            var bustedPlayers = CurrentGame.RealPlayers.Where(p => p.RemainingStack < CurrentGame.SmallBlind).ToList();

            // Prepare all busted player tasks but do not start yet
            List<Func<Task>> lostTasks = new();

            foreach (var busted in bustedPlayers)
            {
                lostTasks.Add(async () =>
                {
                    await Task.Delay(2000);
                    OnPlayerLost?.Invoke(busted);
                    await Task.Delay(2000);
                    await RemovePlayer(busted);
                });
            }

            // Now execute all tasks in parallel, but do not await them
            _ = Task.WhenAll(lostTasks.Select(f => f()));
        }

        private int GetFirstActivePlayerAfterDealer()
        {
            if (_dbTableService == null || PlayersPending == null || GameTable == null || CurrentGame == null || AvailableActions == null)
                throw new ArgumentNullException("null objects found");

            if (CurrentGame.RealPlayers.Length < 2)
                return 0;

            int index = (CurrentGame.DealerIndex + 1) % CurrentGame.RealPlayers.Length;

            // Find first active player (not folded, not sitting out)
            for (int i = 0; i < CurrentGame.RealPlayers.Length; i++)
            {
                var player = CurrentGame.RealPlayers[index];
                if (!player.HasFolded && !player.IsSittingOut)
                    return index;

                index = (index + 1) % CurrentGame.RealPlayers.Length;
            }

            // fallback: return dealer if everyone else folded/sitting out
            return CurrentGame.DealerIndex;
        }


        private bool IsBettingRoundComplete()
        {
            if (CurrentGame == null || CurrentGame.Players == null)
                throw new ArgumentNullException("objects are null");

            var activePlayers = CurrentGame.RealPlayers
                .Where(p => !p.HasFolded && !p.IsSittingOut)
                .ToList();

            if (!activePlayers.All(p => p.HasActedThisRound || p.AllIn))
                return false;

            // Special check for Ante round
            if (CurrentGame.CurrentRound == BettingRound.Ante)
                return activePlayers.All(p => p.HasPostedAnte);

            // Normal check for other rounds
            return activePlayers.All(p => p.CurrentBet == CurrentGame.CurrentBet || p.AllIn);
        }



        private async Task CalculateWinner()
        {
            if (_dbTableService == null || PlayersPending == null || GameTable == null || CurrentGame == null || AvailableActions == null)
                throw new ArgumentNullException("null objects found");

            if (CurrentGame.RoundFinished)
                return;

            // Get all active players (not folded, not sitting out)
            var activePlayers = CurrentGame.RealPlayers.Where(p => !p.HasFolded && !p.IsSittingOut).ToList();

            // If no active players, everyone folded
            if (!activePlayers.Any())
            {
                foreach (var p in CurrentGame.RealPlayers)
                    p.Result = "Alle haben gefoldet.\nUnentschieden.";

                CurrentGame.RoundLocked = false;
                CurrentGame.RoundFinished = true;
                AvailableActions.Clear();

                await UpdateLooserAndSession();
                return;
            }

            // If only one active player remains, they automatically win the pot
            if (activePlayers.Count == 1)
            {
                var winner = activePlayers[0];
                winner.RemainingStack += CurrentGame.Pot;
                winner.Result = $"Alle anderen haben gefoldet.\nDu gewinnst den Pot {CurrentGame.Pot}";

                foreach (var p in CurrentGame.RealPlayers)
                {
                    if (p != winner)
                        p.Result = "Du hast gefoldet.";

                    p.IsNext = false;
                }

                CurrentGame.TheWinners = new() { new PlayerModel(winner, "durch Fold") };
                CurrentGame.RoundLocked = false;
                CurrentGame.RoundFinished = true;
                AvailableActions.Clear();

                await UpdateLooserAndSession();
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
            foreach (var p in CurrentGame.RealPlayers)
            {
                var handName = bestHands.ContainsKey(p) ? bestHands[p].HandName : "Keine Hand";

                if (topPlayers.Contains(p))
                {
                    string kickerText = "";

                    // If tie, show kicker cards
                    if (topPlayers.Count > 1 && bestHands[p].Tie.Any())
                    {
                        kickerText = "\nKicker: " + string.Join(", ", bestHands[p].Tie.Select(t => GamePlayHelpers.CardValueToString(t)));
                    }

                    p.RemainingStack += potShare;
                    p.Result = topPlayers.Count > 1
                        ? $"Unentschieden!\nDeine beste Hand {handName}{kickerText}\nGewonnener Pot: {potShare}"
                        : $"Du hast diese Runde gewonnen!\nDeine beste Hand {handName}\nGewinnener Pot: {potShare}";

                    CurrentGame.TheWinners.Add(new PlayerModel(p, handName));
                }
                else
                {
                    p.Result = $"Du hast diese Runde verloren.\nDeine beste Hand {handName}\nGewonnener Pot: 0";
                }

                p.IsNext = false;
            }


            CurrentGame.RoundLocked = false;
            CurrentGame.RoundFinished = true;
            AvailableActions.Clear();

            await UpdateLooserAndSession();
        }


        private int CompareHands(
            (int Rank, List<int> Tie, string HandName) hand1,
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

            return 0; // hands are completely equal
        }


        public async ValueTask DisposeAsync()
        {
            if (_dbTableService == null || PlayersPending == null || GameTable == null || CurrentGame == null || AvailableActions == null)
                throw new ArgumentNullException("null objects found");

            if (_dbTableService != null)
                await _dbTableService.SaveItemAsync(GameTable);

            GameTable = null;
            CardSet = null;
            CommunityCards = null;
            CurrentGame = null;
            AvailableActions = null;

            return;
        }
    }
}

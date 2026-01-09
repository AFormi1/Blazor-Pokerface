using Pokerface.Enums;
using Pokerface.Services;
using Pokerface.Services.DB;

namespace Pokerface.Models
{
    public class GameSessionModel : IAsyncDisposable
    {
        private readonly DbTableService? _dbTableService;

        public delegate void PlayerLostEventHandler(PlayerModel player);
        public event PlayerLostEventHandler? OnPlayerLost;

        public event Action? OnSessionChanged;

        public TableModel CurrentGame { get; set; }
        public List<PlayerModel>? PlayersPending { get; set; } = new List<PlayerModel>();
        public List<Card>? CardSet { get; set; }
        public List<Card>? CommunityCards { get; set; }
        public List<ActionOption>? AvailableActions { get; private set; } = new();

        public GameSessionModel(DbTableService dbTableService, TableModel selectedTable)
        {
            _dbTableService = dbTableService;
            CurrentGame = selectedTable;
        }

        public PlayerModel? GetPlayerById(int player)
        {
            return PlayersPending?.Where(p => p.Id == player).FirstOrDefault();
        }

        public async Task AddPlayer(PlayerModel player)
        {
            if (_dbTableService == null || PlayersPending == null)
                throw new ArgumentNullException("null objects found in AddPlayer");

            PlayersPending.Add(player);

            CurrentGame.CurrentPlayers = PlayersPending.Count;

            //Update the TableModel in DB
            await _dbTableService.SaveItemAsync(CurrentGame);

            OnSessionChanged?.Invoke();

        }

        public async Task RemovePlayer(PlayerModel player)
        {
            if (_dbTableService == null || PlayersPending == null)
                throw new ArgumentNullException("null objects found in RemovePlayer");

            // Mark as folded / sitting out
            player.HasFolded = true;
            player.IsSittingOut = true;

            // If the next player is sitting out or left, auto-fold
            if (player.IsNext)
                await OnPlayerActionComitted(player, new PlayerAction { ActionType = EnumPlayerAction.Fold });

            PlayersPending.Remove(player);
            CurrentGame.CurrentPlayers = PlayersPending.Count();

            // Update DB
            await _dbTableService.SaveItemAsync(CurrentGame);

            // If everyone left, close the session
            if (PlayersPending.All(p => p.IsSittingOut))
            {
                CurrentGame.RoundLocked = false;
                CurrentGame.RoundFinished = false;
            }

            OnSessionChanged?.Invoke();
        }

        public async Task StartGame()
        {
            if (_dbTableService == null || PlayersPending == null)
                throw new ArgumentNullException("null objects found");

            // Reset the deck and community cards
            CardSet = CardDeck.GenerateShuffledDeck();
            CommunityCards = new List<Card>();

            // Initialize the game context
            int lastDealerIndex = CurrentGame != null ? CurrentGame.DealerIndex : 0;

            if (CurrentGame == null)
                throw new ArgumentNullException("null objects found");

            Console.WriteLine("Starting New Round ...");

            CurrentGame.RestartRound(PlayersPending, lastDealerIndex);

            AvailableActions = new();

            if (CurrentGame.Players.Count < 2)
                return;

            // Reset player round flags
            foreach (var player in CurrentGame.Players)
            {
                player.ResetRoundSettings();
                player.PlayerInput -= OnPlayerActionComitted;
                player.PlayerInput += OnPlayerActionComitted;
            }

            // Start with the Ante round
            CurrentGame.CurrentRound = BettingRound.Ante;

            // First active player posts ante
            CurrentGame.CurrentPlayer = GetFirstActivePlayerAfterDealer();
            CurrentGame.Players[CurrentGame.CurrentPlayer].IsNext = true;

            UpdateAvailableActions(CurrentGame.Players[CurrentGame.CurrentPlayer]);

            OnSessionChanged?.Invoke(); // Wait for player input for ante
        }



        private async Task OnPlayerActionComitted(PlayerModel player, PlayerAction action)
        {
            if (_dbTableService == null || PlayersPending == null)
                throw new ArgumentNullException("null objects found");

            if (!player.IsNext)
                return;

            GamePlayHelpers.LogPlayerComitted(player, action);

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
                    {
                        int callAmount = CurrentGame.CurrentBet - player.CurrentBet;
                        int actualCall = Math.Min(callAmount, player.RemainingStack);
                        player.CurrentBet += actualCall;
                        player.RemainingStack -= actualCall;
                        CurrentGame.Pot += actualCall;
                        if (player.RemainingStack == 0) player.AllIn = true;
                        player.HasActedThisRound = true;
                    }
                    break;

                case EnumPlayerAction.Bet:
                case EnumPlayerAction.Raise:
                case EnumPlayerAction.ReRaise:
                    {
                        int totalBet = Math.Min(action.CurrentBet, player.RemainingStack);
                        player.CurrentBet += totalBet;
                        player.RemainingStack -= totalBet;
                        CurrentGame.CurrentBet = Math.Max(CurrentGame.CurrentBet, player.CurrentBet);
                        CurrentGame.Pot += totalBet;
                        if (player.RemainingStack == 0) player.AllIn = true;
                        player.HasActedThisRound = true;
                    }
                    break;

                case EnumPlayerAction.AllIn:
                    {
                        int allInAmount = player.RemainingStack;
                        player.CurrentBet += allInAmount;
                        CurrentGame.CurrentBet = Math.Max(CurrentGame.CurrentBet, player.CurrentBet);
                        CurrentGame.Pot += allInAmount;
                        player.RemainingStack = 0;
                        player.AllIn = true;
                        player.HasActedThisRound = true;
                    }
                    break;

                case EnumPlayerAction.PostAnte:
                    {
                        int anteAmount = CurrentGame.Ante;
                        int actualAnte = Math.Min(anteAmount, player.RemainingStack);
                        player.CurrentBet += actualAnte;
                        player.RemainingStack -= actualAnte;
                        CurrentGame.Pot += actualAnte;
                        player.HasPostedAnte = true;
                        player.HasActedThisRound = true;
                        if (player.RemainingStack == 0) player.AllIn = true;
                    }
                    break;

                case EnumPlayerAction.SmallBlind:
                    {
                        int sb = Math.Min(CurrentGame.SmallBlind, player.RemainingStack);
                        player.CurrentBet += sb;
                        player.RemainingStack -= sb;
                        CurrentGame.Pot += sb;
                        player.HasPostedSmallBlind = true;
                        CurrentGame.CurrentBet = Math.Max(CurrentGame.CurrentBet, sb);
                        if (player.RemainingStack == 0) player.AllIn = true;
                    }
                    break;

                case EnumPlayerAction.BigBlind:
                    {
                        int bb = Math.Min(CurrentGame.BigBlind, player.RemainingStack);
                        player.CurrentBet += bb;
                        player.RemainingStack -= bb;
                        CurrentGame.Pot += bb;
                        player.HasPostedBigBlind = true;
                        CurrentGame.CurrentBet = Math.Max(CurrentGame.CurrentBet, bb);
                        if (player.RemainingStack == 0) player.AllIn = true;
                    }
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

                default:
                    throw new ArgumentOutOfRangeException(nameof(action.ActionType), "Unknown player action");
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

            OnSessionChanged?.Invoke();
        }


        public void UpdateAvailableActions(PlayerModel player)
        {
            if (_dbTableService == null || PlayersPending == null || AvailableActions == null)
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

            if (CurrentGame.CurrentRound == BettingRound.Ante)
            {
                //skip this section, if Ante is not apllied
                if (CurrentGame.Ante == 0)
                {
                    CurrentGame.CurrentRound = BettingRound.PreFlop;
                }
                else
                {
                    actions.Add(new ActionOption(EnumPlayerAction.PostAnte, CurrentGame.SmallBlind));
                    AvailableActions = actions;
                    GamePlayHelpers.LogGameOptions(player, AvailableActions);
                    return;
                }
            }

            // --- PRE-FLOP BLINDS ---
            if (CurrentGame.CurrentRound == BettingRound.PreFlop)
            {
                bool noBlinds = CurrentGame.SmallBlind == 0 || CurrentGame.BigBlind == 0;

                if (noBlinds)
                {
                    // No forced bets at all → just ensure cards are dealt
                    HandOutPlayerCardsIfNeeded();
                    // IMPORTANT: do NOT mark blinds as posted
                }
                else
                {
                    if (playerIndex == CurrentGame.SmallBlindIndex && !player.HasPostedSmallBlind)
                    {
                        actions.Add(new ActionOption(EnumPlayerAction.SmallBlind, CurrentGame.SmallBlind));
                        AvailableActions = actions;
                        GamePlayHelpers.LogGameOptions(player, AvailableActions);
                        return;
                    }

                    if (playerIndex == CurrentGame.BigBlindIndex && !player.HasPostedBigBlind)
                    {
                        actions.Add(new ActionOption(EnumPlayerAction.BigBlind, CurrentGame.BigBlind));
                        AvailableActions = actions;
                        GamePlayHelpers.LogGameOptions(player, AvailableActions);
                        return;
                    }
                }
            }
            // --- Call / Check ---
            int callAmount = CurrentGame.CurrentBet - player.CurrentBet;
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
                    GamePlayHelpers.LogGameOptions(player, AvailableActions);
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
                GamePlayHelpers.LogGameOptions(player, AvailableActions);
                return;
            }

            // --- All-in ---
            if (player.RemainingStack > 0 && !actions.Any(a => a.ActionType == EnumPlayerAction.AllIn))
                actions.Add(new ActionOption(EnumPlayerAction.AllIn, player.RemainingStack));

            AvailableActions = actions;
            GamePlayHelpers.LogGameOptions(player, AvailableActions);
        }



        private async Task AdvanceRound()
        {
            if (_dbTableService == null || PlayersPending == null || AvailableActions == null)
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
                case BettingRound.Ante:
                    HandOutPlayerCardsIfNeeded();
                    CurrentGame.CurrentRound = BettingRound.PreFlop;
                    break;

                case BettingRound.PreFlop:
                    //give every Player two Cards, if they dont have it (in Case, if we skipped Ante/SmallBlind/BigBild
                    HandOutPlayerCardsIfNeeded();
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

            Console.WriteLine("Advance Round finished ...");
            OnSessionChanged?.Invoke();
        }


        private async Task UpdateLooserAndSession()
        {
            if (CurrentGame == null || CurrentGame.Players == null)
                return;

            // Ensure players can cover blinds for next game
            var bustedPlayers = CurrentGame.Players.Where(p => p.RemainingStack < CurrentGame.SmallBlind).ToList();

            bool gameOver = bustedPlayers.Any();

            // Prepare all busted player tasks but do not start yet
            List<Func<Task>> lostTasks = new();

            foreach (var busted in bustedPlayers)
            {
                lostTasks.Add(async () =>
                {
                    await Task.Delay(2000);
                    busted.Card1 = null;
                    busted.Card2 = null;
                    OnPlayerLost?.Invoke(busted);

                    await Task.Delay(2000);
                    await RemovePlayer(busted);
                });
            }

            Console.WriteLine("Round finished!");

            // Now execute all tasks in parallel, but do not await them
            _ = Task.WhenAll(lostTasks.Select(f => f()));

            if (!gameOver)
                OnSessionChanged?.Invoke();

        }

        private int GetFirstActivePlayerAfterDealer()
        {
            if (_dbTableService == null || PlayersPending == null || AvailableActions == null)
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
            if (CurrentGame == null || CurrentGame.Players == null)
                throw new ArgumentNullException("objects are null");

            var activePlayers = CurrentGame.Players
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
            if (_dbTableService == null || PlayersPending == null || AvailableActions == null)
                throw new ArgumentNullException("null objects found");

            if (CurrentGame.RoundFinished)
                return;

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

                await UpdateLooserAndSession();
                return;
            }

            // If only one active player remains, they automatically win the pot
            if (activePlayers.Count == 1)
            {
                var winner = activePlayers[0];
                winner.RemainingStack += CurrentGame.Pot;
                winner.Result = $"Alle anderen haben gefoldet.\nDu gewinnst den Pot {CurrentGame.Pot}";

                foreach (var p in CurrentGame.Players)
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
            var bestHands = new Dictionary<PlayerModel, (int Rank, List<int> Tie, string HandName, string HandRank)>();
            foreach (var p in activePlayers)
            {
                List<Card> fullHand = [p.Card1!, p.Card2!, .. CommunityCards];
                bestHands[p] = GamePlayHelpers.EvaluateBestHand(fullHand);
            }


            // Find top hand(s)
            var topPlayers = new List<PlayerModel>();
            (int Rank, List<int> Tie, string HandName, string HandRank) bestHand = (0, new List<int>(), "", "");

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

            bool isDraw = topPlayers.Count > 1;
            int potShare = CurrentGame.Pot / topPlayers.Count;
            CurrentGame.TheWinners = new();

            // Assign results and distribute pot
            foreach (var p in CurrentGame.Players)
            {
                var handName = bestHands.ContainsKey(p) ? bestHands[p].HandName : "Keine Hand";

                // Determine kicker text for this player, if we got a draw
                string kickerText = "";
                if (isDraw && bestHands.ContainsKey(p) && bestHands[p].Tie.Any())
                {
                    kickerText = "\nKicker: " + string.Join(", ", bestHands[p].Tie.OrderDescending().Select(t => GamePlayHelpers.CardValueToString(t)));
                }

                if (topPlayers.Contains(p))
                {
                    // Winner messages
                    p.RemainingStack += potShare;
                    p.Result = topPlayers.Count > 1
                        ? $"Unentschieden!\nDeine beste Hand {handName}{kickerText}\nGewonnener Pot: {potShare}"
                        : $"Du hast diese Runde gewonnen!\nDeine beste Hand {handName}{kickerText}\nGewonnener Pot: {potShare}";

                    CurrentGame.TheWinners.Add(new PlayerModel(p, handName));
                }
                else
                {
                    // Lost player messages
                    // Show kicker if they had the same hand type as winners
                    bool sameHandAsWinner = topPlayers.Any(w => bestHands[w].HandRank == bestHands[p].HandRank);

                    string lostKickerText = sameHandAsWinner ? kickerText : "";

                    p.Result = $"Du hast diese Runde verloren.\nDeine beste Hand {handName}{lostKickerText}\nGewonnener Pot: 0";
                }

                p.IsNext = false;
            }



            CurrentGame.RoundLocked = false;
            CurrentGame.RoundFinished = true;
            AvailableActions.Clear();

            await UpdateLooserAndSession();
        }


        private int CompareHands(
            (int Rank, List<int> Tie, string HandName, string HandRank) hand1,
            (int Rank, List<int> Tie, string HandName, string HandRank) hand2)
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

        private void HandOutPlayerCardsIfNeeded()
        {
            if (!CurrentGame.PlayersGotCards)
            {
                CurrentGame.PlayersGotCards = true;
                GamePlayHelpers.DealPlayerCards(CardSet, CurrentGame.Players);
            }
        }



        public async ValueTask DisposeAsync()
        {
            if (_dbTableService == null || PlayersPending == null || AvailableActions == null)
                throw new ArgumentNullException("null objects found");

            if (_dbTableService != null)
                await _dbTableService.SaveItemAsync(CurrentGame);

            PlayersPending = null;
            CardSet = null;
            CommunityCards = null;
            AvailableActions = null;

            return;
        }
    }
}
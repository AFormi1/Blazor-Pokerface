using Pokerface.Enums;
using Pokerface.Models;
using System.Runtime.InteropServices;

namespace Pokerface.Services
{
    public static class GamePlayHelpers
    {
        public static void DealPlayerCards(List<Card>? cardset, PlayerModel[]? players)
        {
            if (cardset == null || players == null)
                return;

            BurnCard(cardset);

            // Deal player cards
            foreach (var player in players)
            {
                player.Card1 = cardset[0]; cardset.RemoveAt(0);
                player.Card2 = cardset[0]; cardset.RemoveAt(0);
            }
        }

        public static void DealFlop(List<Card>? cardset, List<Card>? communityset)
        {
            if (cardset == null)
                return;

            BurnCard(cardset);
            if (communityset == null)
                communityset = new List<Card>();

            communityset.Add(cardset[0]);
            communityset.Add(cardset[1]);
            communityset.Add(cardset[2]);

            cardset.RemoveRange(0, 3);
        }

        public static void DealTurn(List<Card>? cardset, List<Card>? communityset)
        {
            if (cardset == null || communityset == null)
                return;

            BurnCard(cardset);
            communityset.Add(cardset[0]);
            cardset.RemoveAt(0);
        }

        public static void DealRiver(List<Card>? cardset, List<Card>? communityset)
        {
            if (cardset == null || communityset == null)
                return;

            BurnCard(cardset);
            communityset.Add(cardset[0]);
            cardset.RemoveAt(0);
        }

        public static void BurnCard(List<Card>? cardset)
        {
            if (cardset == null)
                return;

            cardset.RemoveAt(0);
        }

        public static (int Rank, List<int> Tie, string HandName) EvaluateBestHand(List<Card> sevenCards)
        {
            var cardValues = sevenCards.Select(c => (int)c.Rank)
                                       .OrderByDescending(v => v)
                                       .ToList();
            var groups = cardValues.GroupBy(v => v)
                                   .OrderByDescending(g => g.Count())
                                   .ThenByDescending(g => g.Key)
                                   .ToList();

            var suitGroups = sevenCards.GroupBy(c => c.Suit)
                                       .Where(g => g.Count() >= 5)
                                       .ToList();
            bool isFlush = suitGroups.Any();

            var distinctValues = cardValues.Distinct().ToList();
            int straightHigh = -1;

            for (int i = 0; i <= distinctValues.Count - 5; i++)
            {
                if (distinctValues[i] - distinctValues[i + 4] == 4)
                {
                    straightHigh = distinctValues[i];
                    break;
                }
            }

            // Low-Ace straight (A-2-3-4-5)
            if (distinctValues.Contains((int)EnumCardRank.Ace) &&
                distinctValues.Contains((int)EnumCardRank.Five) &&
                distinctValues.Contains((int)EnumCardRank.Four) &&
                distinctValues.Contains((int)EnumCardRank.Three) &&
                distinctValues.Contains((int)EnumCardRank.Two))
            {
                straightHigh = 5;
            }

            bool isStraight = straightHigh != -1;

            string RankToName(int rank) => Enum.GetName(typeof(EnumCardRank), rank) ?? rank.ToString();

            // Straight flush / Royal flush
            if (isFlush && isStraight)
                return (900 + straightHigh, new List<int> { straightHigh },
                    straightHigh == (int)EnumCardRank.Ace ? "Royal Flush" : $"Straight Flush {RankToName(straightHigh)} high");

            if (groups[0].Count() == 4)
                return (800 + groups[0].Key, groups.Select(g => g.Key).ToList(), $"Four of a Kind {RankToName(groups[0].Key)}");

            if (groups[0].Count() == 3 && groups.Count > 1 && groups[1].Count() >= 2)
                return (700 + groups[0].Key, new List<int> { groups[1].Key, groups[0].Key },
                    $"Full House {RankToName(groups[0].Key)} over {RankToName(groups[1].Key)}");

            if (isFlush)
            {
                var flushCards = suitGroups.First().Select(c => (int)c.Rank)
                                                 .OrderByDescending(v => v)
                                                 .Take(5)
                                                 .ToList();
                return (600 + flushCards[0], flushCards, $"Flush {RankToName(flushCards[0])} high");
            }

            if (isStraight)
                return (500 + straightHigh, new List<int> { straightHigh }, $"Straight {RankToName(straightHigh)} high");

            if (groups[0].Count() == 3)
                return (400 + groups[0].Key, groups.Select(g => g.Key).ToList(), $"Three of a Kind {RankToName(groups[0].Key)}");

            if (groups[0].Count() == 2 && groups.Count > 1 && groups[1].Count() == 2)
                return (300 + groups[0].Key, groups.Select(g => g.Key).ToList(),
                    $"Two Pair {RankToName(groups[0].Key)} & {RankToName(groups[1].Key)}");

            if (groups[0].Count() == 2)
                return (200 + groups[0].Key, groups.Select(g => g.Key).ToList(), $"One Pair {RankToName(groups[0].Key)}");

            return (100 + groups[0].Key, groups.Select(g => g.Key).ToList(), $"High Card {RankToName(groups[0].Key)}");
        }


        public static string CardValueToString(int value)
        {
            return value switch
            {
                1 => "A",
                11 => "J",
                12 => "Q",
                13 => "K",
                _ => value.ToString()
            };
        }

    }
}

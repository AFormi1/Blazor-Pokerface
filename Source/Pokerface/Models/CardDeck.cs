using Pokerface.Enums;

namespace Pokerface.Models
{  
    public static class CardDeck
    {
        private static readonly Random Random = new();

        public static List<Card> GenerateShuffledDeck()
        {
            var deck = Enum.GetValues<EnumCardRank>()
                .SelectMany(rank => Enum.GetValues<EnumCardSuit>()
                    .Select(suit => new Card(rank, suit)))
                .ToList();

            // Fisher-Yates shuffle
            for (int i = deck.Count - 1; i > 0; i--)
            {
                int j = Random.Next(i + 1);
                (deck[i], deck[j]) = (deck[j], deck[i]);
            }

            return deck;
        }
    }
}

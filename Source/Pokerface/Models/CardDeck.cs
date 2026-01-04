namespace Pokerface.Models
{
    public record Card(EnumCardRank Rank, EnumCardSuit Suit);

    public static class CardDeck
    {
        private static readonly Random Random = new();

        public static List<Card> GenerateShuffledDeck()
        {
            // Create full deck
            var deck = Enum.GetValues<EnumCardRank>()
                .SelectMany(rank => Enum.GetValues<EnumCardSuit>()
                    .Select(suit => new Card(rank, suit)))
                .ToList();

            // Shuffle
            return deck.OrderBy(_ => Random.Next()).ToList();
        }

        public static List<Card> MixWholeRandomCards()
        {
            var deck = GenerateShuffledDeck();
            return deck.Take(52).ToList();
        }
    }
}

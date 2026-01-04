using Pokerface.Services;

namespace Pokerface.Models
{
    public class PokerCardModel
    {
        private readonly CardSvgProvider? _cardSvgProvider;             
        public string ImageUrl { get; private set; } = string.Empty;
        public bool IsVisible { get; private set; }

        public PokerCardModel()
        {
            
        }
        public PokerCardModel(CardSvgProvider provider)
        {
            _cardSvgProvider = provider;
            Init(EnumCardSuit.Spade, EnumCardRank.Ace, false);
        }

        public void Init(EnumCardSuit suit, EnumCardRank rank, bool showFront)
        {
            if (_cardSvgProvider == null)
                return;

            ImageUrl = showFront
                ? _cardSvgProvider.GetFrontsideSvg(rank, suit)
                : _cardSvgProvider.GetBacksideSvg();
        }
    }
}
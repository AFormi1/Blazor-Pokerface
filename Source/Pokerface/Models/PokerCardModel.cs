using Pokerface.Services;

namespace Pokerface.Models
{
    public class PokerCardModel
    {
          
        public string ImageUrl { get; private set; } = string.Empty;
        public bool IsVisible { get; private set; }

        //Default Constructor
        public PokerCardModel()
        {
            Init(EnumCardSuit.Spade, EnumCardRank.Ace, false);
        }

        //Constructor for new GamePlay
        public PokerCardModel(Card card)
        {
            Init(card.Suit, card.Rank, false);
        }


        public void Init(EnumCardSuit suit, EnumCardRank rank, bool showFront)
        {         

            ImageUrl = showFront
                ? CardSvgProvider.GetFrontsideSvg(rank, suit)
                : CardSvgProvider.GetBacksideSvg();
        }
    }
}
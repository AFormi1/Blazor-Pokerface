using Pokerface.Services;

namespace Pokerface.Models
{
    public class PokerCardModel
    {
          
        public string ImageUrl { get; private set; } = string.Empty;
        public bool IsVisible { get; private set; }
        public EnumCardSuit Suit { get; private set; }
        public EnumCardRank Rank { get; private set; }

        //Default Constructor
        public PokerCardModel()
        {
            ImageUrl = CardSvgProvider.GetBacksideSvg();
        }

        //Constructor for new GamePlay
        public PokerCardModel(Card card)
        {
            ImageUrl = CardSvgProvider.GetBacksideSvg();
        }


    }
}
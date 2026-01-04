using Pokerface.Services;

namespace Pokerface.Models
{
    public class PokerCardModel
    {
          
        public string ImageUrl { get; private set; } = string.Empty;
        public bool IsVisible { get; private set; }
        public bool ShowFace { get; set; }
        public EnumCardSuit Suit { get; private set; }
        public EnumCardRank Rank { get; private set; }

        //Default Constructor
        public PokerCardModel()
        {
            ImageUrl = CardSvgProvider.GetBacksideSvg();
        }

        //Constructor for new GamePlay
        public PokerCardModel(Card card, bool showFace)
        {
            Suit = card.Suit;
            Rank = card.Rank;
            ShowFace = showFace;
            ImageUrl = ShowFace ? CardSvgProvider.GetFrontsideSvg(Suit, Rank) : CardSvgProvider.GetBacksideSvg();
        }


    }
}
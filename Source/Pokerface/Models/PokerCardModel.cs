using Pokerface.Services;

namespace Pokerface.Models
{
    public class PokerCardModel
    {
        private readonly CardProvider _cardProvider;

        public double X { get; private set; }
        public double Y { get; private set; }
        public double Width { get; private set; }
        public double Height { get; private set; }
        public double Rot { get; private set; }
        public string ImageUrl { get; private set; } = string.Empty;

        public PokerCardModel(CardProvider provider)
        {
            _cardProvider = provider;
        }

        public void Init(EnumCardSuit suit, EnumCardRank rank, bool showFront)
        {
            ImageUrl = showFront
                ? _cardProvider.GetFrontsideSvg(rank, suit)
                : _cardProvider.GetBacksideSvg();
        }

        public void Update(CardProvider provider, EnumCardPositions position)
        {
            var rect = provider.GetCardRect(position);

            X = rect.X;
            Y = rect.Y;
            Width = rect.Width;
            Height = rect.Height;

            Rot = 0;
            if (position == EnumCardPositions.Player8Card1 || position == EnumCardPositions.Player8Card2)
            {
                Width = rect.Height;
                Height = rect.Width;
                Rot = 90;
            }
            if (position == EnumCardPositions.Player4Card1 || position == EnumCardPositions.Player4Card2)
            {
                Width = rect.Height;
                Height = rect.Width;
                Rot = 270;
            }
        }
    }

}
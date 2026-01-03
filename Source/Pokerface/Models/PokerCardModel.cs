using Pokerface.Models;
using Pokerface.Services;

public class PokerCardModel
{
    private readonly CardProvider _cardProvider;

    public double WidthPercent { get; private set; }
    public double HeightPercent { get; private set; }

    public double LeftPercent { get; private set; }
    public double TopPercent { get; private set; }
    public double Rotation { get; private set; }

    public string ImageUrl { get; private set; } = string.Empty;

    public PokerCardModel(CardProvider cardProvider)
    {
        _cardProvider = cardProvider;
    }

    public void Init(EnumCardSuit suit, EnumCardRank rank, bool showFront)
    {
        ImageUrl = showFront
            ? _cardProvider.GetFrontsideSvg(rank, suit)
            : _cardProvider.GetBacksideSvg();
    }

    public void UpdateForTableLayout(DomRect tableSize, EnumCardPositions tablePosition, bool ShowFront)
    {
        if (!_cardProvider.Positions.TryGetValue(tablePosition, out var layout))
            return;

        double cardScaleFactor = 0.009;
        double baseRatio = 363.0 / 543.0;

        // Compute the "base" card height and width relative to table size
        // We can use the smaller dimension to keep cards proportional
        double baseHeight = tableSize.Height * cardScaleFactor;
        double baseWidth = baseHeight * baseRatio;

        // Assign to card
        WidthPercent = baseWidth;
        HeightPercent = baseHeight;

        LeftPercent = layout.X;
        TopPercent = layout.Y;
        Rotation = layout.Rotation;
    }


}

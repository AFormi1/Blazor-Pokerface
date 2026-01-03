using Pokerface.Models;
using Pokerface.Services;

public class PokerCardModel
{
    private readonly CardTemplateProvider _cardProvider;

    public double WidthPercent { get; private set; }
    public double HeightPercent { get; private set; }

    public double LeftPercent { get; private set; }
    public double TopPercent { get; private set; }

    public string ImageUrl { get; private set; } = string.Empty;

    public PokerCardModel(CardTemplateProvider cardProvider)
    {
        _cardProvider = cardProvider;
    }

    public void Init(EnumCardSuit suit, EnumCardRank rank, bool showFront)
    {
        ImageUrl = showFront
            ? _cardProvider.GetFrontsideSvg(rank, suit)
            : _cardProvider.GetBacksideSvg();
    }

    public void Update(DomRect tableSize, double columnIndex, double rowIndex, int totalColumns, int totalRows)
    {
        if (tableSize.Width == 0 || tableSize.Height == 0)
            return;

        // Width and height as % of table
        WidthPercent = 100.0 / totalColumns * 0.9;   // 90% of cell width
        HeightPercent = 100.0 / totalRows * 0.9;     // 90% of cell height

        // Left and top as % of container
        LeftPercent = (100.0 / totalColumns) * (columnIndex + 0.5); // center in cell
        TopPercent = (100.0 / totalRows) * (rowIndex + 0.5);
    }
}

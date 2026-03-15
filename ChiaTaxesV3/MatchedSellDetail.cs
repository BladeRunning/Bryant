namespace ChiaTaxes;

public class MatchedSellDetail 
{ 
    public DateTime SellDate { get; set; }

    public decimal SellPrice { get; set; }

    public decimal SellAmount { get; set; }

    public DateTime BuyDate { get; set; }

    public decimal BuyPrice { get; set; }

    public decimal XCHAmount { get; set; }

    public decimal CostBasis => BuyPrice * XCHAmount;

    public decimal Proceeds => SellPrice * XCHAmount;

    public decimal ProfitLoss => Proceeds - CostBasis;
}
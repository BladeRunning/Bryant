namespace ChiaTaxesV3.Form8949Generator.Models
{
    public class Form8949Row
    {
        public string Description { get; set; }

        public DateTime DateAcquired { get; set; }

        public DateTime DateSold { get; set; }

        public decimal Proceeds { get; set; }

        public decimal CostBasis { get; set; }

        public string? Codes { get; set; }

        public decimal? Adjustment { get; set; }

        public decimal GainOrLoss { get; set; }
    }
}

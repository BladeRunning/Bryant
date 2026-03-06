namespace ChiaTaxes;

public class AllTransactions
{
    public DateTime confirmed_time { get; set; }

    public string sender_address { get; set; }

    public Decimal amount { get; set; }
}
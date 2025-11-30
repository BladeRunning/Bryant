using Dapper;
using Microsoft.Data.SqlClient;

namespace ChiaTaxes;

// --STEPS--
// download the daily stock data from finance.yahoo.com -> XCH-USD 
// import into Historical Daily SSMS table
// use the spacescan api and run the test transaction swagger on the mining wallet and the farming wallet
// https://www.spacescan.io/apis
// export json
// use online convert json->csv
// open csv in excel and save to .xls format
// import into SSMS table from excel with import tool
// two different models and queries because the spacescan column names were different?

internal class Program
{
    static void Main()
    {
        GenerateSellProfitLossCSVFile();

        //GenerateTransactionsOutput(2024);
    }

    private static void GenerateTransactionsOutput(int year)
    {
        // headers in output
        Console.WriteLine("Historical Date,Transaction Date,XCH Amount,Price Avg,Revenue");

        GenerateBlockWinsCSV(year);

        Console.WriteLine("");

        GeneratePoolRewardsCSV(year);
    }

    /// <summary>
    /// Displays the cost basis of each mining and block win transactions excluding transfers from internal wallets.
    /// </summary>
    private static void GenerateSellProfitLossCSVFile()
    {
        var ExcludedFromConfigSettings = GetConfigSettings<string[]>("ExcludedPoolRewardWinSenders");
        string[] excludeSenders = ExcludedFromConfigSettings;

        // historical price data is in separate tables by year
        var historicalDailyPrices = Query<HistoricalDaily>("SELECT [Date], [Open], [High], [Low]\r\nFROM HistoricalDaily2022\r\nUNION ALL\r\nSELECT [Date], [Open], [High], [Low]\r\nFROM HistoricalDaily2023\r\nUNION ALL\r\nSELECT [Date], [Open], [High], [Low]\r\nFROM HistoricalDaily2024\r\nORDER BY [Date]");

        var transactions = Query<Transactions>("Select [data__in__xch__coinfirmed_time], [data__in__xch__sender__address], [data__in__xch__amount] From Transactions");
        var walletTransactions = from transaction in transactions
                                 join historicalPrice in historicalDailyPrices
                                 on transaction.data__in__xch__coinfirmed_time.Date equals historicalPrice.Date
                                 where !excludeSenders.Contains(transaction.data__in__xch__sender__address)
                                 orderby transaction.data__in__xch__coinfirmed_time.Date
                                 select new
                                 {
                                     HistoricalDate = historicalPrice.Date.ToString("MM/dd/yyyy"),
                                     TransactionDate = transaction.data__in__xch__coinfirmed_time,
                                     PriceAvg = (historicalPrice.Low + historicalPrice.High) / 2,
                                     XCHAmount = transaction.data__in__xch__amount,
                                 };

        string filePath = "transaction history.csv";
        using (StreamWriter writer = new StreamWriter(filePath, true))
        {
            bool fileExists = File.Exists(filePath);

            if (!fileExists)
            {
                writer.WriteLine("HistoricalDate,TransactionDate,XCHAmount,PriceAvg,TotalValue");
            }

            foreach (var transaction in walletTransactions)
            {
                writer.WriteLine($"{transaction.HistoricalDate},{transaction.TransactionDate},{transaction.XCHAmount},{transaction.PriceAvg},{transaction.PriceAvg * transaction.XCHAmount}");
            }
        }

        Console.WriteLine($"Data written to {filePath}");
    }

    private static void GeneratePoolRewardsCSV(int taxYear)
    {
        var ExcludedFromConfigSettings = GetConfigSettings<string[]>("ExcludedPoolRewardWinSenders");
        string[] excludeSenders = ExcludedFromConfigSettings;

        var historicalDailyPrices = Query<HistoricalDaily>("Select [Date], [Open],[High], [Low] From HistoricalDaily" + taxYear);
        var transactions = Query<Transactions>("Select [data__in__xch__coinfirmed_time], [data__in__xch__sender__address], [data__in__xch__amount] From Transactions");

        var walletTransactions = from transaction in transactions
                                 join historicalPrice in historicalDailyPrices
                                 on transaction.data__in__xch__coinfirmed_time.Date equals historicalPrice.Date
                                 where !excludeSenders.Contains(transaction.data__in__xch__sender__address)
                                 where historicalPrice.Date.Year == taxYear 
                                 orderby transaction.data__in__xch__coinfirmed_time.Date
                                 select new
                                 {
                                     HistoricalDate = historicalPrice.Date.ToString("MM/dd/yyyy"),
                                     TransactionDate = transaction.data__in__xch__coinfirmed_time,
                                     PriceAvg = (historicalPrice.Low + historicalPrice.High) / 2,
                                     XCHAmount = transaction.data__in__xch__amount,
                                 };

        foreach (var transaction in walletTransactions)
        {
            Console.WriteLine($"{transaction.HistoricalDate},{transaction.TransactionDate},{transaction.XCHAmount},{transaction.PriceAvg},{transaction.PriceAvg * transaction.XCHAmount}");
        }
    }

    private static void GenerateBlockWinsCSV(int taxYear)
    {
        var ExcludedFromConfigSettings = GetConfigSettings<string[]>("ExcludedBlockWinSenders");
        string[] excludeSenders = ExcludedFromConfigSettings;

        var historicalDailyPrices = Query<HistoricalDaily>("Select [Date], [Open],[High], [Low] From HistoricalDaily" + taxYear);
        var transactionsBlockWins = Query<BlockWins>("Select coinfirmed_time, amount From TransactionsBlockWins");

        var walletTransactions = from transaction in transactionsBlockWins
                                 join historicalPrice in historicalDailyPrices
                                 on transaction.coinfirmed_time.Date equals historicalPrice.Date
                                 where historicalPrice.Date.Year == taxYear
                                 orderby transaction.coinfirmed_time.Date
                                 select new
                                 {
                                     HistoricalDate = historicalPrice.Date.ToString("MM/dd/yyyy"),
                                     TransactionDate = transaction.coinfirmed_time,
                                     PriceAvg = (historicalPrice.Low + historicalPrice.High) / 2,
                                     XCHAmount = transaction.amount,
                                 };

        foreach (var transaction in walletTransactions)
        {
            Console.WriteLine($"{transaction.HistoricalDate},{transaction.TransactionDate},{transaction.XCHAmount},{transaction.PriceAvg},{transaction.PriceAvg * transaction.XCHAmount}");
        }
    }

    private static List<T> Query<T>(string sql)
    {
        IConfigurationBuilder builder = new ConfigurationBuilder().AddJsonFile("appsettings.json", false, true);
        IConfigurationRoot root = builder.Build();

        var connectionString = root.GetConnectionString("DefaultConnection");

        using (var connection = new SqlConnection(connectionString))
        {
            return connection.Query<T>(sql).ToList();
        }
    }

    private static string[] GetConfigSettings<T>(string name)
    {
        IConfigurationBuilder builder = new ConfigurationBuilder().AddJsonFile("appsettings.json", false, true);
        IConfigurationRoot root = builder.Build();

        return root.GetSection(name).Get<string[]>();
    }
}
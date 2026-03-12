using ChiaTaxesV3.Form8949Generator;
using Dapper;
using Microsoft.Data.SqlClient;
using QuestPDF.Infrastructure;

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
        //GenerateTransactionsOutput(2025);

        //GenerateSellProfitLoss();

        QuestPDF.Settings.License = LicenseType.Community;
        var rows = Get8949CsvRows.ConvertCsvTo8949("Form8949Generator/profit loss.csv");
        var outputPath = "Form8949Generator/8949 output.pdf";
        var templatePath = "Form8949Generator/f8949.pdf";

        // generate 8949 pdf from csv
        Form8949Generator.Create(isShortTerm: true, templatePath, outputPath, rows);
    }

    private static void GenerateSellProfitLoss()
    {
        var historicalDailyPrices = Query<HistoricalDaily>("SELECT [Date], [Open], [High], [Low] FROM \r\n(\r\nSELECT * FROM Chia.dbo.HistoricalDaily2022\r\nUNION ALL\r\nSELECT * FROM Chia.dbo.HistoricalDaily2023\r\nUNION ALL\r\nSELECT * FROM Chia.dbo.HistoricalDaily2024\r\nUNION ALL\r\nSELECT * FROM Chia.dbo.HistoricalDaily2025\r\n) as PriceHistory\r\nORDER BY Date");
        var transactions = Query<AllTransactions>("SELECT * FROM (SELECT\r\n    confirmed_time,\r\n    height,\r\n    amount,\r\n    amount_mojo,\r\n    [to],\r\n    sender_address\r\nFROM dbo.BlockWinView\r\nUNION ALL\r\nSELECT\r\n    confirmed_time,                    \r\n    confirmed AS height,               \r\n    amount,\r\n    amount_mojo,\r\n    receiver_address AS [to],     \r\n    sender_address\r\nFROM dbo.PayoutView) AS unified \r\nORDER BY unified.height;");
        var sellTransactions = Query<SellTransactions>("SELECT * FROM TransactionsSells ORDER BY height;\r\n");

        var setPriceForSellTransactions = from transaction in sellTransactions
                                          join historicalPrice in historicalDailyPrices
                                          on transaction.time.Date equals historicalPrice.Date
                                          orderby transaction.time.Date
                                          select new
                                          {
                                              HistoricalDate = historicalPrice.Date.ToString("MM/dd/yyyy"),
                                              TransactionDate = transaction.time,
                                              PriceAvg = (historicalPrice.Low + historicalPrice.High) / 2,
                                              XCHAmount = transaction.amount,
                                          };

        string sellFilePath = "sell transactions.csv";
        using (StreamWriter writer = new StreamWriter(sellFilePath, true))
        {
            writer.WriteLine("HistoricalDate,TransactionDate,XCHAmount,PriceAvg,TotalValue");

            foreach (var transaction in setPriceForSellTransactions)
            {
                writer.WriteLine($"{transaction.HistoricalDate},{transaction.TransactionDate},{transaction.XCHAmount},{transaction.PriceAvg},{transaction.PriceAvg * transaction.XCHAmount}");
            }
        }

        Console.WriteLine($"Data written to {sellFilePath}");

        var setPriceForTransactions = from transaction in transactions
                                 join historicalPrice in historicalDailyPrices
                                 on transaction.confirmed_time.Date equals historicalPrice.Date
                                 orderby transaction.confirmed_time.Date
                                 select new
                                 {
                                     HistoricalDate = historicalPrice.Date.ToString("MM/dd/yyyy"),
                                     TransactionDate = transaction.confirmed_time,
                                     PriceAvg = (historicalPrice.Low + historicalPrice.High) / 2,
                                     XCHAmount = transaction.amount,
                                 };

        // rework linq to output MatchedSellDetail list and remove duplicate code for buylist and selllist
        var buyList = setPriceForTransactions
        .Select(t => (t.TransactionDate, t.XCHAmount, t.PriceAvg))
        .OrderBy(t => t.TransactionDate)
        .ToList();

        var sellList = setPriceForSellTransactions
            .Select(t => (t.TransactionDate, Math.Abs(t.XCHAmount), t.PriceAvg))
            .OrderBy(t => t.TransactionDate)
            .ToList();
        //

        var fifoResults = ComputeFifoProfitLoss(buyList, sellList);

        string filePath = "transaction profit loss.csv";
        using (StreamWriter writer = new StreamWriter(filePath, true))
        {
            writer.WriteLine("SellDate, BuyDate, XCHAmount, BuyPrice, SellPrice, CostBasis, Proceeds, ProfitLoss");

            foreach (var r in fifoResults)
            {
                writer.WriteLine($"{r.SellDate}, {r.BuyDate}, {r.XCHAmount}, {r.BuyPrice}, {r.SellPrice}, {r.CostBasis}, {r.Proceeds}, {r.ProfitLoss}");
            }
        }


        Console.WriteLine($"Data written to {filePath}");
    }

    private static List<MatchedSellDetail> ComputeFifoProfitLoss(List<(DateTime Date, decimal Amount, decimal Price)> buys, List<(DateTime Date, decimal Amount, decimal Price)> sells)
    {
        var results = new List<MatchedSellDetail>();

        // FIFO queue of remaining transaction / buy amounts - buys = payouts + block wins
        var buyQueue = new Queue<(DateTime Date, decimal Amount, decimal Price)>(buys);

        foreach (var sell in sells)
        {
            decimal remainingToMatch = sell.Amount;

            while (remainingToMatch > 0 && buyQueue.Count > 0)
            {
                var buy = buyQueue.Peek();
                decimal matched = Math.Min(remainingToMatch, buy.Amount);

                results.Add(new MatchedSellDetail
                {
                    SellDate = sell.Date,
                    SellPrice = sell.Price,
                    SellAmount = sell.Amount,

                    BuyDate = buy.Date,
                    BuyPrice = buy.Price,
                    XCHAmount = matched
                });

                // Reduce buy amount
                var leftover = buy.Amount - matched;
                buyQueue.Dequeue();

                if (leftover > 0)
                {
                    buyQueue.Enqueue((buy.Date, leftover, buy.Price));
                }

                remainingToMatch -= matched;
            }
        }

        return results;
    }

    private static void GenerateTransactionsOutput(int year)
    {
        // headers in output
        Console.WriteLine("Historical Date,Transaction Date,XCH Amount,Price Avg,Revenue");

        GenerateBlockWinsCSV(year);

        Console.WriteLine("");

        GeneratePoolRewardsCSV(year);
    }

    private static void GeneratePoolRewardsCSV(int taxYear)
    {
        var includeWalletsFromConfig = GetConfigSettings<string[]>("ExcludedPoolRewardWinSenders");
        string[] includeSenders = includeWalletsFromConfig;

        var historicalDailyPrices = Query<HistoricalDaily>("Select [Date], [Open], [High], [Low] From HistoricalDaily" + taxYear);
        var transactions = Query<Transactions>("SELECT time as confirmed_time, height, amount, mojo, [to], [from] as sender_address \r\n    FROM [Chia].[dbo].[xchjhqv2025]\r\n    UNION ALL\r\n    SELECT confirmed_time, coinfirmed AS height, amount, amount_mojo AS mojo,\r\n           [receiver/address] AS [to], [sender/address] AS sender_address\r\n    FROM [Chia].[dbo].[xchfkkglv2025]");

        var walletTransactions = from transaction in transactions
                                 join historicalPrice in historicalDailyPrices
                                 on transaction.confirmed_time.Date equals historicalPrice.Date
                                 where includeSenders.Contains(transaction.sender_address)
                                 where historicalPrice.Date.Year == taxYear 
                                 orderby transaction.confirmed_time.Date
                                 select new
                                 {
                                     HistoricalDate = historicalPrice.Date.ToString("MM/dd/yyyy"),
                                     TransactionDate = transaction.confirmed_time,
                                     PriceAvg = (historicalPrice.Low + historicalPrice.High) / 2,
                                     XCHAmount = transaction.amount,
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
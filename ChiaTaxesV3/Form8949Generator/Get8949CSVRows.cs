using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using System.Text;

namespace ChiaTaxesV3.Form8949Generator;

public class Get8949CsvRows
{
    public static List<Form8949Row> ConvertCsvTo8949(string csvPath)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            TrimOptions = TrimOptions.Trim,
            IgnoreBlankLines = true,
            BadDataFound = null,
            DetectDelimiter = true
        };

        using var reader = new StreamReader(csvPath, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        using var csv = new CsvReader(reader, config);

        if (!csv.Read())
            throw new Exception("CSV is empty or unreadable.");

        csv.ReadHeader();
        var rows = new List<Form8949Row>();

        while (csv.Read())
        {
            var row = new Form8949Row
            {
                Description = csv.GetField("description"),
                DateAcquired = csv.GetField<DateTime>("acquired"),
                DateSold = csv.GetField<DateTime>("sold"),
                Proceeds = csv.GetField<decimal>("proceeds"),
                Codes = csv.GetField("codes"),
                Adjustment = csv.GetField<decimal>("adjustment"),
                CostBasis = csv.GetField<decimal>("costbasis"),
                GainOrLoss = csv.GetField<decimal>("gainloss")
            };

            rows.Add(row);
        }

        return rows;
    }
}





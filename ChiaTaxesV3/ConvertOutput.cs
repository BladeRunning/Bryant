using System.Text.RegularExpressions;

namespace ChiaTaxes
{
    public class ConvertOutput
    {
        public void FileToCSV(string inputFilename, string outputFilename)
        {
            // Read the content of the file
            string content = File.ReadAllText(inputFilename);

            string pattern = @"Transaction (\S+).*?Amount rewarded: (\S+).*?To address: (\S+).*?Created at: (\S+)";
            var regex = new Regex(pattern, RegexOptions.Singleline);
            MatchCollection matches = regex.Matches(content);

            List<string[]> rows = new List<string[]>();
            // Header row
            rows.Add(new string[] { "Transaction", "Amount", "To address",  "Date" }); 

            foreach (Match match in matches)
            {
                if (match.Groups.Count > 3)
                {
                    string transactionId = match.Groups[1].Value;
                    string amount = match.Groups[2].Value;
                    string toAddress = match.Groups[3].Value;
                    string date = match.Groups[4].Value;

                    // Add a row to the list of CSV rows
                    rows.Add(new string[] { transactionId, amount, toAddress, date });
                }
            }

            // Write the results to a CSV file
            using (var writer = new StreamWriter(outputFilename))
            {
                foreach (var row in rows)
                {
                    writer.WriteLine(string.Join(",", row));
                }
            }

            Console.WriteLine("Transactions exported to " + outputFilename);
        }

    }
}

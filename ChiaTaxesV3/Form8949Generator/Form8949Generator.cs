using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using PdfSharpCore.Drawing;
using System.Globalization;

namespace ChiaTaxesV3.Form8949Generator;

public class Form8949Generator
{
    public static void Create(bool isShortTerm, string templatePath, string outputPath, List<Form8949Row> rows)
    {
        var templateDoc = PdfReader.Open(templatePath, PdfDocumentOpenMode.Import);
        var page = isShortTerm ? 0 : 1;
        var templatePage = templateDoc.Pages[page];

        var outputDoc = new PdfDocument();
        var font = new XFont("Arial", 9);

        double startY = 375;
        double rowHeight = 24;
        int rowsPerPage = 11;

        decimal proceedsTotal = 0;
        decimal costBasisTotal = 0;
        decimal gainOrLossTotal = 0;

        int index = 0;
        XGraphics gfx = null;

        foreach (var row in rows)
        {
            // Start a new page when needed
            if (index % rowsPerPage == 0)
            {
                // If this is not the first page, draw totals for the previous page
                if (index > 0)
                {
                    gfx.DrawString(proceedsTotal.ToString("F2", CultureInfo.InvariantCulture), font, XBrushes.Black, new XPoint(280, 655));
                    gfx.DrawString(costBasisTotal.ToString("F2", CultureInfo.InvariantCulture), font, XBrushes.Black, new XPoint(345, 655));
                    gfx.DrawString(gainOrLossTotal.ToString("F2", CultureInfo.InvariantCulture), font, XBrushes.Black, new XPoint(520, 655));
                }

                // Reset totals for the new page
                proceedsTotal = 0;
                costBasisTotal = 0;
                gainOrLossTotal = 0;

                // Create new page
                var newPage = outputDoc.AddPage(templatePage);
                gfx = XGraphics.FromPdfPage(newPage);

                // Draw x in box at top of form - long-term transactions reported on 1099-B or 1099-DA etc
                // One option is used
                //const int boxDPos = 225;
                //const int boxEPos = 237;
                //const int boxFPos = 249;
                //const int boxJPos = 261;
                //const int boxKPos = 274;
                const int boxLPos = 285;

                // Draw X in box
                var xFont = new XFont("Arial", 13);
                gfx.DrawString("x", xFont, XBrushes.Black, new XPoint(51, boxLPos));
            }

            double y = startY + (index % rowsPerPage) * rowHeight;

            // Draw row text
            gfx.DrawString(row.Description, font, XBrushes.Black, new XPoint(40, y));
            gfx.DrawString(row.DateAcquired.ToShortDateString(), font, XBrushes.Black, new XPoint(175, y));
            gfx.DrawString(row.DateSold.ToShortDateString(), font, XBrushes.Black, new XPoint(225, y));

            // Round values
            var roundedProceeds = row.Proceeds.ToString("F2", CultureInfo.InvariantCulture);
            var roundedCostBasis = row.CostBasis.ToString("F2", CultureInfo.InvariantCulture);
            var roundedGainLoss = row.GainOrLoss.ToString("F2", CultureInfo.InvariantCulture);

            // Draw rounded values
            gfx.DrawString(roundedProceeds, font, XBrushes.Black, new XPoint(280, y));
            gfx.DrawString(roundedCostBasis, font, XBrushes.Black, new XPoint(345, y));
            gfx.DrawString(roundedGainLoss, font, XBrushes.Black, new XPoint(520, y));

            // Add to per‑page totals
            proceedsTotal += decimal.Parse(roundedProceeds, CultureInfo.InvariantCulture);
            costBasisTotal += decimal.Parse(roundedCostBasis, CultureInfo.InvariantCulture);
            gainOrLossTotal += decimal.Parse(roundedGainLoss, CultureInfo.InvariantCulture);

            // Draw totals on the last page
            if (index == rows.Count - 1)
            {
                gfx.DrawString(proceedsTotal.ToString("F2", CultureInfo.InvariantCulture), font, XBrushes.Black, new XPoint(280, 655));
                gfx.DrawString(costBasisTotal.ToString("F2", CultureInfo.InvariantCulture), font, XBrushes.Black, new XPoint(345, 655));
                gfx.DrawString(gainOrLossTotal.ToString("F2", CultureInfo.InvariantCulture), font, XBrushes.Black, new XPoint(520, 655));
            }

            index++;
        }

        outputDoc.Save(outputPath);
    }
}
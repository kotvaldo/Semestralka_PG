using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Semestralka_PG
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.IO;
    using System.Windows.Forms;
    using OfficeOpenXml;
    using OfficeOpenXml.Drawing.Chart;

    public class Profiler
    {
        public List<int> Data { get; set; } = new List<int>();
        Stopwatch Stopwatch { get; set; } = new Stopwatch();

        public List<String> Names  {get; set;} 
        public Profiler()
        {
            ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;
        }

        public void StartNewSequence()
        {
            Stopwatch = new Stopwatch();
            Stopwatch.Start();
        }

        public void ClearData()
        {
            Data.Clear();
        }
        public void StopNewSequence()
        {
            Stopwatch.Stop();
            Data.Add((int)Stopwatch.ElapsedMilliseconds);
            
        }
        public void CreateFileWithChartGroupedByAlgorithm()
        {
            if (Data.Count == 0)
            {
                throw new InvalidOperationException("Nie sú žiadne dáta na uloženie.");
            }

            int algorithmsCount = 7; // Počet algoritmov

            string projectDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string dataFolder = Path.Combine(projectDirectory, "Data");

            if (!Directory.Exists(dataFolder))
            {
                Directory.CreateDirectory(dataFolder);
            }

            string filePath = Path.Combine(dataFolder, $"Data_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");

            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Data");


               
                for (int i = 0; i < algorithmsCount; i++)
                {
                    worksheet.Cells[i + 2, 1].Value = Names;
                }
                worksheet.Cells[algorithmsCount + 2, 1].Value = "Total";

                for (int i = 0; i < Data.Count; i++)
                {
                    int row = (i % algorithmsCount) + 2; 
                    int column = (i / algorithmsCount) + 2;

                    worksheet.Cells[row, column].Value = Data[i];
                }

                for (int i = 0; i < algorithmsCount; i++)
                {
                    int row = i + 2;
                    worksheet.Cells[row, Data.Count / algorithmsCount + 2].Formula = $"AVERAGE({worksheet.Cells[row, 2].Address}:{worksheet.Cells[row, Data.Count / algorithmsCount + 1].Address})";
                }
                for (int col = 2; col <= Data.Count / algorithmsCount + 1; col++)
                {
                    worksheet.Cells[1, col].Value = $"{col - 2}.";
                }

                for (int col = 2; col <= Data.Count / algorithmsCount + 1; col++)
                {
                    worksheet.Cells[algorithmsCount + 2, col].Formula = $"SUM({worksheet.Cells[2, col].Address}:{worksheet.Cells[algorithmsCount + 1, col].Address})";
                }

          
                var totalChart = worksheet.Drawings.AddChart("TotalChart", eChartType.ColumnClustered);
                totalChart.Title.Text = "Total Times per Algorithm";
                totalChart.SetPosition(15, 0, 15, 15);
                totalChart.SetSize(800, 400);

                var totalSeries = totalChart.Series.Add(worksheet.Cells[2, Data.Count / algorithmsCount + 2, algorithmsCount + 1, Data.Count / algorithmsCount + 2], worksheet.Cells[2, 1, algorithmsCount + 1, 1]);
                totalSeries.Header = "AVG time";

                var columnSumChart = worksheet.Drawings.AddChart("ColumnSumChart", eChartType.Line);
                columnSumChart.Title.Text = "Total time of sequence";
                columnSumChart.SetPosition(15, 0, 0, 3);
                columnSumChart.SetSize(800, 400);

                var columnSumSeries = columnSumChart.Series.Add(worksheet.Cells[algorithmsCount + 2, 2, algorithmsCount + 2, Data.Count / algorithmsCount + 1], worksheet.Cells[1, 2, 1, Data.Count / algorithmsCount + 1]);
                columnSumSeries.Header = "Total time";


                package.SaveAs(new FileInfo(filePath));
            }

            MessageBox.Show($"Data were saved to {filePath}");
        }

    }

}

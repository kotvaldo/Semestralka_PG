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
        public void CreateFileWithChart()
        {
            if (Data.Count == 0)
            {
                throw new InvalidOperationException("Nie sú žiadne dáta na uloženie.");
            }
            int avg = (int)Data.Average(i => (double)i);

            MessageBox.Show($"Average time of algorithm for {Data.Count} images : {avg} ms ");
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

                worksheet.Cells[1, 1].Value = "Index";
                worksheet.Cells[1, 2].Value = "Hodnota";
                for (int i = 0; i < Data.Count; i++)
                {
                    worksheet.Cells[i + 2, 1].Value = i + 1;
                    worksheet.Cells[i + 2, 2].Value = Data[i];
                }


                var chart = worksheet.Drawings.AddChart("DataChart", eChartType.Line);
                chart.Title.Text = "Chart of Values";
                chart.SetPosition(0, 0, 3, 0);
                chart.SetSize(800, 400);

                var series = chart.Series.Add(worksheet.Cells[2, 2, Data.Count + 1, 2], worksheet.Cells[2, 1, Data.Count + 1, 1]);
                series.Header = "Values";


                package.SaveAs(new FileInfo(filePath));
            }

        }
    }

}

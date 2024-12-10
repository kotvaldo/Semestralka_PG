using System;
using System.Diagnostics;

namespace Semestralka_PG
{
    public class Profiler
    {
        private Stopwatch stopwatch;

        public Profiler()
        {
            stopwatch = new Stopwatch();
        }

        public void ProfileImageGeneration(string filePath, int cycles = 1)
        {
            Console.WriteLine($"Začiatok profilovania... ({cycles} cyklov)");

            ImageProcessor processor = new ImageProcessor(filePath);

            long totalTime = 0;

            for (int i = 0; i < cycles; i++)
            {
                stopwatch.Restart();
                processor.ProcessImage();
                stopwatch.Stop();

                long elapsed = stopwatch.ElapsedMilliseconds;
                totalTime += elapsed;

                Console.WriteLine($"Čas cyklu {i + 1}: {elapsed} ms");
            }

            Console.WriteLine($"Priemerný čas spracovania: {totalTime / (double)cycles} ms");
            Console.WriteLine("Profilovanie ukončené.");
        }
    }
}

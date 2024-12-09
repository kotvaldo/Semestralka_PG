using MathNet.Numerics.Interpolation;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace Semestralka_PG
{
    public class ImageProcessor
    {
        private const int IMAGE_WIDTH = 512;
        private const int IMAGE_HEIGHT = 512;

        private byte[,] luminance;

        // Property pre finálny Bitmap obrázok
        public Bitmap FinalBitmap { get; private set; }

        public ImageProcessor(string filePath)
        {
            luminance = LoadImageFromFile(filePath);
        }

        private byte[,] LoadImageFromFile(string filePath)
        {
            byte[] fileBytes = File.ReadAllBytes(filePath);
            byte[,] lum = new byte[IMAGE_HEIGHT, IMAGE_WIDTH];

            for (int y = 0; y < IMAGE_HEIGHT; y++)
                for (int x = 0; x < IMAGE_WIDTH; x++)
                    lum[y, x] = fileBytes[y * IMAGE_WIDTH + x];

            return lum;
        }

        private double[,] ApplyGaussianFilter(byte[,] image)
        {
            double[,] kernel = {
                { 1, 4, 6, 4, 1 },
                { 4, 16, 24, 16, 4 },
                { 6, 24, 36, 24, 6 },
                { 4, 16, 24, 16, 4 },
                { 1, 4, 6, 4, 1 }
            };

            double kernelSum = kernel.Cast<double>().Sum();
            int offset = kernel.GetLength(0) / 2;

            double[,] result = new double[IMAGE_HEIGHT, IMAGE_WIDTH];

            for (int y = offset; y < IMAGE_HEIGHT - offset; y++)
                for (int x = offset; x < IMAGE_WIDTH - offset; x++)
                {
                    double sum = 0;

                    for (int ky = -offset; ky <= offset; ky++)
                        for (int kx = -offset; kx <= offset; kx++)
                            sum += kernel[ky + offset, kx + offset] * image[y + ky, x + kx];

                    result[y, x] = sum / kernelSum;
                }

            return result;
        }

        private byte[,] ApplyThreshold(byte[,] image)
        {
            int[] histogram = new int[256];
            for (int y = 0; y < IMAGE_HEIGHT; y++)
                for (int x = 0; x < IMAGE_WIDTH; x++)
                    histogram[image[y, x]]++;

            int totalPixels = IMAGE_WIDTH * IMAGE_HEIGHT;

            int sumB = 0, wB = 0, max = 0;
            int sum1 = histogram.Select((t, i) => t * i).Sum();
            int threshold = 0;

            for (int t = 0; t < 256; t++)
            {
                wB += histogram[t];
                if (wB == 0) continue;

                int wF = totalPixels - wB;
                if (wF == 0) break;

                sumB += t * histogram[t];
                int between = wB * wF * (sumB / wB - (sum1 - sumB) / wF) * (sumB / wB - (sum1 - sumB) / wF);

                if (between > max)
                {
                    max = between;
                    threshold = t;
                }
            }

            byte[,] binaryImage = new byte[IMAGE_HEIGHT, IMAGE_WIDTH];
            for (int y = 0; y < IMAGE_HEIGHT; y++)
                for (int x = 0; x < IMAGE_WIDTH; x++)
                    binaryImage[y, x] = (byte)(image[y, x] >= threshold ? 255 : 0);

            return binaryImage;
        }

        private List<PointF> FindCenterLine(byte[,] binaryImage)
        {
            var centerLine = new List<PointF>();

            for (int y = 0; y < IMAGE_HEIGHT; y++)
            {
                int sumX = 0, count = 0;
                for (int x = 0; x < IMAGE_WIDTH; x++)
                {
                    if (binaryImage[y, x] == 0)
                    {
                        sumX += x;
                        count++;
                    }
                }

                if (count > 0)
                    centerLine.Add(new PointF(sumX / (float)count, y));
            }

            return centerLine;
        }

        private List<PointF> FitCurveWithMathNet(List<PointF> points)
        {
            if (points.Count < 2) return points;

            var xVals = points.Select(p => (double)p.Y).ToArray();
            var yVals = points.Select(p => (double)p.X).ToArray();

            var spline = CubicSpline.InterpolateNatural(xVals, yVals);

            var fittedPoints = new List<PointF>();
            for (double y = xVals.Min(); y <= xVals.Max(); y += 1.0)
            {
                double x = spline.Interpolate(y);
                fittedPoints.Add(new PointF((float)x, (float)y));
            }

            return fittedPoints;
        }

        public Bitmap ProcessImage()
        {
            double[,] blurred = ApplyGaussianFilter(luminance);
            byte[,] binaryImage = ApplyThreshold(luminance);
            List<PointF> centerLine = FindCenterLine(binaryImage);
            List<PointF> curve = FitCurveWithMathNet(centerLine);

            FinalBitmap = new Bitmap(IMAGE_WIDTH, IMAGE_HEIGHT);
            for (int y = 0; y < IMAGE_HEIGHT; y++)
                for (int x = 0; x < IMAGE_WIDTH; x++)
                {
                    int value = (int)blurred[y, x];
                    FinalBitmap.SetPixel(x, y, Color.FromArgb(value, value, value));
                }

            using (Graphics g = Graphics.FromImage(FinalBitmap))
            {
                foreach (PointF p in curve)
                    FinalBitmap.SetPixel((int)p.X, (int)p.Y, Color.Red);
            }

            return FinalBitmap;
        }
    }
}

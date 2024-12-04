using MathNet.Numerics.Interpolation;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Semestralka_PG
{
    public partial class Form1 : Form
    {
        private const int ImageWidth = 512;
        private const int ImageHeight = 512;

        public Form1()
        {
            InitializeComponent();
        }

        private void BtnLoadImageClick(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Text Files (*.txt)|*.txt",
                Title = "Select Text Image File"
            };

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                byte[,] luminance = LoadYChannel(openFileDialog.FileName);

                // Apply a Gaussian filter
                double[,] blurred = ApplyGaussianFilter(luminance);

                // Adaptive threshold using Otsu
                byte[,] binaryImage = ApplyThreshold(luminance, OtsuThreshold(luminance));

                // Edge detection using Sobel operator
                byte[,] edges = SobelEdgeDetection(luminance);

                // Curve fitting
                List<PointF> centerLine = FindCenterLine(binaryImage);
                List<PointF> fittedCurve = FitCurveWithMathNet(centerLine);

                // Render the processed image
                pictureBox1.Image = RenderImage(blurred, binaryImage, edges, fittedCurve);
            }
        }

        private List<PointF> FindCenterLine(byte[,] binaryImage)
        {
            var centerLine = new List<PointF>();

            for (int y = 0; y < ImageHeight; y++)
            {
                int sumX = 0, count = 0;
                for (int x = 0; x < ImageWidth; x++)
                {
                    if (binaryImage[y, x] == 0)
                    {
                        sumX += x;
                        count++;
                    }
                }

                if (count > 0)
                {
                    centerLine.Add(new PointF(sumX / (float)count, y));
                }
            }

            return centerLine;
        }

        private byte[,] LoadYChannel(string filePath)
        {
            byte[] fileBytes = File.ReadAllBytes(filePath);
            byte[,] luminance = new byte[ImageHeight, ImageWidth];

            for (int y = 0; y < ImageHeight; y++)
            {
                for (int x = 0; x < ImageWidth; x++)
                {
                    luminance[y, x] = fileBytes[y * ImageWidth + x];
                }
            }

            return luminance;
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
            int kSize = kernel.GetLength(0);
            int offset = kSize / 2;

            double[,] result = new double[ImageHeight, ImageWidth];

            for (int y = offset; y < ImageHeight - offset; y++)
            {
                for (int x = offset; x < ImageWidth - offset; x++)
                {
                    double sum = 0;

                    for (int ky = -offset; ky <= offset; ky++)
                    {
                        for (int kx = -offset; kx <= offset; kx++)
                        {
                            sum += kernel[ky + offset, kx + offset] * image[y + ky, x + kx];
                        }
                    }

                    result[y, x] = sum / kernelSum;
                }
            }

            return result;
        }

        private int[] GenerateHistogram(byte[,] image)
        {
            int[] histogram = new int[256];
            for (int y = 0; y < ImageHeight; y++)
            {
                for (int x = 0; x < ImageWidth; x++)
                {
                    histogram[image[y, x]]++;
                }
            }
            return histogram;
        }

        private int OtsuThreshold(byte[,] image)
        {
            int[] histogram = GenerateHistogram(image);
            int totalPixels = ImageWidth * ImageHeight;

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
                int mB = sumB / wB;
                int mF = (sum1 - sumB) / wF;
                int between = wB * wF * (mB - mF) * (mB - mF);

                if (between > max)
                {
                    max = between;
                    threshold = t;
                }
            }

            return threshold;
        }

        private byte[,] ApplyThreshold(byte[,] image, int threshold)
        {
            byte[,] binaryImage = new byte[ImageHeight, ImageWidth];
            for (int y = 0; y < ImageHeight; y++)
            {
                for (int x = 0; x < ImageWidth; x++)
                {
                    binaryImage[y, x] = (byte)(image[y, x] >= threshold ? 255 : 0);
                }
            }
            return binaryImage;
        }

        private byte[,] SobelEdgeDetection(byte[,] image)
        {
            int[,] gx = {
                { -1, 0, 1 },
                { -2, 0, 2 },
                { -1, 0, 1 }
            };

            int[,] gy = {
                { 1, 2, 1 },
                { 0, 0, 0 },
                { -1, -2, -1 }
            };

            byte[,] edges = new byte[ImageHeight, ImageWidth];

            for (int y = 1; y < ImageHeight - 1; y++)
            {
                for (int x = 1; x < ImageWidth - 1; x++)
                {
                    int sumX = 0, sumY = 0;

                    for (int ky = -1; ky <= 1; ky++)
                    {
                        for (int kx = -1; kx <= 1; kx++)
                        {
                            sumX += gx[ky + 1, kx + 1] * image[y + ky, x + kx];
                            sumY += gy[ky + 1, kx + 1] * image[y + ky, x + kx];
                        }
                    }

                    int magnitude = (int)Math.Sqrt(sumX * sumX + sumY * sumY);
                    edges[y, x] = (byte)Math.Min(255, magnitude);
                }
            }

            return edges;
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

        private Bitmap RenderImage(double[,] blurred, byte[,] binaryImage, byte[,] edges, List<PointF> curve)
        {
            Bitmap bmp = new Bitmap(ImageWidth, ImageHeight);

            // Draw blurred background
            for (int y = 0; y < ImageHeight; y++)
            {
                for (int x = 0; x < ImageWidth; x++)
                {
                    int value = (int)blurred[y, x];
                    bmp.SetPixel(x, y, Color.FromArgb(value, value, value));
                }
            }

            // Draw endges
            using (Graphics g = Graphics.FromImage(bmp))
            {
                for (int y = 0; y < ImageHeight; y++)
                {
                    for (int x = 0; x < ImageWidth; x++)
                    {
                        if (edges[y, x] > 0)
                        {
                            bmp.SetPixel(x, y, Color.Gray);
                        }
                    }
                }

                // Draw binary image
                for (int y = 0; y < ImageHeight; y++)
                {
                    for (int x = 0; x < ImageWidth; x++)
                    {
                        if (binaryImage[y, x] == 0)
                        {
                            bmp.SetPixel(x, y, Color.Black);
                        }
                    }
                }

                // Draw fitted curve
                if (curve != null && curve.Count > 1)
                {
                    for (int i = 0; i < curve.Count - 1; i++)
                    {
                        g.DrawLine(Pens.Red, curve[i], curve[i + 1]);
                    }
                }
            }

            return bmp;
        }
    }
}

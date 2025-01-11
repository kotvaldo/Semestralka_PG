﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
                Stopwatch stopwatch = new Stopwatch(); // Časovač na meranie času
                stopwatch.Start();

                byte[,] luminance = LoadYChannel(openFileDialog.FileName);

                double[,] blurred = ApplyGaussianFilterDCTVParallel(luminance);

                byte[,] binaryImage = ApplyThresholdParallel(luminance, OtsuThreshold(luminance));

                byte[,] edges = SobelEdgeDetectionParallel(luminance);

                List<PointF> centerLine = FindCenterLine(binaryImage);

                pictureBox1.Image = RenderImageFast(blurred, binaryImage, edges, centerLine);

                stopwatch.Stop(); // Zastavíme časovač
                MessageBox.Show($"Processing completed in {stopwatch.ElapsedMilliseconds} ms", "Processing Time");
            }
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

        private double[,] ApplyGaussianFilterDCTVParallel(byte[,] image)
        {
            int R = 3;
            double sigma = 1.0;

            int kernelSize = 2 * R + 1;
            double[] g = GenerateGaussianKernel(kernelSize, sigma);

            double[,] result = new double[ImageHeight, ImageWidth];

            // Paralelná aplikácia filtra na riadky
            Parallel.For(0, ImageHeight, y =>
            {
                double[] row = new double[ImageWidth];
                for (int x = 0; x < ImageWidth; x++)
                {
                    row[x] = image[y, x];
                }
                double[] filteredRow = ApplyDCTV(row, g);

                for (int x = 0; x < ImageWidth; x++)
                {
                    result[y, x] = filteredRow[x];
                }
            });

            // Paralelná aplikácia filtra na stĺpce
            Parallel.For(0, ImageWidth, x =>
            {
                double[] column = new double[ImageHeight];
                for (int y = 0; y < ImageHeight; y++)
                {
                    column[y] = result[y, x];
                }
                double[] filteredColumn = ApplyDCTV(column, g);

                for (int y = 0; y < ImageHeight; y++)
                {
                    result[y, x] = filteredColumn[y];
                }
            });

            return result;
        }

        // Generovanie Gaussovho jadra
        private double[] GenerateGaussianKernel(int size, double sigma)
        {
            double[] kernel = new double[size];
            int center = size / 2;
            double sigma2 = sigma * sigma;
            double normalization = 1.0 / (Math.Sqrt(2 * Math.PI) * sigma);

            for (int i = 0; i < size; i++)
            {
                int x = i - center;
                kernel[i] = normalization * Math.Exp(-x * x / (2 * sigma2));
            }

            return kernel;
        }

        // Aplikácia DCT-V
        
        private double[] ApplyDCTV(double[] data, double[] kernel)
        {
            int N = data.Length;
            int K = kernel.Length;
            double[] result = new double[N];
            double[] dctKernel = new double[K];

            // Vypočítanie DCT koeficientov jadra
            for (int k = 0; k < K; k++)
            {
                double sum = 0.0;
                for (int n = 0; n < K; n++)
                {
                    sum += kernel[n] * Math.Cos(Math.PI * k * (2 * n + 1) / (2 * K));
                }
                dctKernel[k] = sum;
            }

            // Aplikácia DCT-V na dáta
            for (int i = 0; i < N; i++)
            {
                double sum = 0.0;
                for (int k = 0; k < K; k++)
                {
                    if (i - k >= 0 && i - k < N)
                    {
                        sum += data[i - k] * dctKernel[k];
                    }
                }
                result[i] = sum;
            }

            return result;
        }

        private int[] GenerateHistogram(byte[,] image)
        {
            int[] histogram = new int[256];

            Parallel.For(0, ImageHeight, y =>
            {
                for (int x = 0; x < ImageWidth; x++)
                {
                    lock (histogram)
                    {
                        histogram[image[y, x]]++;
                    }
                }
            });

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

        private byte[,] ApplyThresholdParallel(byte[,] image, int threshold)
        {
            byte[,] binaryImage = new byte[ImageHeight, ImageWidth];

            Parallel.For(0, ImageHeight, y =>
            {
                for (int x = 0; x < ImageWidth; x++)
                {
                    binaryImage[y, x] = (byte)(image[y, x] >= threshold ? 255 : 0);
                }
            });

            return binaryImage;
        }

        private byte[,] SobelEdgeDetectionParallel(byte[,] image)
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

            Parallel.For(1, ImageHeight - 1, y =>
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
            });

            return edges;
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

        private void DrawBezierCurve(Graphics g, List<PointF> points)
        {
            if (points.Count >= 4)
            {
                for (int i = 0; i < points.Count - 3; i += 3)
                {
                    g.DrawBezier(Pens.Red, points[i], points[i + 1], points[i + 2], points[i + 3]);
                }
            }
            else if (points.Count == 2)
            {
                g.DrawLine(Pens.Red, points[0], points[1]);
            }
        }

        private Bitmap RenderImageFast(double[,] blurred, byte[,] binaryImage, byte[,] edges, List<PointF> curve)
        {
            Bitmap bmp = new Bitmap(ImageWidth, ImageHeight);
            Rectangle rect = new Rectangle(0, 0, ImageWidth, ImageHeight);
            BitmapData bmpData = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);

            int stride = bmpData.Stride;
            IntPtr ptr = bmpData.Scan0;
            int bytes = stride * ImageHeight;
            byte[] rgbValues = new byte[bytes];

            Parallel.For(0, ImageHeight, y =>
            {
                for (int x = 0; x < ImageWidth; x++)
                {
                    int index = y * stride + x * 3;
                    int value = (int)blurred[y, x];
                    value = Math.Max(0, Math.Min(255, value));
                    rgbValues[index] = (byte)value; // B
                    rgbValues[index + 1] = (byte)value; // G
                    rgbValues[index + 2] = (byte)value; // R
                }
            });

            System.Runtime.InteropServices.Marshal.Copy(rgbValues, 0, ptr, bytes);
            bmp.UnlockBits(bmpData);

            using (Graphics g = Graphics.FromImage(bmp))
            {
                if (curve != null && curve.Count >= 4)
                {
                    DrawBezierCurve(g, curve);
                }
            }

            return bmp;
        }


    }
}

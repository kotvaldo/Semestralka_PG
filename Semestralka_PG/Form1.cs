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
        const int IMAGE_WIDTH = 512;
        const int IMAGE_HEIGHT = 512;

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
                byte[] fileBytes = File.ReadAllBytes(openFileDialog.FileName);
                byte[,] lum_bytes = new byte[IMAGE_HEIGHT, IMAGE_WIDTH];

                for (int y = 0; y < IMAGE_HEIGHT; y++)
                {
                    for (int x = 0; x < IMAGE_WIDTH; x++)
                    {
                        lum_bytes[y, x] = fileBytes[y * IMAGE_WIDTH + x];
                    }
                }



                Bitmap filteredImage = RenderImage(lum_bytes);

                pictureBox1.Image = filteredImage;

                // Definujte body pre homografiu
                PointF[] srcPoints = {
            new PointF(0, 0),             // Horný ľavý roh
            new PointF(IMAGE_WIDTH - 1, 0), // Horný pravý roh
            new PointF(0, IMAGE_HEIGHT - 1),// Dolný ľavý roh
            new PointF(IMAGE_WIDTH - 1, IMAGE_HEIGHT - 1) // Dolný pravý roh
        };

                PointF[] destPoints = {
            new PointF(100, 100), // Horný ľavý roh
            new PointF(400, 100), // Horný pravý roh
            new PointF(100, 500), // Dolný ľavý roh
            new PointF(400, 500)  // Dolný pravý roh
        };

                float[,] homographyMatrix = ComputeHomographyMatrix(srcPoints, destPoints);

                Bitmap transformedImage = ApplyHomographyToImage((Bitmap)pictureBox1.Image, homographyMatrix);

                pictureBox2.Image = transformedImage;
            }
        }

        private float[,] ComputeHomographyMatrix(PointF[] srcPoints, PointF[] destPoints)
        {
            if (srcPoints.Length != 4 || destPoints.Length != 4)
                throw new ArgumentException("Need exactly 4 source and destination points.");

            var A = new List<double[]>();
            var b = new List<double>();

            for (int i = 0; i < 4; i++)
            {
                float x = srcPoints[i].X, y = srcPoints[i].Y;
                float xPrime = destPoints[i].X, yPrime = destPoints[i].Y;

                A.Add(new double[] { x, y, 1, 0, 0, 0, -xPrime * x, -xPrime * y });
                A.Add(new double[] { 0, 0, 0, x, y, 1, -yPrime * x, -yPrime * y });
                b.Add(xPrime);
                b.Add(yPrime);
            }

            var matrix = MathNet.Numerics.LinearAlgebra.Matrix<double>.Build.DenseOfRows(A);
            var vector = MathNet.Numerics.LinearAlgebra.Vector<double>.Build.DenseOfEnumerable(b);
            var solution = matrix.Solve(vector);

            return new float[3, 3]
            {
        { (float)solution[0], (float)solution[1], (float)solution[2] },
        { (float)solution[3], (float)solution[4], (float)solution[5] },
        { (float)solution[6], (float)solution[7], 1 }
            };
        }

        private Bitmap ApplyHomographyToImage(Bitmap source, float[,] homographyMatrix)
        {
            Bitmap transformed = new Bitmap(IMAGE_WIDTH, IMAGE_HEIGHT);

            for (int y = 0; y < IMAGE_HEIGHT; y++)
            {
                for (int x = 0; x < IMAGE_WIDTH; x++)
                {
                    float newX = homographyMatrix[0, 0] * x + homographyMatrix[0, 1] * y + homographyMatrix[0, 2];
                    float newY = homographyMatrix[1, 0] * x + homographyMatrix[1, 1] * y + homographyMatrix[1, 2];
                    float w = homographyMatrix[2, 0] * x + homographyMatrix[2, 1] * y + homographyMatrix[2, 2];

                    newX /= w;
                    newY /= w;

                    if (newX >= 0 && newX < IMAGE_WIDTH && newY >= 0 && newY < IMAGE_HEIGHT)
                    {
                        Color color = source.GetPixel(x, y);
                        transformed.SetPixel((int)newX, (int)newY, color);
                    }
                }
            }

            return transformed;
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

            double kernelSum = 0;
            foreach (double value in kernel)
            {
                kernelSum += value;
            }
            int kSize = kernel.GetLength(0);

            int offset = kSize / 2;

            double[,] result = new double[IMAGE_HEIGHT, IMAGE_WIDTH];

            for (int y = offset; y < IMAGE_HEIGHT - offset; y++)
            {
                for (int x = offset; x < IMAGE_WIDTH - offset; x++)
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


        private byte[,] ApplyThreshold(byte[,] image)
        {
            int[] histogram = new int[256];
            for (int y = 0; y < IMAGE_HEIGHT; y++)
            {
                for (int x = 0; x < IMAGE_WIDTH; x++)
                {
                    histogram[image[y, x]]++;
                }
            }

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
                int mB = sumB / wB;
                int mF = (sum1 - sumB) / wF;
                int between = wB * wF * (mB - mF) * (mB - mF);

                if (between > max)
                {
                    max = between;
                    threshold = t;
                }
            }

            byte[,] binaryImage = new byte[IMAGE_HEIGHT, IMAGE_WIDTH];
            for (int y = 0; y < IMAGE_HEIGHT; y++)
            {
                for (int x = 0; x < IMAGE_WIDTH; x++)
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

            byte[,] edges = new byte[IMAGE_HEIGHT, IMAGE_WIDTH];

            for (int y = 1; y < IMAGE_HEIGHT - 1; y++)
            {
                for (int x = 1; x < IMAGE_WIDTH - 1; x++)
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
                {
                    centerLine.Add(new PointF(sumX / (float)count, y));
                }
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


        private Bitmap RenderImage(byte[,] luminance)
        {
            double[,] blurred = ApplyGaussianFilter(luminance);
            byte[,] binaryImage = ApplyThreshold(luminance);
            byte[,] edges = SobelEdgeDetection(luminance);

            List<PointF> centerLine = FindCenterLine(binaryImage);
            List<PointF> curve = FitCurveWithMathNet(centerLine);

            Bitmap bmp = new Bitmap(IMAGE_WIDTH, IMAGE_HEIGHT);

            for (int y = 0; y < IMAGE_HEIGHT; y++)
            {
                for (int x = 0; x < IMAGE_WIDTH; x++)
                {
                    int value = (int)blurred[y, x];
                    bmp.SetPixel(x, y, Color.FromArgb(value, value, value));
                }
            }

            using (Graphics g = Graphics.FromImage(bmp))
            {
                for (int y = 0; y < IMAGE_HEIGHT; y++)
                {
                    for (int x = 0; x < IMAGE_WIDTH; x++)
                    {
                        if (edges[y, x] > 0)
                        {
                            bmp.SetPixel(x, y, Color.Gray);
                        }
                        if (binaryImage[y, x] == 0)
                        {
                            bmp.SetPixel(x, y, Color.Black);
                        }
                    }
                }

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

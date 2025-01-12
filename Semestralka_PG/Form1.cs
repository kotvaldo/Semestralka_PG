using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using MathNet.Numerics.LinearAlgebra;

namespace Semestralka_PG
{
    public partial class Form1 : Form
    {
        private const int ImageWidth = 512;
        private const int ImageHeight = 512;
        private Profiler _profiler = new Profiler();

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
                _profiler.ClearData();
                _profiler.StartNewSequence();

                byte[,] luminance = LoadYChannel(openFileDialog.FileName);

                double[,] blurred = ApplyGaussianFilterDCTVParallel(luminance);

                byte[,] binaryImage = ApplyThresholdParallel(luminance, OtsuThreshold(luminance));
                
                List<PointF> centerLine = FindCenterLine(binaryImage);

                var bezierPoints = FitBezierCurve(centerLine);

                pictureBox1.Image = RenderImageFast(blurred,binaryImage, bezierPoints);

                _profiler.StopNewSequence();
                MessageBox.Show($"Processing completed in {_profiler.Data.First()} ms", "Processing Time");
            }
        }

        private void BtnLoadImageWithCyclic(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Text Files (*.txt)|*.txt",
                Title = "Select Text Image File"
            };
            
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                _profiler.ClearData();
                int count = 0;
                if (textBox1.Text == "" )
                {
                    count = 20;
                }
                else
                {
                    try
                    {
                        count = int.Parse(textBox1.Text);
                    }catch {
                        MessageBox.Show("Not correct number. Set Default to 20", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        count = 20;
                    }
                    
                }
                for (int i = 0; i <= 20; i++)
                {
                    _profiler.StartNewSequence();

                    byte[,] luminance = LoadYChannel(openFileDialog.FileName);

                    double[,] blurred = ApplyGaussianFilterDCTVParallel(luminance);

                    byte[,] binaryImage = ApplyThresholdParallel(luminance, OtsuThreshold(luminance));

                    List<PointF> centerLine = FindCenterLine(binaryImage);

                    var bezierPoints = FitBezierCurve(centerLine);

                    pictureBox1.Image = RenderImageFast(blurred, binaryImage, bezierPoints);

                    _profiler.StopNewSequence();

                }
                _profiler.CreateFileWithChart();
                _profiler.ClearData();
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

       
        
        private double[] ApplyDCTV(double[] data, double[] kernel)
        {
            int N = data.Length;
            int K = kernel.Length;
            double[] result = new double[N];
            double[] dctKernel = new double[K];

           
            for (int k = 0; k < K; k++)
            {
                double sum = 0.0;
                for (int n = 0; n < K; n++)
                {
                    sum += kernel[n] * Math.Cos(Math.PI * k * (2 * n + 1) / (2 * K));
                }
                dctKernel[k] = sum;
            }

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

       
        private List<PointF> FitBezierCurve(List<PointF> points)
        {

            if (points.Count < 4)
            {
                throw new ArgumentException("At least 4 points are required to fit a cubic Bezier curve.");
            }

            var vectorPoints = points.Select(p => Vector<double>.Build.DenseOfArray(new double[] { p.X, p.Y })).ToArray();
            var fittedPoints = FitCubicBezier(vectorPoints);

            return fittedPoints.Select(v => new PointF((float)v[0], (float)v[1])).ToList();
        }

        private Vector<double>[] FitCubicBezier(Vector<double>[] points)
        {
            int n = points.Length;

            if (n < 4)
            {
                throw new ArgumentException("At least 4 points are required to fit a cubic Bezier curve.");
            }

            Vector<double> P0 = points[0];
            Vector<double> P3 = points[n - 1];

            // Parameter t values for each point (chord length parameterization)
            double[] t = new double[n];

            t[0] = 0;

            for (int i = 1; i < n; i++)
            {
                t[i] = t[i - 1] + (points[i] - points[i - 1]).L2Norm();
            }
            for (int i = 1; i < n; i++)
            {
                t[i] /= t[n - 1];
            }

            // Create matrix A and vector B for least squares solution
            var A = Matrix<double>.Build.Dense(n, 2);
            var Bx = Vector<double>.Build.Dense(n);
            var By = Vector<double>.Build.Dense(n);

            for (int i = 0; i < n; i++)
            {
                double u = 1 - t[i];
                double tt = t[i] * t[i];
                double uu = u * u;
                double ttt = tt * t[i];
                double uuu = uu * u;

                A[i, 0] = 3 * uu * t[i]; 
                A[i, 1] = 3 * u * tt;   

                Bx[i] = points[i][0] - (uuu * P0[0] + ttt * P3[0]);
                By[i] = points[i][1] - (uuu * P0[1] + ttt * P3[1]);
            }

            var P1P2X = A.Solve(Bx);
            var P1P2Y = A.Solve(By);

            return new[]
            {
                P0,
                Vector<double>.Build.DenseOfArray(new[] { P1P2X[0], P1P2Y[0] }),
                Vector<double>.Build.DenseOfArray(new[] { P1P2X[1], P1P2Y[1] }),
                P3,
            };
        }
        private Bitmap RenderImageFast(double[,] blurred, byte[,] binaryImage, List<PointF> curvePoints)
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

            List<PointF> linePoints = FindCenterLine(binaryImage);

            using (Graphics g = Graphics.FromImage(bmp))
            {
              
                if (curvePoints.Count >= 4)
                {
                    for (int i = 0; i < curvePoints.Count - 3; i += 3)
                    {
                        g.DrawBezier(Pens.Red, curvePoints[i], curvePoints[i + 1], curvePoints[i + 2], curvePoints[i + 3]);
                    }
                }
                else if (curvePoints.Count == 2)
                {
                    g.DrawLine(Pens.Red, curvePoints[0], curvePoints[1]);
                }
            }

            return bmp;
        }

        
    }
}

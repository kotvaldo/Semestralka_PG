using System;
using System.Collections.Generic;
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
        private Profiler _profiler = new Profiler();

        public Form1()
        {
            InitializeComponent();
        }


        private void  BtnLoadImageClick(object sender, EventArgs e)
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

                byte[,] blurred = ApplyGaussianFilter(luminance);
            
               
                byte[,] binaryImage = ApplyThresholdParallel(luminance, OtsuThreshold(luminance));

                byte[,] edges = SobelEdgeDetectionParallel(blurred);
                edges = ApplyThresholdParallel(edges, 9);


                List<PointF> centerLine = FindCenterLine(binaryImage, 0.1);


                BezierCurve bezierCurve = new BezierCurve();
                bezierCurve.SetControlPoints(centerLine);

                pictureBox1.Image = RenderImageFast(binaryImage, edges, bezierCurve);

                _profiler.StopNewSequence();
                MessageBox.Show($"Processing completed in {_profiler.Data.First()} ms", "Processing Time");

                _profiler.ClearData();

            }
        }

        /*private void BtnLoadImageWithCyclic(object sender, EventArgs e)
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
                if (textBox1.Text == "")
                {
                    count = 20;
                }
                else
                {
                    try
                    {
                        count = int.Parse(textBox1.Text);
                    }
                    catch
                    {
                        MessageBox.Show("Not correct number. Set Default to 20", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        count = 20;
                    }

                }

                for (int i = 0; i <= count; i++)
                {
                   _profiler.StartNewSequence();
                    byte[,] luminance = LoadYChannel(openFileDialog.FileName);

                    byte[,] blurred = ApplyGaussianFilter(luminance);

                    byte[,] edges = SobelEdgeDetectionParallel(blurred);
                    edges = ApplyThresholdParallel(edges, 9);

                    byte[,] binaryImage = ApplyThresholdParallel(luminance, OtsuThreshold(luminance));

                    List<PointF> centerLine = FindCenterLine(binaryImage, 0.2);


                    BezierCurve bezierCurve = new BezierCurve();
                    bezierCurve.SetControlPoints(centerLine);

                    pictureBox1.Image = RenderImageFast(binaryImage, edges, bezierCurve);
                    _profiler.StopNewSequence();

                }
                _profiler.CreateFileWithChart();
                _profiler.ClearData();
            }
        }*/

        private async Task ProcessImageAsync(string filePath)
        {
            // Načítanie luminancie
            byte[,] luminance = await Task.Run(() => LoadYChannel(filePath));

            // Aplikácia Gaussovho filtra
            byte[,] blurred = await Task.Run(() => ApplyGaussianFilter(luminance));

            // Detekcia hrán (Sobel) a prahovanie hrán
            byte[,] edges = await Task.Run(() => SobelEdgeDetectionParallel(blurred));
            edges = await Task.Run(() => ApplyThresholdParallel(edges, 10));

            // Prahovanie na binárny obraz
            byte[,] binaryImage = await Task.Run(() => ApplyThresholdParallel(luminance, OtsuThreshold(luminance)));

            // Výpočet stredovej línie
            List<PointF> centerLine = await Task.Run(() => FindCenterLine(binaryImage, 0.2));

            // Vytvorenie Bézierovej krivky
            BezierCurve bezierCurve = await Task.Run(() =>
            {
                BezierCurve curve = new BezierCurve();
                curve.SetControlPoints(centerLine);
                return curve;
            });

            // Aktualizácia obrázka v UI (musí prebiehať na hlavnom vlákne)
            await Task.Run(() =>
            {
                pictureBox1.Invoke(new Action(() =>
                {
                    pictureBox1.Image = RenderImageFast(binaryImage, edges, bezierCurve);
                }));
            });
        }

        private async void BtnLoadImageWithCyclic(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Text Files (*.txt)|*.txt",
                Title = "Select Text Image File"
            };

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                _profiler.ClearData();
                int count = string.IsNullOrEmpty(textBox1.Text) ? 20 : int.TryParse(textBox1.Text, out var n) ? n : 20;

                for (int i = 0; i <= count; i++)
                {
                    _profiler.StartNewSequence();
                    await ProcessImageAsync(openFileDialog.FileName); // Volanie postupného spracovania
                    _profiler.StopNewSequence();
                    GC.Collect(); // Nútené čistenie pamäte
                    GC.WaitForPendingFinalizers();
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

       
        public byte[,] ApplyGaussianFilter(byte[,] image)
        {
            double[] kernel = GenerateGaussianKernel(3);
            double kernelSum = kernel.Sum(); // Sum of kernel values for normalization
            int offset = kernel.Length / 2; // Half-size of the kernel

            // Temporary arrays for intermediate results
            byte[,] temp = new byte[ImageHeight, ImageWidth];
            byte[,] result = new byte[ImageHeight, ImageWidth];

            // Horizontal pass: Apply the kernel along rows
            Parallel.For(0, ImageHeight, y =>
            {
                for (int x = offset; x < ImageWidth - offset; x++)
                {
                    double sum = 0;
                    for (int k = -offset; k <= offset; k++)
                    {
                        sum += kernel[k + offset] * image[y, x + k];
                    }
                    temp[y, x] = (byte)(sum / kernelSum); // Normalize
                }
            });

            // Vertical pass: Apply the kernel along columns
            Parallel.For(0, ImageWidth, x =>
            {
                for (int y = offset; y < ImageHeight - offset; y++)
                {
                    double sum = 0;
                    for (int k = -offset; k <= offset; k++)
                    {
                        sum += kernel[k + offset] * temp[y + k, x];
                    }
                    result[y, x] = (byte)(sum / kernelSum); // Normalize
                }
            });

            return result;
        }


        public double[] GenerateGaussianKernel(double sigma)
        {
            int size = (int)Math.Ceiling(6 * sigma) | 1; 
            double[] kernel = new double[size];
            int offset = size / 2; 
            double sum = 0;

            for (int i = 0; i < size; i++)
            {
                double x = i - offset;
                kernel[i] = Math.Exp(-(x * x) / (2 * sigma * sigma)) / (Math.Sqrt(2 * Math.PI) * sigma);
                sum += kernel[i];
            }

            for (int i = 0; i < size; i++)
            {
                kernel[i] /= sum;
            }

            return kernel;
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

                    edges[y, x] = (byte)(magnitude >= 0 ? Math.Min(255, magnitude) : 0);
                }
            });

            return edges;
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



        public List<PointF> FindCenterLine(byte[,] binaryImage, double timeStep = 0.2)
        {
            if (timeStep <= 0 || timeStep > 1.0)
                throw new ArgumentException("Time step must be greater than 0 and less than or equal to 1.");

            var centerLine = new List<PointF>();

            // Iterate through rows with the given time step
            for (int y = 0; y < ImageHeight; y += (int)(ImageHeight * timeStep))
            {
                int sumX = 0, count = 0;

                for (int x = 0; x < ImageWidth; x++)
                {
                    if (binaryImage[y, x] == 0) // Black pixel
                    {
                        sumX += x;
                        count++;
                    }
                }

                if (count > 0)
                {
                    centerLine.Add(new PointF(sumX / (float)count, y)); // Average x-coordinate
                }
            }

            return centerLine;
        }




        private Bitmap RenderImageFast(byte[,] binaryImage, byte[,] edges, BezierCurve bezierCurve)
        {
            Bitmap bmp = new Bitmap(ImageWidth, ImageHeight);

            Rectangle rect = new Rectangle(0, 0, ImageWidth, ImageHeight);
            BitmapData bmpData = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);

            int stride = bmpData.Stride;
            IntPtr ptr = bmpData.Scan0;
            int bytes = stride * ImageHeight;
            byte[] rgbValues = new byte[bytes];

            // 1. Initialize the entire image to white
            Parallel.For(0, ImageHeight, y =>
            {
                for (int x = 0; x < ImageWidth; x++)
                {
                    int index = y * stride + x * 3;
                    rgbValues[index] = 0;     // B (White)
                    rgbValues[index + 1] = 0; // G (White)
                    rgbValues[index + 2] = 0; // R (White)
                }
            });

            // 2. Render edges in silver
           

            // 3. Render the binary image (black center)
            Parallel.For(0, ImageHeight, y =>
            {
                for (int x = 0; x < ImageWidth; x++)
                {
                    if (binaryImage[y, x] > 0) // Binary image (white areas of object)
                    {
                        int index = y * stride + x * 3;
                        rgbValues[index] = 255;     // B (Black)
                        rgbValues[index + 1] = 255; // G (Black)
                        rgbValues[index + 2] = 255; // R (Black)
                    }
                }
            });


            Parallel.For(0, ImageHeight, y =>
            {
                for (int x = 0; x < ImageWidth; x++)
                {
                    if (edges[y, x] > 0) // Threshold to include only significant edges
                    {
                        int index = y * stride + x * 3;
                        rgbValues[index] = 192;     // B (Silver)
                        rgbValues[index + 1] = 192; // G (Silver)
                        rgbValues[index + 2] = 192; // R (Silver)
                    }
                }
            });
            System.Runtime.InteropServices.Marshal.Copy(rgbValues, 0, ptr, bytes);
            bmp.UnlockBits(bmpData);

            // 4. Draw Bézier curve and control points
            using (Graphics g = Graphics.FromImage(bmp))
            {
                bezierCurve.Draw(g);
            }

            return bmp;
        }




    }
}

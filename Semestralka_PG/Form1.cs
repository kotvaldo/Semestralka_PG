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
        private List<Label> labels = new List<Label>();
        private List<String> _names = new List<String>();
        public Form1()
        {
            InitializeComponent();
            labels.Add(label1);
            labels.Add(label2);
            labels.Add(label3);
            labels.Add(label4);
            labels.Add(label5);
            labels.Add(label6);
            labels.Add(label7);
            labels.Add(label8);

            _names.Add("Luminance");
            _names.Add("Gauss");
            _names.Add("Otsu threshold");
            _names.Add("Sobel Edge");
            _names.Add("Center Line");
            _names.Add("DeCasteljau");
            _names.Add("Bitmap drawing");
            _profiler.Names = _names;
            for (int i = 0; i < labels.Count - 1; i++)
            {
                labels[i].Text = $"Algortihm {i + 1} Time:";
            }
            label8.Text = "Total time> ";
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

                Algorithm(openFileDialog);

                for (int i = 0; i < 7; i++)
                {
                    labels[i].Text = $"{_names[i]} {i} Time: {_profiler.Data[i]}ms";
                }
                labels[7].Text = $"Total Time: {_profiler.Data.Sum()}ms";

                GC.Collect();
                GC.WaitForPendingFinalizers();
                _profiler.ClearData();

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
                int count = string.IsNullOrEmpty(textBox1.Text) ? 20 : int.TryParse(textBox1.Text, out var n) ? n : 20;

                for (int i = 0; i <= count; i++)
                {
                    Algorithm(openFileDialog);

                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }

                _profiler.CreateFileWithChartGroupedByAlgorithm();
                _profiler.ClearData();

            }
        }

        private void Algorithm(OpenFileDialog openFileDialog)
        {
            _profiler.StartNewSequence();
            byte[,] yChannel = LoadYChannel(openFileDialog.FileName); // luminance
            _profiler.StopNewSequence();

            _profiler.StartNewSequence();
            byte[,] blurredYChannel = ApplyGaussianFilter(yChannel); // gauss
            _profiler.StopNewSequence();

            _profiler.StartNewSequence();
            byte[,] binaryImage = ApplyThresholdParallel(yChannel); // threshold 
            _profiler.StopNewSequence();

            _profiler.StartNewSequence();
            byte[,] edges = SobelEdgeDetectionParallel(blurredYChannel, 8); // sobel edge
            _profiler.StopNewSequence();


            _profiler.StartNewSequence();
            List<PointF> centerLine = FindCenterLine(binaryImage, 0.1); // center line
            _profiler.StopNewSequence();

            BezierCurve bezierCurve = new BezierCurve();

            _profiler.StartNewSequence();
            bezierCurve.SetControlPoints(centerLine); // bezier decasteljau
            _profiler.StopNewSequence();

            _profiler.StartNewSequence();
            pictureBox1.Image = RenderImageFast(binaryImage, edges, bezierCurve); // render 
            _profiler.StopNewSequence();
        }






        private byte[,] LoadYChannel(string filePath)
        {
            byte[] fileBytes = File.ReadAllBytes(filePath);
            byte[,] yChannel = new byte[ImageHeight, ImageWidth];

            for (int x = 0; x < ImageHeight; x++)
            {
                for (int y = 0; y < ImageWidth; y++)
                {
                    yChannel[x, y] = fileBytes[x * ImageWidth + y];
                }
            }

            return yChannel;
        }

       
        public byte[,] ApplyGaussianFilter(byte[,] image)
        {
            double[] kernel = GenerateGaussianKernel(3);
            double kernelSum = kernel.Sum(); 
            int offset = kernel.Length / 2; 

          
            byte[,] temp = new byte[ImageHeight, ImageWidth];
            byte[,] result = new byte[ImageHeight, ImageWidth];

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
            int size = (int)Math.Ceiling(6 * sigma + 1) | 1;
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


      

        private byte[,] SobelEdgeDetectionParallel(byte[,] image, int T)
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

                    int gradientSize = (int)Math.Sqrt(sumX * sumX + sumY * sumY);

                    edges[y, x] = (byte)(gradientSize >= T ? Math.Min(255, gradientSize) : 0);
                }
            });

            return edges;
        }



        private byte[,] ApplyThresholdParallel(byte[,] image)
        {
            int totalPixels = ImageWidth * ImageHeight;

            int threshold = 0;

            // Histogram a výpočet pravdepodobností
            int[] h = new int[256];
            for (int y = 0; y < ImageHeight; y++)
            {
                for (int x = 0; x < ImageWidth; x++)
                {
                    h[image[y, x]]++;
                }
            }

            double[] p = new double[256];
            for (int i = 0; i < 256; i++)
            {
                p[i] = (double)h[i] / totalPixels;
            }

            // Počiatočné premenné
            double sum1 = 0;
            for (int i = 0; i < 256; i++)
            {
                sum1 += i * p[i];
            }

            double w1 = 0, sum2 = 0, max = 0;

            for (int i = 0; i < 256; i++)
            {
                w1 += p[i];
                if (w1 == 0) continue;

                double w2 = 1 - w1;
                if (w2 == 0) break;

                sum2 += i * p[i];
                double m1 = sum2 / w1;
                double m2 = (sum1 - sum2) / w2;
                double between = w1 * w2 * (m1 - m2) * (m1 - m2);

                if (between > max)
                {
                    max = between;
                    threshold = i;
                }
            }

            // Binarizácia obrazu na základe vypočítaného prahu
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

            for (int y = 0; y < ImageHeight; y += (int)(ImageHeight * timeStep))
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
                    centerLine.Add(new PointF(sumX / (float)count, y)); //
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


           
            System.Runtime.InteropServices.Marshal.Copy(rgbValues, 0, ptr, bytes);
            bmp.UnlockBits(bmpData);

            using (Graphics g = Graphics.FromImage(bmp))
            {
                bezierCurve.Draw(g);
            }

            return bmp;
        }




    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Semestralka_PG
{
    public partial class Form1 : Form
    {
        private List<Bitmap> processedImages = new List<Bitmap>();
        private int currentImageIndex = 0;

        public Form1()
        {
            InitializeComponent();
        }

        private void BtnLoadImageClick(object sender, EventArgs e)
        {
            string projectPath = AppDomain.CurrentDomain.BaseDirectory;
            string resourcesPath = Path.Combine(projectPath, @"..\..\Resources");

            string image1Path = Path.Combine(resourcesPath, "NewImage1.txt");
            string image2Path = Path.Combine(resourcesPath, "NewImage2.txt");
            string image3Path = Path.Combine(resourcesPath, "NewImage3.txt");

            ImageProcessor processor1 = new ImageProcessor(image1Path);
            ImageProcessor processor2 = new ImageProcessor(image2Path);
            ImageProcessor processor3 = new ImageProcessor(image3Path);

            processedImages.Clear();
            processedImages.Add(processor1.ProcessImage());
            processedImages.Add(processor2.ProcessImage());
            processedImages.Add(processor3.ProcessImage());

            currentImageIndex = 0;
            UpdatePictureBox();
        }

        private void BtnNextClick(object sender, EventArgs e)
        {
            if (processedImages.Count == 0) return;

            currentImageIndex = (currentImageIndex + 1) % processedImages.Count;
            UpdatePictureBox();
        }

        private void BtnPreviousClick(object sender, EventArgs e)
        {
            if (processedImages.Count == 0) return;

            currentImageIndex = (currentImageIndex - 1 + processedImages.Count) % processedImages.Count;
            UpdatePictureBox();
        }

        private void UpdatePictureBox()
        {
            pictureBox1.Image = processedImages[currentImageIndex];
        }

        private void button4_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }
    }
}

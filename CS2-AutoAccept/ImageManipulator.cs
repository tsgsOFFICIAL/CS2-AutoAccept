using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace CS2_AutoAccept
{
    internal class ImageManipulator
    {
        /// <summary>
        /// Resize image
        /// </summary>
        /// <param name="bmp"></param>
        /// <param name="newWidth"></param>
        /// <param name="newHeight"></param>
        /// <returns>This method returns a Bitmap that has been resized & optimised</returns>
        public static Bitmap Resize(Bitmap bmp, int newWidth, int newHeight)
        {
            Bitmap temp = bmp;

            Bitmap bmap = new Bitmap(newWidth, newHeight, temp.PixelFormat);

            double nWidthFactor = (double)temp.Width / (double)newWidth;
            double nHeightFactor = (double)temp.Height / (double)newHeight;

            double fx, fy, nx, ny;
            int cx, cy, fr_x, fr_y;
            Color color1 = new Color();
            Color color2 = new Color();
            Color color3 = new Color();
            Color color4 = new Color();
            byte nRed, nGreen, nBlue;

            byte bp1, bp2;

            for (int i = 0; i < bmap.Width; ++i)
            {
                for (int j = 0; j < bmap.Height; ++j)
                {
                    fr_x = (int)Math.Floor(i * nWidthFactor);
                    fr_y = (int)Math.Floor(j * nHeightFactor);
                    cx = fr_x + 1;
                    if (cx >= temp.Width)
                        cx = fr_x;

                    cy = fr_y + 1;

                    if (cy >= temp.Height)
                        cy = fr_y;

                    fx = i * nWidthFactor - fr_x;
                    fy = j * nHeightFactor - fr_y;
                    nx = 1.0 - fx;
                    ny = 1.0 - fy;

                    color1 = temp.GetPixel(fr_x, fr_y);
                    color2 = temp.GetPixel(cx, fr_y);
                    color3 = temp.GetPixel(fr_x, cy);
                    color4 = temp.GetPixel(cx, cy);

                    // Blue
                    bp1 = (byte)(nx * color1.B + fx * color2.B);

                    bp2 = (byte)(nx * color3.B + fx * color4.B);

                    nBlue = (byte)(ny * (double)(bp1) + fy * (double)(bp2));

                    // Green
                    bp1 = (byte)(nx * color1.G + fx * color2.G);

                    bp2 = (byte)(nx * color3.G + fx * color4.G);

                    nGreen = (byte)(ny * (double)(bp1) + fy * (double)(bp2));

                    // Red
                    bp1 = (byte)(nx * color1.R + fx * color2.R);

                    bp2 = (byte)(nx * color3.R + fx * color4.R);

                    nRed = (byte)(ny * (double)(bp1) + fy * (double)(bp2));

                    bmap.SetPixel(i, j, Color.FromArgb(255, nRed, nGreen, nBlue));
                }
            }

            //bmap = SetGrayscale(bmap);
            bmap = RemoveNoise(bmap);

            return bmap;

        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="img"></param>
        /// <returns></returns>
        public static Bitmap SetGrayscale(Bitmap img)
        {
            Bitmap temp = (Bitmap)img;
            Bitmap bmap = (Bitmap)temp.Clone();
            Color c;

            for (int i = 0; i < bmap.Width; i++)
            {
                for (int j = 0; j < bmap.Height; j++)
                {
                    c = bmap.GetPixel(i, j);
                    byte gray = (byte)(.299 * c.R + .587 * c.G + .114 * c.B);

                    bmap.SetPixel(i, j, Color.FromArgb(gray, gray, gray));
                }
            }

            return (Bitmap)bmap.Clone();

        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="bmap"></param>
        /// <returns></returns>
        public static Bitmap RemoveNoise(Bitmap bmap)
        {
            for (int i = 0; i < bmap.Width; i++)
            {
                for (int j = 0; j < bmap.Height; j++)
                {
                    Color pixel = bmap.GetPixel(i, j);
                    if (pixel.R < 162 && pixel.G < 162 && pixel.B < 162)
                        bmap.SetPixel(i, j, Color.Black);
                    else if (pixel.R > 162 && pixel.G > 162 && pixel.B > 162)
                        bmap.SetPixel(i, j, Color.White);
                }
            }

            return bmap;
        }
    }
}
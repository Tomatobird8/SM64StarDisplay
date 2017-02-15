﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StarDisplay
{
    public class GraphicsManager
    {
        Image darkStar;
        Image goldStar;

        Bitmap goldSquare;
        Bitmap blackSquare;

        LayoutDescription ld;

        Graphics _graphics;
        public Graphics graphics {
            internal get { return _graphics; }
            set { _graphics.Dispose(); _graphics = value; }
        }

        Font DrawFont;
        Font BigFont;

        public GraphicsManager(Graphics graphics, LayoutDescription ld)
        {
            darkStar = ld.darkStar;
            goldStar = ld.goldStar;
            this.ld = ld;

            if (darkStar.Width != 20 || goldStar.Height != 20)
                Compress();

            _graphics = graphics;

            PrivateFontCollection collection = new PrivateFontCollection();
            collection.AddFontFile("font/CourierNew.ttf");
            FontFamily fontFamily = new FontFamily("Courier New", collection);
            
            DrawFont = new Font(fontFamily, 10);
            BigFont = new Font(fontFamily, 15);

            goldSquare = new Bitmap(4, 4);
            for (int i = 0; i < goldSquare.Width; i++)
                for (int j = 0; j < goldSquare.Height; j++)
                    goldSquare.SetPixel(i, j, Color.Gold);

            blackSquare = new Bitmap(4, 4);
            for (int i = 0; i < blackSquare.Width; i++)
                for (int j = 0; j < blackSquare.Height; j++)
                    blackSquare.SetPixel(i, j, Color.Black);
        }

        public void PaintHUD(int width, int height)
        {
            SolidBrush blackBrush = new SolidBrush(Color.Black);
            SolidBrush drawBrush = new SolidBrush(Color.LightGray);

            graphics.FillRectangle(blackBrush, new Rectangle(0, 0, width, height));
            for (int line = 0; line < ld.courseDescription.Length; line++)
            {
                if (ld.courseDescription[line] == null) continue;
                DrawLine(ld.courseDescription[line], line, false);
            }
            for (int line = 0; line < ld.secretDescription.Length; line++)
            {
                if (ld.secretDescription[line] == null) continue;
                DrawLine(ld.secretDescription[line], line, true);
            }
            int lastLine = Math.Max(ld.courseDescription.Length, ld.secretDescription.Length);
            graphics.DrawString("Savestateless Stars", BigFont, drawBrush, 45, lastLine * 23 + 2);

            blackBrush.Dispose();
            drawBrush.Dispose();
        }

        public void DrawByte(byte stars, int lineNumber, bool isSecret, byte mask)
        {
            for (int i = 1; i <= 7; i++)
            {
                if ((mask & (1 << i)) == 0) continue;
                int x = (isSecret ? 180 : 0) + i * 20;
                int y = lineNumber * 23;
                bool isAcquired = (stars & (1 << (i - 1))) != 0;
                Image img = isAcquired ? goldStar : darkStar;
                graphics.DrawImage(img, x, y, 20, 20);
            }
        }

        public void DrawLine(LineDescription ld, int lineNumber, bool isSecret)
        {
            SolidBrush drawBrush = new SolidBrush(Color.LightGray);
            if (ld.isTextOnly)
            {
                graphics.DrawString(ld.text, DrawFont, drawBrush, (isSecret ? 180: 0) + 7, lineNumber * 23 + 2);
            }
            else
            {
                graphics.DrawString(ld.text, DrawFont, drawBrush, isSecret ? 180 : 0, lineNumber * 23 + 2);
                DrawByte(0, lineNumber, isSecret, ld.starMask);
            }
            drawBrush.Dispose();
        }

        public void AddLineHighlight(LineEntry le, LineDescription lind)
        {
            if (lind.text != "")
            {
                int x = (le.Secret ? 180 : 0) + 1;
                int y = le.Line * 23 + 2;

                SolidBrush yellowBrush = new SolidBrush(Color.DarkGoldenrod);
                Pen yellowPen = new Pen(yellowBrush);
                graphics.DrawRectangle(yellowPen, new Rectangle(x, y, 17, 17));
                yellowPen.Dispose();
                yellowBrush.Dispose();
            }
            else
            {
                int x = (le.Secret ? 180 : 0);
                int y = le.Line * 23;

                graphics.DrawImage(goldSquare, x + 8, y + 8);
            }
        }

        public void RemoveLineHighlight(LineEntry le, LineDescription lind)
        {
            if (lind.text != "")
            {
                int x = (le.Secret ? 180 : 0) + 1;
                int y = le.Line * 23 + 2;

                SolidBrush blackBrush = new SolidBrush(Color.Black);
                Pen blackPen = new Pen(blackBrush);
                graphics.DrawRectangle(blackPen, new Rectangle(x, y, 17, 17));
                blackPen.Dispose();
                blackBrush.Dispose();
            }
            else
            {
                int x = (le.Secret ? 180 : 0);
                int y = le.Line * 23;

                graphics.DrawImage(blackSquare, x + 8, y + 8);
            }
        }

        public void DrawGreenString(LineEntry le, LineDescription lind)
        {
            int x = le.Secret ? 180 : 0;
            int y = le.Line * 23;

            graphics.DrawImage(blackSquare, x + 1, y + 1);
            SolidBrush drawBrush = new SolidBrush(Color.LightGreen);

            PrivateFontCollection collection = new PrivateFontCollection();
            collection.AddFontFile("font/CourierNew.ttf");
            FontFamily fontFamily = new FontFamily("Courier New", collection);

            Font drawFont = new Font(fontFamily, 10);

            graphics.DrawString(lind.text, drawFont, drawBrush, x, y + 2);

            drawBrush.Dispose();
        }

        public void DrawGrayString(LineEntry le, LineDescription lind)
        {
            int x = le.Secret ? 180 : 0;
            int y = le.Line * 23;

            graphics.DrawImage(blackSquare, x + 1, y + 1);
            SolidBrush drawBrush = new SolidBrush(Color.LightGray);

            PrivateFontCollection collection = new PrivateFontCollection();
            collection.AddFontFile("font/CourierNew.ttf");
            FontFamily fontFamily = new FontFamily("Courier New", collection);

            Font drawFont = new Font(fontFamily, 10);

            graphics.DrawString(lind.text, drawFont, drawBrush, x, y + 2);

            drawBrush.Dispose();
        }

        public void DrawStarNumber(string totalCount, int starCount)
        {
            string starLine = starCount.ToString().PadLeft(3) + "/" + totalCount.PadRight(3);
            
            int totalStarLine = Math.Max(ld.courseDescription.Length, ld.secretDescription.Length) + 1;

            SolidBrush blackBrush = new SolidBrush(Color.Black);
            SolidBrush drawBrush = new SolidBrush(Color.LightGray);

            PrivateFontCollection collection = new PrivateFontCollection();
            collection.AddFontFile("font/CourierNew.ttf");
            FontFamily fontFamily = new FontFamily("Courier New", collection);
            Font bigFont = new Font(fontFamily, 15);

            graphics.FillRectangle(blackBrush, new Rectangle(15, totalStarLine * 23 + 2, 200, 20));
            graphics.DrawString(starLine, bigFont, drawBrush, 120, totalStarLine * 23 + 2);

            blackBrush.Dispose();
            drawBrush.Dispose();
        }

        public void Compress()
        {
            var goldCompressedImage = new Bitmap(20, 20);
            var darkCompressedImage = new Bitmap(20, 20);

            var destRect = new Rectangle(0, 0, 20, 20);

            using (var graphics = Graphics.FromImage(goldCompressedImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (var wrapMode = new ImageAttributes())
                {
                    wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.DrawImage(goldStar, destRect, 0, 0, goldStar.Width, goldStar.Height, GraphicsUnit.Pixel, wrapMode);
                }
            }
            goldStar.Dispose();
            ld.goldStar = goldCompressedImage;
            goldStar = goldCompressedImage;

            using (var graphics = Graphics.FromImage(darkCompressedImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (var wrapMode = new ImageAttributes())
                {
                    wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.DrawImage(darkStar, destRect, 0, 0, darkStar.Width, darkStar.Height, GraphicsUnit.Pixel, wrapMode);
                }
            }
            darkStar.Dispose();
            ld.darkStar = darkCompressedImage;
            darkStar = darkCompressedImage;
        }
    }
}

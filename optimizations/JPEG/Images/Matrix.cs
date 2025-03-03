using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace JPEG.Images;

#pragma warning disable CA1416
internal class Matrix(int height, int width)
{
	public readonly int Width = width;
	public readonly int Height = height;
	public readonly Pixel[] Pixels = new Pixel[height * width];

	public static unsafe explicit operator Matrix(Bitmap bmp)
	{
		var width = bmp.Width - bmp.Width % 8;
		var height = bmp.Height - bmp.Height % 8;
		var matrix = new Matrix(height, width);
		var bd = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, bmp.PixelFormat);

		try
		{
			fixed (Pixel* pPixels = &matrix.Pixels[0])
			{
				for (var h = 0; h < height; h++)
				{
					var pBmpPixel = (byte*)bd.Scan0 + h * bd.Stride;
					for (var w = 0; w < width; w++)
					{
						var blue = *pBmpPixel++;
						var green = *pBmpPixel++;
						var red = *pBmpPixel++;
						
						*(pPixels + h * width + w) = new Pixel(red, green, blue);
					}
				}
				return matrix;
			}
		}
		finally { bmp.UnlockBits(bd); }
	}

	public static unsafe explicit operator Bitmap(Matrix matrix)
	{
		var width = matrix.Width;
		var height = matrix.Height;
		
		var bmp = new Bitmap(matrix.Width, matrix.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
		var bd = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, bmp.PixelFormat);

		try
		{
			fixed (Pixel* pPixels = &matrix.Pixels[0])
			{
				for (var h = 0; h < height; h++)
				{
					var pBmpPixel = (byte*)bd.Scan0 + h * bd.Stride;
					for (var w = 0; w < width; w++)
					{
						var pixel = *(pPixels + h * width + w);
						*pBmpPixel = ToByte(pixel.B); pBmpPixel++;
						*pBmpPixel = ToByte(pixel.G); pBmpPixel++;
						*pBmpPixel = ToByte(pixel.R); pBmpPixel++;
					}
				}
				return bmp;
			}
		}
		finally { bmp.UnlockBits(bd); }
	}

	public void GetSubMatrix(int yOff, int xOff, int subWidth, int subHeight, Func<Pixel, double> f, Span<double> output)
	{
		for (var j = 0; j < subHeight; j++)
		for (var i = 0; i < subWidth; i++)
			output[j * subWidth + i] = f(Pixels[(yOff + j) * Width + xOff + i]);
	}

	private static byte ToByte(double d) => d switch
	{ 
		> byte.MaxValue => byte.MaxValue, 
		< byte.MinValue => byte.MinValue, 
		_ => (byte)d
	};
}
#pragma warning restore CA1416
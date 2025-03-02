using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;

namespace JPEG.Images;

#pragma warning disable CA1416
internal class Matrix(int height, int width)
{
	public readonly int Width = width;
	public readonly int Height = height;
	public readonly Pixel[,] Pixels = new Pixel[height, width];

	public static unsafe explicit operator Matrix(Bitmap bmp)
	{
		var width = bmp.Width - bmp.Width % 8;
		var height = bmp.Height - bmp.Height % 8;
		var matrix = new Matrix(height, width);
		var bd = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, bmp.PixelFormat);

		try
		{
			for (var h = 0; h < height; h++)
			{
				var pPixel = (byte*)bd.Scan0 + h * bd.Stride;
				for (var w = 0; w < width; w++)
				{
					var blue = *pPixel++;
					var green = *pPixel++;
					var red = *pPixel++;
					matrix.Pixels[h, w] = new Pixel(red, green, blue, PixelFormat.RGB);
				}
			}
			return matrix;
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
			for (var h = 0; h < height; h++)
			{
				var pPixel = (byte*)bd.Scan0 + h * bd.Stride;
				for (var w = 0; w < width; w++)
				{
					var pixel = matrix.Pixels[h, w];
					*pPixel = ToByte(pixel.B); pPixel++;
					*pPixel = ToByte(pixel.G); pPixel++;
					*pPixel = ToByte(pixel.R); pPixel++;
				}
			}
			return bmp;
		}
		finally { bmp.UnlockBits(bd); }
	}

	private static byte ToByte(double d) => d switch
	{ 
		> byte.MaxValue => byte.MaxValue, 
		< byte.MinValue => byte.MinValue, 
		_ => (byte)d
	};
}
#pragma warning restore CA1416
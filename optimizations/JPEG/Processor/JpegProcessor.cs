using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using JPEG.Images;
using PixelFormat = JPEG.Images.PixelFormat;

namespace JPEG.Processor;

#pragma warning disable CA1416
public class JpegProcessor : IJpegProcessor
{
	public static readonly JpegProcessor Init = new();
	public const int CompressionQuality = 70;
	private const int DCTSize = 8;
	private readonly DCT Dct = new(DCTSize);

	private static void PrintSpan<T>(Span<T> span)
	{
		foreach (var item in span)
			Console.WriteLine(item);
	}

	public void Compress(string imagePath, string compressedImagePath)
	{
		using var fileStream = File.OpenRead(imagePath);
		using var bmp = (Bitmap)Image.FromStream(fileStream, false, false);
		var imageMatrix = (Matrix)bmp;
		//Console.WriteLine($"{bmp.Width}x{bmp.Height} - {fileStream.Length / (1024.0 * 1024):F2} MB");
		var compressionResult = Compress(imageMatrix, CompressionQuality);
		compressionResult.Save(compressedImagePath);
	}

	public void Uncompress(string compressedImagePath, string uncompressedImagePath)
	{
		var compressedImage = CompressedImage.Load(compressedImagePath);
		var uncompressedImage = Uncompress(compressedImage);
		var resultBmp = (Bitmap)uncompressedImage;
		resultBmp.Save(uncompressedImagePath, ImageFormat.Bmp);
	}

	private CompressedImage Compress(Matrix matrix, int quality = 50)
	{
		var index = 0;
		Span<byte> allQuantizedBytes = new byte[matrix.Pixels.Length * 3];
		
		Span<double> subMatrix = new double[DCTSize * DCTSize];
		Span<byte> quantizedFreqs = new byte[DCTSize * DCTSize];
		Span<double> channelFreqs = new double[DCTSize * DCTSize];
		Span<int> quantizationMatrix = GetQuantizationMatrix(quality);
		
		var selectors = new Func<Pixel, double>[] { p => p.Y - 128, p => p.Cb - 128, p => p.Cr - 128 };

		for (var y = 0; y < matrix.Height; y += DCTSize)
		for (var x = 0; x < matrix.Width; x += DCTSize) 
			foreach (var selector in selectors)
			{
				matrix.GetSubMatrix(y, x, DCTSize, DCTSize, selector, subMatrix);
				Dct.DCT2D(subMatrix, channelFreqs);
				Quantize(channelFreqs, quantizationMatrix, quantizedFreqs);
				ZigZagScan(quantizedFreqs, allQuantizedBytes.Slice(index, DCTSize * DCTSize));
				index += DCTSize * DCTSize;
			}
		
		var compressedBytes = HuffmanCodec.Encode(allQuantizedBytes, out var decodeTable, out var bitsCount);

		return new CompressedImage
		{
			Quality = quality, CompressedBytes = compressedBytes, BitsCount = bitsCount, DecodeTable = decodeTable,
			Height = matrix.Height, Width = matrix.Width
		};
	}

	private Matrix Uncompress(CompressedImage image)
	{
		var index = 0;
		var result = new Matrix(image.Height, image.Width);
		
		Span<double> _y = new double[DCTSize * DCTSize];
		Span<double> cb = new double[DCTSize * DCTSize];
		Span<double> cr = new double[DCTSize * DCTSize];
		
		Span<byte> quantizedFreqs = new byte[DCTSize * DCTSize];
		Span<double> channelFreqs = new double[DCTSize * DCTSize];
		Span<int> quantizationMatrix = GetQuantizationMatrix(image.Quality);
		
		var allQuantizedBytes = HuffmanCodec.Decode(image.CompressedBytes, image.DecodeTable, image.BitsCount).AsSpan();
		
		for (var y = 0; y < image.Height; y += DCTSize)
		for (var x = 0; x < image.Width; x += DCTSize)
		{
			ProcessChannel(
				allQuantizedBytes.Slice(index, DCTSize * DCTSize), 
				quantizedFreqs, channelFreqs, quantizationMatrix, _y);
			index += DCTSize * DCTSize;
				
			ProcessChannel(
				allQuantizedBytes.Slice(index, DCTSize * DCTSize), 
				quantizedFreqs, channelFreqs, quantizationMatrix, cb);
			index += DCTSize * DCTSize;
				
			ProcessChannel(
				allQuantizedBytes.Slice(index, DCTSize * DCTSize), 
				quantizedFreqs, channelFreqs, quantizationMatrix, cr);
			index += DCTSize * DCTSize;
			SetPixels(result, _y, cb, cr, y, x);
		}

		return result;
	}

	private void ProcessChannel(
		Span<byte> quantizedBytes, Span<byte> quantizedFreqs, 
		Span<double> channelFreqs, Span<int> quantizationMatrix, Span<double> channel)
	{
		ZigZagUnScan(quantizedBytes, quantizedFreqs);
		DeQuantize(quantizedFreqs, quantizationMatrix, channelFreqs);
		Dct.IDCT2D(channelFreqs, channel);
		for (var i = 0; i < DCTSize * DCTSize; i++) channel[i] += 128;
	}

	private static void SetPixels(Matrix matrix, Span<double> a, Span<double> b, Span<double> c, int yOff, int xOff)
	{
		for (var y = 0; y < DCTSize; y++)
		for (var x = 0; x < DCTSize; x++)
		{
			var pixel = new Pixel(a[y * DCTSize + x], b[y * DCTSize + x], c[y * DCTSize + x]);
			matrix.Pixels[(yOff + y) * matrix.Width + xOff + x] = pixel;
		}
	}

	private static void ZigZagScan(in Span<byte> channelFreqs, Span<byte> output)
	{
		output[0] = channelFreqs[0 * DCTSize + 0];
		output[1] = channelFreqs[0 * DCTSize + 1];
		output[2] = channelFreqs[1 * DCTSize + 0];
		output[3] = channelFreqs[2 * DCTSize + 0];
		output[4] = channelFreqs[1 * DCTSize + 1];
		output[5] = channelFreqs[0 * DCTSize + 2];
		output[6] = channelFreqs[0 * DCTSize + 3];
		output[7] = channelFreqs[1 * DCTSize + 2];
		
		output[8] = channelFreqs[2 * DCTSize + 1];
		output[9] = channelFreqs[3 * DCTSize + 0];
		output[10] = channelFreqs[4 * DCTSize + 0];
		output[11] = channelFreqs[3 * DCTSize + 1];
		output[12] = channelFreqs[2 * DCTSize + 2];
		output[13] = channelFreqs[1 * DCTSize + 3];
		output[14] = channelFreqs[0 * DCTSize + 4];
		output[15] = channelFreqs[0 * DCTSize + 5];
		
		output[16] = channelFreqs[1 * DCTSize + 4];
		output[17] = channelFreqs[2 * DCTSize + 3];
		output[18] = channelFreqs[3 * DCTSize + 2];
		output[19] = channelFreqs[4 * DCTSize + 1];
		output[20] = channelFreqs[5 * DCTSize + 0];
		output[21] = channelFreqs[6 * DCTSize + 0];
		output[22] = channelFreqs[5 * DCTSize + 1];
		output[23] = channelFreqs[4 * DCTSize + 2];
		
		output[24] = channelFreqs[3 * DCTSize + 3];
		output[25] = channelFreqs[2 * DCTSize + 4];
		output[26] = channelFreqs[1 * DCTSize + 5];
		output[27] = channelFreqs[0 * DCTSize + 6];
		output[28] = channelFreqs[0 * DCTSize + 7];
		output[29] = channelFreqs[1 * DCTSize + 6];
		output[30] = channelFreqs[2 * DCTSize + 5];
		output[31] = channelFreqs[3 * DCTSize + 4];
		
		output[32] = channelFreqs[4 * DCTSize + 3];
		output[33] = channelFreqs[5 * DCTSize + 2];
		output[34] = channelFreqs[6 * DCTSize + 1];
		output[35] = channelFreqs[7 * DCTSize + 0];
		output[36] = channelFreqs[7 * DCTSize + 1];
		output[37] = channelFreqs[6 * DCTSize + 2];
		output[38] = channelFreqs[5 * DCTSize + 3];
		output[39] = channelFreqs[4 * DCTSize + 4];
		
		output[40] = channelFreqs[3 * DCTSize + 5];
		output[41] = channelFreqs[2 * DCTSize + 6];
		output[42] = channelFreqs[1 * DCTSize + 7];
		output[43] = channelFreqs[2 * DCTSize + 7];
		output[44] = channelFreqs[3 * DCTSize + 6];
		output[45] = channelFreqs[4 * DCTSize + 5];
		output[46] = channelFreqs[5 * DCTSize + 4];
		output[47] = channelFreqs[6 * DCTSize + 3];
		
		output[48] = channelFreqs[7 * DCTSize + 2];
		output[49] = channelFreqs[7 * DCTSize + 3];
		output[50] = channelFreqs[6 * DCTSize + 4];
		output[51] = channelFreqs[5 * DCTSize + 5];
		output[52] = channelFreqs[4 * DCTSize + 6];
		output[53] = channelFreqs[3 * DCTSize + 7];
		output[54] = channelFreqs[4 * DCTSize + 7];
		output[55] = channelFreqs[5 * DCTSize + 6];
		
		output[56] = channelFreqs[6 * DCTSize + 5];
		output[57] = channelFreqs[7 * DCTSize + 4];
		output[58] = channelFreqs[7 * DCTSize + 5];
		output[59] = channelFreqs[6 * DCTSize + 6];
		output[60] = channelFreqs[5 * DCTSize + 7];
		output[61] = channelFreqs[6 * DCTSize + 7];
		output[62] = channelFreqs[7 * DCTSize + 6];
		output[63] = channelFreqs[7 * DCTSize + 7];
	}

	private static void ZigZagUnScan(in Span<byte> quantizedBytes, Span<byte> output)
	{
		output[0] = quantizedBytes[0];
		output[1] = quantizedBytes[1];
		output[2] = quantizedBytes[5];
		output[3] = quantizedBytes[6];
		output[4] = quantizedBytes[14];
		output[5] = quantizedBytes[15];
		output[6] = quantizedBytes[27];
		output[7] = quantizedBytes[28];
		
		output[8] = quantizedBytes[2];
		output[9] = quantizedBytes[4];
		output[10] = quantizedBytes[7];
		output[11] = quantizedBytes[13];
		output[12] = quantizedBytes[16];
		output[13] = quantizedBytes[26];
		output[14] = quantizedBytes[29];
		output[15] = quantizedBytes[42];
		
		output[16] = quantizedBytes[3];
		output[17] = quantizedBytes[8];
		output[18] = quantizedBytes[12];
		output[19] = quantizedBytes[17];
		output[20] = quantizedBytes[25];
		output[21] = quantizedBytes[30];
		output[22] = quantizedBytes[41];
		output[23] = quantizedBytes[43];
		
		output[24] = quantizedBytes[9];
		output[25] = quantizedBytes[11];
		output[26] = quantizedBytes[18];
		output[27] = quantizedBytes[24];
		output[28] = quantizedBytes[31];
		output[29] = quantizedBytes[40];
		output[30] = quantizedBytes[44];
		output[31] = quantizedBytes[53];
		
		output[32] = quantizedBytes[10];
		output[33] = quantizedBytes[19];
		output[34] = quantizedBytes[23];
		output[35] = quantizedBytes[32];
		output[36] = quantizedBytes[39];
		output[37] = quantizedBytes[45];
		output[38] = quantizedBytes[52];
		output[39] = quantizedBytes[54];
		
		output[40] = quantizedBytes[20];
		output[41] = quantizedBytes[22];
		output[42] = quantizedBytes[33];
		output[43] = quantizedBytes[38];
		output[44] = quantizedBytes[46];
		output[45] = quantizedBytes[51];
		output[46] = quantizedBytes[55];
		output[47] = quantizedBytes[60];
		
		output[48] = quantizedBytes[21];
		output[49] = quantizedBytes[34];
		output[50] = quantizedBytes[37];
		output[51] = quantizedBytes[47];
		output[52] = quantizedBytes[50];
		output[53] = quantizedBytes[56];
		output[54] = quantizedBytes[59];
		output[55] = quantizedBytes[61];
		
		output[56] = quantizedBytes[35];
		output[57] = quantizedBytes[36];
		output[58] = quantizedBytes[48];
		output[59] = quantizedBytes[49];
		output[60] = quantizedBytes[57];
		output[61] = quantizedBytes[58];
		output[62] = quantizedBytes[62];
		output[63] = quantizedBytes[63];
	}

	private static void Quantize(in Span<double> channelFreqs, in Span<int> quantizationMatrix, Span<byte> output)
	{
		for (var i = 0; i < DCTSize * DCTSize; i++)
			output[i] = (byte)(channelFreqs[i] / quantizationMatrix[i]);
	}

	private static void DeQuantize(in Span<byte> quantizedBytes, in Span<int> quantizationMatrix, Span<double> output)
	{
		for (var i = 0; i < DCTSize * DCTSize; i++)
			output[i] = (sbyte)quantizedBytes[i] * quantizationMatrix[i];
	}

	private static int[] GetQuantizationMatrix(int quality)
	{
		if (quality is < 1 or > 99)
			throw new ArgumentException("quality must be in [1,99] interval");
		
		var result = new[]
		{
			16, 11, 10, 16, 24, 40, 51, 61,
			12, 12, 14, 19, 26, 58, 60, 55 ,
			14, 13, 16, 24, 40, 57, 69, 56 ,
			14, 17, 22, 29, 51, 87, 80, 62 ,
			18, 22, 37, 56, 68, 109, 103, 77,
			24, 35, 55, 64, 81, 104, 113, 92,
			49, 64, 78, 87, 103, 121, 120, 101,
			72, 92, 95, 98, 112, 100, 103, 99
		};
		var multiplier = quality < 50 ? 5000 / quality : 200 - 2 * quality;

		for (var y = 0; y < DCTSize; y++)
		for (var x = 0; x < DCTSize; x++)
		{
			var i = y * DCTSize + x;
			result[i] = (multiplier * result[i] + 50) / 100;
		}
		return result;
	}
}
#pragma warning restore CA1416
using System;
using System.Runtime.CompilerServices;

namespace JPEG;

public class DCT
{
	private readonly int dctSize;
	private readonly double[] buffer;
	private readonly double[] biasesMatrix;
	private readonly double[] biasesMatrixT;
	
	public DCT(int dctSize)
	{
		this.dctSize = dctSize;
		buffer = new double[dctSize * dctSize]; 
		biasesMatrix = new double[dctSize * dctSize];
		biasesMatrixT = new double[dctSize * dctSize];
		
		for (var k = 0; k < dctSize; k++)
		for (var n = 0; n < dctSize; n++)
		{
			var ck = k == 0 ? Math.Sqrt(dctSize) : Math.Sqrt(dctSize / 2d);
			biasesMatrix[k * dctSize + n] = 1 / ck * Math.Cos(Math.PI / dctSize * (n + 0.5) * k);
			biasesMatrixT[n * dctSize + k] = biasesMatrix[k * dctSize + n];
		}
	}
	
	public void DCT2D(in Span<double> input, Span<double> output)
	{
		if (input.Length != dctSize * dctSize)
			throw new ArgumentException("Input array length is not equal to dct size");

		var bufferSpan = buffer.AsSpan();
		MultiplyMatrix(biasesMatrix.AsSpan(), input, bufferSpan);
		MultiplyMatrix(bufferSpan, biasesMatrixT.AsSpan(), output);
	}
	
	public void IDCT2D(in Span<double> input, Span<double> output)
	{
		if (input.Length != dctSize * dctSize)
			throw new ArgumentException("Input array length is not equal to dct size");
		
		var bufferSpan = buffer.AsSpan();
		MultiplyMatrix(biasesMatrixT.AsSpan(), input, bufferSpan);
		MultiplyMatrix(bufferSpan, biasesMatrix.AsSpan(), output);
	}
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void MultiplyMatrix(Span<double> m1, Span<double> m2, Span<double> result) 
		=> MultiplyMatrix(m1, dctSize, dctSize, m2, dctSize, dctSize, result);
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void MultiplyMatrix(
		Span<double> m1, int m1Height, int m1Width, 
		Span<double> m2, int m2Height, int m2Width, 
		Span<double> result)
	{
		if (m1Width != m2Height)  // it's cycle stop variable
			throw new ArgumentException("Dimensions don't match");
		
		for (var i = 0; i < m1Height; i++)
		for (var j = 0; j < m2Width; j++)
		{
			result[i * m2Width + j] = 0;
			for (var k = 0; k < m2Height; k++)
				result[i * m2Width + j] += m1[i * m1Width + k] * m2[k * m2Width + j];
		}
	}
}
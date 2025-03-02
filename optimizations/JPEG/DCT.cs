using System;
using System.Threading.Tasks;
using JPEG.Utilities;

namespace JPEG;

public class DCT
{
	private readonly int dctSize;
	private readonly double[,] biasesMatrix;
	private readonly double[,] biasesMatrixT;
	
	public DCT(int dctSize)
	{
		this.dctSize = dctSize;
		biasesMatrix = new double[dctSize,dctSize];
		biasesMatrixT = new double[dctSize,dctSize];
		
		for (var k = 0; k < dctSize; k++)
		for (var n = 0; n < dctSize; n++)
		{
			var ck = k == 0 ? Math.Sqrt(dctSize) : Math.Sqrt(dctSize / 2d);
			biasesMatrix[k, n] = 1 / ck * Math.Cos(Math.PI / dctSize * (n + 0.5) * k);
			biasesMatrixT[n, k] = biasesMatrix[k, n];
		}
	}
	
	public double[,] DCT2D(double[,] input)
	{
		var width = input.GetLength(1);
		var height = input.GetLength(0);

		if (height != dctSize || width != dctSize)
			throw new ArgumentException("Dimensions don't match");
		
		var semiResult = MatrixMultiply(biasesMatrix, input);
		return MatrixMultiply(semiResult, biasesMatrixT);
	}
	
	public double[,] IDCT2D(double[,] input)
	{
		var width = input.GetLength(1);
		var height = input.GetLength(0);
		
		if (height != dctSize || width != dctSize)
			throw new ArgumentException("Dimensions don't match");

		var semiResult = MatrixMultiply(biasesMatrixT, input);
		return MatrixMultiply(semiResult, biasesMatrix);
	}

	private static double[,] MatrixMultiply(double[,] m1, double[,] m2)
	{
		var result = new double[m1.GetLength(0), m2.GetLength(1)];

		Parallel.For(0, m1.GetLength(0), i =>
		{
			for (var j = 0; j < m2.GetLength(1); j++) 
			for (var k = 0; k < m1.GetLength(0); k++)
				result[i, j] += m1[i, k] * m2[k, j];
		});
		
		return result;
	}
}
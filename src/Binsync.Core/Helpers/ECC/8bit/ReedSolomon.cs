using System;

namespace Binsync.Core.Helpers.ECC
{
	class ReedSolomon
	{
		byte[] polynomial;
		int paritySymbols;

		public ReedSolomon(int paritySymbols)
		{
			this.paritySymbols = paritySymbols;
			polynomial = GeneratePolynomial(paritySymbols);
			//can be done once in pool. only needed for encode. but num of parities also needed for syndrom gen
		}

		byte[] GeneratePolynomial(int paritySymbols)
		{
			var g = new byte[] { 1 };
			for (int i = 0; i < paritySymbols; i++)
				g = GaloisField.MultiplyPolinomials(g, new byte[] { 1, GaloisField.Exp[i] });
			return g;
		}

		byte[] CalculateSyndromes(byte[] data)
		{
			byte[] syndromes = new byte[paritySymbols];
			for (int i = 0; i < paritySymbols; i++)
				syndromes[i] = GaloisField.EvaluatePolinomial(data, GaloisField.Exp[i]);

			return syndromes;
		}

		public byte[] Encode(byte[] input)
		{
			byte[] output = new byte[input.Length + paritySymbols];

			Array.Copy(input, output, input.Length);

			for (int i = 0; i < input.Length; i++)
			{
				byte coef = output[i];
				if (coef != 0)
					for (int j = 0; j < polynomial.Length; j++)
						output[i + j] ^= GaloisField.Multiply(polynomial[j], coef);
			}
			Array.Copy(input, output, input.Length);

			return output;
		}


		byte[] cachedQ = null;

		public void CacheQ(int dataLength, byte[] erasureLocators)
		{
			var q = new byte[] { 1 };
			for (int i = 0; i < erasureLocators.Length; i++)
			{
				byte x = GaloisField.Exp[dataLength - 1 - erasureLocators[i]];
				q = GaloisField.MultiplyPolinomials(q, new byte[] { x, 1 });
			}
			cachedQ = q;
		}

		public void Decode(byte[] data, byte[] erasureLocators)
		{
			Decode(data, erasureLocators, false);
		}

		public void Decode(byte[] data, byte[] erasureLocators, bool cached)
		{
			byte[] syndromes = CalculateSyndromes(data);

			byte[] q;
			if (cached)
			{
				q = new byte[cachedQ.Length];
				cachedQ.CopyTo(q, 0); //copyto because threads
			}
			else
			{
				q = new byte[] { 1 };
				for (int i = 0; i < erasureLocators.Length; i++)
				{
					byte x = GaloisField.Exp[data.Length - 1 - erasureLocators[i]];
					q = GaloisField.MultiplyPolinomials(q, new byte[] { x, 1 });
				}
			}

			byte[] p = new byte[erasureLocators.Length];
			Array.Copy(syndromes, p, erasureLocators.Length);

			Array.Reverse(p);

			p = GaloisField.MultiplyPolinomials(p, q);

			byte[] tempP = new byte[erasureLocators.Length];
			Array.Copy(p, p.Length - erasureLocators.Length, tempP, 0, erasureLocators.Length);
			p = tempP;

			byte[] tempQ = new byte[q.Length / 2];
			int oddeven = (q.Length % 2 == 0) ? 0 : 1;
			for (int i = 0; i < q.Length / 2; i++)
				tempQ[i] = q[oddeven + i * 2];
			q = tempQ;

			for (int i = 0; i < erasureLocators.Length; i++)
			{
				byte x = GaloisField.Exp[erasureLocators[i] + 256 - data.Length];
				byte y = GaloisField.EvaluatePolinomial(p, x);
				byte z = GaloisField.EvaluatePolinomial(q, GaloisField.Multiply(x, x));
				data[erasureLocators[i]] ^= GaloisField.Divide(y, GaloisField.Multiply(x, z));
			}
		}
	}
}

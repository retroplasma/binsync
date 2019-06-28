//#define OPTIMIZATION2D
//#define OPTIMIZATIONNOCONDITION

using System;
using System.Runtime.CompilerServices;

namespace Binsync.Core.Helpers.ECC
{
    static class GaloisField
    {
		#if OPTIMIZATIONNOCONDITION
		public static byte[] Exp = new byte[1024];
		public static short[] Log = new short[256];
		#else
		public static byte[] Exp = new byte[512];
		public static byte[] Log = new byte[256];
		#endif

		#if OPTIMIZATION2D
		public static byte[][] MultiplicationTable;
		public static byte[][] DivisionTable;
		#endif

		static GaloisField()
        {
            for (int i = 0; i < 512; i++)
                Exp[i] = 1;

            short x = 1;
            for (byte i = 1; i < 255; i++)
            {
                x <<= 1;
                if ((x & 0x100) != 0)
                    x ^= 0x11d;
               
                Exp[i] = (byte)x;
                Log[x] = i;
            }

            for (int i = 255; i < 512; i++)
                Exp[i] = Exp[i - 255];

			#if OPTIMIZATIONNOCONDITION
			//Exp[506] = 255;
			//Exp[507]++;
			//Exp[508]++;
			// Log value range 0 - 254
			//Log[142] = 3;
			// 254+254
			// 254+255-1
			// 508 could happen?! 509 only on divide by zero!?
			// 510 can happen
			// Exp[509] = 0; // shouldn't matter?
			Log[0] = 510;
			Exp[510] = 0;
			Exp[511] = 0;
			#endif

			#if OPTIMIZATION2D
            MultiplicationTable = new byte[256][];
            for (int a = 0; a < 256; a++)
            {
                MultiplicationTable[a] = new byte[256];
                for (int b = 0; b < 256; b++)
                {
                    MultiplicationTable[a][b] = MultiplyStandard((byte)a, (byte)b);
                }
            }
            DivisionTable = new byte[256][];
            for (int a = 0; a < 256; a++)
            {
                DivisionTable[a] = new byte[256];
                for (int b = 1; b < 256; b++)
                {
                    DivisionTable[a][b] = DivideStandard((byte)a, (byte)b);
                }
            }
			#endif
        }

		public static byte Multiply(byte x, byte y)
        {
			#if OPTIMIZATION2D
            return MultiplicationTable[x][y];
			#else
			return MultiplyStandard(x,y);
			#endif
        }
		public static byte Divide(byte x, byte y)
        {
			#if OPTIMIZATION2D
            // if (y == 0)
            //    throw new DivideByZeroException();
            return DivisionTable[x][y];
			#else
			return DivideStandard(x,y);
			#endif
        }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte MultiplyStandard(byte x, byte y)
        {
			#if !OPTIMIZATIONNOCONDITION
            if ((x == 0) || (y == 0))
                return 0;
			#endif
            return Exp[Log[x] + Log[y]];
        }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte DivideStandard(byte x, byte y)
        {
			#if !OPTIMIZATIONNOCONDITION
            if (y == 0)
                throw new DivideByZeroException();
            if (x == 0)
                return 0;
			#endif
            return Exp[Log[x] + 255 - Log[y]];
        }
        
		// TODO: karatsuba

		public static byte[] MultiplyPolinomials(byte[] p, byte[] q)
        {
            byte[] r = new byte[p.Length + q.Length - 1];
            for (int j = 0; j < q.Length; j++)
                for (int i = 0; i < p.Length; i++)
                    r[i + j] ^= Multiply(p[i], q[j]);
            return r;
        }

		public static byte EvaluatePolinomial(byte[] p, byte x)
        {
            byte y = p[0];
            for (int i = 1; i < p.Length; i++)
                y = (byte)(Multiply(y, x) ^ p[i]);
            return y;
        }
    }
}

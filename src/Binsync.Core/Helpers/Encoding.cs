using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography;
using System.Numerics;
using System.Linq;
using System.IO;

namespace Binsync.Core.Helpers
{
	public static class Encoding
	{
		#region Hex

		/// <summary>
		/// Bytes array to hex string.
		/// </summary>
		/// <returns>Hex string.</returns>
		/// <param name="byteArray">Byte array.</param>
		public static string ByteArrayToHexString(byte[] byteArray)
		{
			StringBuilder hex = new StringBuilder(byteArray.Length * 2);
			foreach (byte b in byteArray)
				hex.AppendFormat("{0:x2}", b);
			return hex.ToString();
		}

		public static string ToHexString(this byte[] bytes)
		{
			return ByteArrayToHexString(bytes);
		}

		// https://stackoverflow.com/questions/311165/how-do-you-convert-byte-array-to-hexadecimal-string-and-vice-versa
		public static byte[] FromHexToBytes(this string hex)
		{
			int NumberChars = hex.Length / 2;
			byte[] bytes = new byte[NumberChars];
			using (var sr = new StringReader(hex))
			{
				for (int i = 0; i < NumberChars; i++)
					bytes[i] =
						Convert.ToByte(new string(new char[2] { (char)sr.Read(), (char)sr.Read() }), 16);
			}
			return bytes;
		}

		#endregion

		#region Base64

		public static class Base64
		{
			static public string EncodeTo64(string toEncode)
			{
				byte[] toEncodeAsBytes
					= System.Text.ASCIIEncoding.ASCII.GetBytes(toEncode);
				string returnValue
					= System.Convert.ToBase64String(toEncodeAsBytes);
				return returnValue;
			}

			static public string EncodeTo64(byte[] toEncodeAsBytes)
			{
				string returnValue
					= System.Convert.ToBase64String(toEncodeAsBytes);
				return returnValue;
			}

			static public string DecodeFrom64(string encodedData)
			{
				byte[] encodedDataAsBytes
					= System.Convert.FromBase64String(encodedData);
				string returnValue =
					System.Text.ASCIIEncoding.ASCII.GetString(encodedDataAsBytes);
				return returnValue;
			}

			static public byte[] DecodeFrom64ToBytes(string encodedData)
			{
				byte[] encodedDataAsBytes
					= System.Convert.FromBase64String(encodedData);
				return encodedDataAsBytes;
			}
		}
		#endregion

		#region Base30
		public static class Base30Special
		{
			// https://github.com/mstum/mstum.utils/blob/master/mstum.utils/Base36Big.cs (MIT)
			private const string CharList = "23456789ABCDEFGHKMNPQRSTUVWXYZ";
			//private static char[] CharArray = CharList.ToCharArray();

			/// <summary>
			/// Decodes the specified Base30 input.
			/// </summary>
			/// <param name="input">Input.</param>
			public static BigInteger Decode(string input)
			{
				var reversed = input.ToLower().Reverse();
				BigInteger result = BigInteger.Zero;
				int pos = 0;
				foreach (char c in reversed)
				{
					result = BigInteger.Add(result, BigInteger.Multiply(CharList.IndexOf(c), BigInteger.Pow(CharList.Length, pos)));
					pos++;
				}
				return result;
			}

			/// <summary>
			/// Encodes the specified input to Base30.
			/// </summary>
			/// <param name="input">Input.</param>
			public static String Encode(BigInteger input)
			{
				if (input.Sign < 0)
					throw new ArgumentOutOfRangeException();

				var result = new Stack<char>();
				while (!input.IsZero)
				{
					var index = (int)(input % CharList.Length);
					result.Push(CharList[index]);
					input = BigInteger.Divide(input, CharList.Length);
				}
				return new string(result.ToArray());
			}
		}
		#endregion

		#region UTF8
		public static byte[] GetBytesUTF8(this string str)
		{
			return System.Text.Encoding.UTF8.GetBytes(str);
		}

		public static string GetStringUTF8(this byte[] bytes)
		{
			return System.Text.Encoding.UTF8.GetString(bytes);
		}
		#endregion
	}
}


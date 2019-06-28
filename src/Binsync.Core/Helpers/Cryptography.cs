using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography;
using System.IO;

namespace Binsync.Core.Helpers
{
	public static class Cryptography
	{
		#region Hashing

		public static byte[] SHA1(this byte[] data){
			return Helpers.Cryptography.HashArray (System.Security.Cryptography.SHA1.Create (), data);
		}

		public static byte[] SHA256(this byte[] data){
			return Helpers.Cryptography.HashArray (System.Security.Cryptography.SHA256.Create (), data);
		}

		/// <summary>
		/// Hashs a portion of a stream at the given offset
		/// </summary>
		/// <returns>Hash</returns>
		/// <param name="algorithm">Algorithm.</param>
		/// <param name="stream">Stream.</param>
		/// <param name="offset">Offset.</param>
		/// <param name="size">Size.</param>
		public static byte[] HashBlock(HashAlgorithm algorithm, Stream stream, long offset, long size)
		{
			HashAlgorithm hasher = algorithm;

			const int bufferLength = 4096 * 16;
			byte[] buffer = new byte[bufferLength];
			long blockCount = size / bufferLength + ((size % bufferLength) == 0 ? 0 : 1);
			lock (stream)
			{
				stream.Position = offset;
				for (long i = 0; i < blockCount - 1; i++)
				{
					stream.Read(buffer, 0, buffer.Length);
					hasher.TransformBlock(buffer, 0, bufferLength, buffer, 0);
				}
				int read = stream.Read(buffer, 0, buffer.Length);
				hasher.TransformFinalBlock(buffer, 0, read);
			}
			return hasher.Hash;
		}

		public static byte[] HashArray(HashAlgorithm algorithm, byte[] data)
		{
			using (MemoryStream ms = new MemoryStream(data)) {
				return HashBlock (algorithm, ms, 0, ms.Length);
			}
		}
		#endregion

		#region Symmetric Encryption

		public enum CryptoType
		{
			Encryptor = 0,
			Decryptor = 1
		}

		/// <summary>
		/// Creates encryptor or decryptor from key and IV.
		/// </summary>
		/// <returns>The crypto.</returns>
		/// <param name="keyBytes">Key bytes.</param>
		/// <param name="iv">IV</param>
		/// <param name="cryptoType">Crypto type.</param>
		public static ICryptoTransform CreateCrypto(byte[] keyBytes, byte[] iv, CryptoType cryptoType)
		{

			if (keyBytes.Length * 8 != 256)
				throw new Exception("Invalid key size.");

			RijndaelManaged symmetricKey = new RijndaelManaged();
			symmetricKey.Mode = CipherMode.CBC;
			symmetricKey.KeySize = 256;
			// default padding: PKCS7
			
			ICryptoTransform cryptor;
			switch (cryptoType)
			{
			case CryptoType.Encryptor:
				cryptor = symmetricKey.CreateEncryptor(keyBytes, iv);
				break;
			case CryptoType.Decryptor:
				cryptor = symmetricKey.CreateDecryptor(keyBytes, iv);
				break;
			default:
				cryptor = null;
				break;
			}
			return cryptor;
		}
		#endregion

		#region Random Generator

		/// <summary>
		/// Returns random non zero bytes.
		/// </summary>
		/// <returns>The random non zero bytes.</returns>
		/// <param name="byteCount">Byte count.</param>
		public static byte[] GetRandomNonZeroBytes(int byteCount)
		{
			byte[] bytes = new byte[byteCount];
			Constants.RNG.GetNonZeroBytes(bytes);
			return bytes;
		}

		/// <summary>
		/// Returns random bytes.
		/// </summary>
		/// <returns>The random bytes.</returns>
		/// <param name="byteCount">Byte count.</param>
		public static byte[] GetRandomBytes(int byteCount)
		{
			byte[] bytes = new byte[byteCount];
			Constants.RNG.GetBytes(bytes);
			return bytes;
		}
		#endregion


		public static bool CompareByteArrays(byte[] array1, byte[] array2)
		{
			if (array1.Length != array2.Length)
				return false;

			var compare = 0;
			for (var i = 0; i < array1.Length; i++)
				compare |= array1[i] ^ array2[i]; 

			return compare == 0;
		}
	}
}

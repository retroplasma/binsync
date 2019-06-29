//TODO: read http://blog.cryptographyengineering.com/2012/02/multiple-encryption.html
//#define TRIPLE_ENCRYPTION

using System;
using System.Text;
using System.IO;
using System.Security.Cryptography;
using System.Linq;

using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Paddings;

namespace Binsync.Core
{
	public class Encryption
	{
		readonly Identifier identifier;

		public Encryption(Identifier identifier)
		{
			this.identifier = identifier;
		}

		static IBlockCipher[] aesTwofishSerpent
		{
			get
			{
				return new IBlockCipher[]
				{
					new AesFastEngine (),
					new TwofishEngine (),
					new SerpentEngine (),
				};
			}
		}

		static IBlockCipher[] aes
		{
			get
			{
				return new IBlockCipher[]
				{
					new AesFastEngine (),
				};
			}
		}

		//Maybe: derive subkeys with id?
		//Maybe: switch to gcm or ctr

		/// <summary>
		/// Encrypt + HMAC the specified data with global keys and include the ID in the authentication.
		/// </summary>
		/// <param name="data">Data.</param>
		/// <param name="id">ID.</param>
		public byte[] Encrypt(byte[] data, string id)
		{
			var additionalAuthenticationData = Encoding.UTF8.GetBytes(id);

#if TRIPLE_ENCRYPTION
			var cascade = aesTwofishSerpent;
#else
			var cascade = aes;
#endif

			return cascadeCrypt(true, data, additionalAuthenticationData, cascade);
		}

		/// <summary>
		/// Decrypt and check the data for integrity using global keys, including the ID in the authentication, using global keys.
		/// </summary>
		/// <param name="data">Data.</param>
		/// <param name="id">ID.</param>
		public byte[] Decrypt(byte[] data, string id)
		{
			var additionalAuthenticationData = Encoding.UTF8.GetBytes(id);

#if TRIPLE_ENCRYPTION
			var cascade = aesTwofishSerpent;
#else
			var cascade = aes;
#endif

			return cascadeCrypt(false, data, additionalAuthenticationData, cascade);
		}

		byte[] cascadeCrypt(bool encrypt, byte[] data, byte[] additionalAuthenticationData, params IBlockCipher[] ciphers)
		{
			return encrypt
				? ciphers.Aggregate(data, (acc, cipher) => blockCbcHmacEncrypt(cipher, acc, additionalAuthenticationData))
				: ciphers.Reverse().Aggregate(data, (acc, cipher) => blockCbcHmacDecrypt(cipher, acc, additionalAuthenticationData));
		}

		// cascade auth needed? or can be omitted?

		byte[] blockCbcHmacEncrypt(IBlockCipher blockCipher, byte[] data, byte[] additionalAuthenticationData)
		{
			byte[] iv = Helpers.Cryptography.GetRandomBytes(16);

			using (MemoryStream inputStream = new MemoryStream(data))
			{
				int size = data.Length;

				using (MemoryStream ms = new MemoryStream(size))
				{
					byte[] idBuf = additionalAuthenticationData;
					ms.Write(idBuf, 0, idBuf.Length);

					ms.Write(iv, 0, iv.Length);

					cbcStream(blockCipher, true, inputStream, iv, ms);

					ms.Position = 0;

					byte[] authHash = hmacWholeStream(ms);

					using (MemoryStream ms2 = new MemoryStream((int)(authHash.Length + ms.Length)))
					{
						ms2.Write(authHash, 0, authHash.Length);
						ms2.Write(ms.ToArray(), idBuf.Length, (int)ms.Length - idBuf.Length);

						return ms2.ToArray();
					}
				}
			}
		}

		byte[] blockCbcHmacDecrypt(IBlockCipher blockCipher, byte[] data, byte[] additionalAuthenticationData)
		{
			byte[] buffer = new byte[4096];

			using (MemoryStream inputStream = new MemoryStream(data))
			{
				byte[] authHash = new byte[256 / 8];
				inputStream.Read(authHash, 0, authHash.Length);

				using (MemoryStream ms2 = new MemoryStream())
				{
					int read = 0;

					byte[] idBuf = additionalAuthenticationData;
					ms2.Write(idBuf, 0, idBuf.Length);

					while ((read = inputStream.Read(buffer, 0, buffer.Length)) > 0)
					{
						ms2.Write(buffer, 0, read);
					}

					ms2.Position = 0;

					byte[] authHashGot = hmacWholeStream(ms2);

					if (!Helpers.Cryptography.CompareByteArrays(authHash, authHashGot))
						throw new Exception("HMACs don't match");

					ms2.Position = idBuf.Length;

					byte[] iv = new byte[16];
					ms2.Read(iv, 0, iv.Length);

					return cbcStream(blockCipher, false, ms2, iv);
				}

			}
		}



		byte[] hmacWholeStream(Stream inputStream)
		{
			return hmacStream(inputStream, 0, inputStream.Length);
		}

		byte[] hmacStream(Stream inputStream, long offset, long size)
		{
			using (var hmacAlgorithm = new HMACSHA256(identifier.AuthenticationKey))
			{
				return Helpers.Cryptography.HashBlock(hmacAlgorithm, inputStream, offset, size);
			}
		}

		byte[] cbcStream(IBlockCipher blockCipher, bool encrypt, Stream inputStream, byte[] iv)
		{
			using (var outputStream = new MemoryStream())
			{
				cbcStream(blockCipher, encrypt, inputStream, iv, outputStream);

				return outputStream.ToArray();
			}
		}

		void cbcStream(IBlockCipher blockCipher, bool encrypt, Stream inputStream, byte[] iv, Stream outputStream)
		{
			const int size = 4096;
			byte[] buffer = new byte[size];

			var engine = blockCipher;
			var _cipher = new CbcBlockCipher(engine);
			var cipher = new PaddedBufferedBlockCipher(_cipher, new Pkcs7Padding());

			cipher.Init(encrypt, new ParametersWithIV(
				new KeyParameter(identifier.EncryptionKey), iv)
			);

			var left = (int)(inputStream.Length - inputStream.Position);

			for (int i = 0; i < left / size - 1; i += 1)
			{
				inputStream.Read(buffer, 0, size);
				var bytes = cipher.ProcessBytes(buffer, 0, size);
				outputStream.Write(bytes, 0, bytes.Length);
			}

			left = (int)(inputStream.Length - inputStream.Position);
			buffer = new byte[left];

			{
				inputStream.Read(buffer, 0, left);
				var bytes = cipher.DoFinal(buffer, 0, left);
				outputStream.Write(bytes, 0, bytes.Length);
			}
		}

		/*
		 * For sharing capabilities:
		 *   Instead of IV:
		 *     Nonce
		 *     HMAC(Nonce) = Key
		 * 
		 *   Or in Subject
		 *   Or in meta against sharing download overhead but then it's meta overhead
		 */
	}
}


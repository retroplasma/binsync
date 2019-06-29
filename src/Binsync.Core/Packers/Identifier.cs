using System;
using System.Security.Cryptography;
using System.Threading.Tasks;

using Binsync.Core.Helpers;

namespace Binsync.Core
{
	public class Identifier
	{
		public byte[] EncryptionKey { get; private set; }
		public byte[] AuthenticationKey { get; private set; }
		public byte[] PinpointingKey { get; private set; }
		public string PublicHash { get; private set; }

		public Identifier(byte[] key)
		{
			const int iterations = 1000; // * 42;

			Func<byte, byte[]> derive = index => new Rfc2898DeriveBytes(new[] { index }, key, iterations).GetBytes(256 / 8);

			Parallel.Invoke(
				() => EncryptionKey = derive(1),
				() => AuthenticationKey = derive(2),
				() => PinpointingKey = derive(3),

				() => PublicHash = derive(4).SHA1().ToHexString()
			);
		}
	}
}


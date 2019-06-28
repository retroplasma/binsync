using NUnit.Framework;
using System;
using Binsync.Core;
using Binsync.Core.Helpers;

namespace Tests
{
	public class Tests
	{
		static Logger logger = new Logger(Console.WriteLine);

		[SetUp]
		public void Setup()
		{
		}

		[Test]
		public static void TestCompression()
		{
			testCompression("test data");
			testCompression("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
		}

		static void testCompression(string str)
		{
			Func<byte[], string> hashStr = b => b.SHA1().ToHexString();

			DateTime start, end;
			TimeSpan span;

			var data = str.GetBytesUTF8();
			var dataHash = hashStr(data);

			logger.Log("Length: {0}", data.Length);
			logger.Log("Hash: {0}", dataHash);

			logger.Log("");

			start = DateTime.Now;
			var compressed = data.GetCompressed();
			var compressedHash = compressed.Compose(hashStr);//hashStr (compressed);
			end = DateTime.Now;
			span = new TimeSpan(end.Ticks - start.Ticks);
			logger.Log("Compression:\t{0} seconds", span.TotalSeconds);
			logger.Log("Length: {0}", compressed.Length);
			logger.Log("Hash: {0}", compressedHash);

			logger.Log("");

			start = DateTime.Now;
			var decompressed = compressed.GetDecompressed();
			var decompressedHash = hashStr(decompressed);
			end = DateTime.Now;
			span = new TimeSpan(end.Ticks - start.Ticks);
			logger.Log("Decompression:\t{0} seconds", span.TotalSeconds);
			logger.Log("Length: {0}", decompressed.Length);
			logger.Log("Hash: {0}", decompressedHash);

			Assert.True(dataHash == decompressedHash);
		}
	}
}
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Binsync.Core;
using Binsync.Core.Helpers;

namespace Tests
{
	public class HashTests
	{

		[SetUp]
		public void Setup()
		{
		}

		[Test]
		public static void TestHashBlock()
		{
			HashSet<string> hashes = new HashSet<string>();
			hashes.Add(new byte[] { }.SHA256().ToHexString());
			for (var i = 1; i <= 100; i++)
			{
				hashes.Add(Enumerable.Repeat<byte>(0, i).ToArray().SHA256().ToHexString());
				hashes.Add(Enumerable.Repeat<byte>(255, i).ToArray().SHA256().ToHexString());
				hashes.Add(Enumerable.Range(1, i).Select(x => (byte)x).ToArray().SHA256().ToHexString());
				hashes.Add(Enumerable.Range(2, i).Select(x => (byte)x).ToArray().SHA256().ToHexString());
			}
			Assert.AreEqual(401, hashes.Count);
		}
	}
}
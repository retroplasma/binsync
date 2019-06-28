using NUnit.Framework;
using System;
using Binsync.Core;
using Binsync.Core.Helpers;
using Binsync.Core.Integrity;

namespace Tests
{
	public class ParityTests
	{
		static Logger logger = new Logger(Console.WriteLine);

		[SetUp]
		public void Setup()
		{
		}

		[Test]
		public static void TestParityPacker()
		{
			//const int randomCount = 518387;
			//const int randomCount = 524288;
			//const int randomCount = 1024*4+2+1;

			//const int randomCount = 1024*32 *2;
			const int randomCount = 518387;
			//Func<string, byte[]> utf8b = Encoding.GetBytesUTF8;
			byte[][] input = {
				General.GetWeakPseudoRandomBytes(randomCount + General.GetWeakPseudoRandomBytes(1)[0]),
				General.GetWeakPseudoRandomBytes(randomCount + General.GetWeakPseudoRandomBytes(1)[0]),
				General.GetWeakPseudoRandomBytes(randomCount + General.GetWeakPseudoRandomBytes(1)[0]),
				General.GetWeakPseudoRandomBytes(randomCount + General.GetWeakPseudoRandomBytes(1)[0]),
				General.GetWeakPseudoRandomBytes(randomCount + General.GetWeakPseudoRandomBytes(1)[0]),
				General.GetWeakPseudoRandomBytes(randomCount + General.GetWeakPseudoRandomBytes(1)[0]),
				General.GetWeakPseudoRandomBytes(randomCount + General.GetWeakPseudoRandomBytes(1)[0]),
				General.GetWeakPseudoRandomBytes(randomCount + General.GetWeakPseudoRandomBytes(1)[0]),
				General.GetWeakPseudoRandomBytes(randomCount + General.GetWeakPseudoRandomBytes(1)[0]),
				General.GetWeakPseudoRandomBytes(randomCount + General.GetWeakPseudoRandomBytes(1)[0]),

				General.GetWeakPseudoRandomBytes(randomCount + General.GetWeakPseudoRandomBytes(1)[0]),
				General.GetWeakPseudoRandomBytes(randomCount + General.GetWeakPseudoRandomBytes(1)[0]),
				General.GetWeakPseudoRandomBytes(randomCount + General.GetWeakPseudoRandomBytes(1)[0]),
				General.GetWeakPseudoRandomBytes(randomCount + General.GetWeakPseudoRandomBytes(1)[0]),
				General.GetWeakPseudoRandomBytes(randomCount + General.GetWeakPseudoRandomBytes(1)[0]),
				General.GetWeakPseudoRandomBytes(randomCount + General.GetWeakPseudoRandomBytes(1)[0]),
				General.GetWeakPseudoRandomBytes(randomCount + General.GetWeakPseudoRandomBytes(1)[0]),
				General.GetWeakPseudoRandomBytes(randomCount + General.GetWeakPseudoRandomBytes(1)[0]),
				General.GetWeakPseudoRandomBytes(randomCount + General.GetWeakPseudoRandomBytes(1)[0]),
				General.GetWeakPseudoRandomBytes(randomCount + General.GetWeakPseudoRandomBytes(1)[0]),
			};

			byte[][] parity = Parity.CreateParity(input, 10);

			logger.Log("");
			/*
			foreach (var x in input.Select(y => y.GetStringUTF8()))
				Console.WriteLine (x);
			foreach (var x in parity.Select(y => y.ToHexString()))
				Console.WriteLine (x);*/
			foreach (var x in input)
				logger.Log(x.SHA256().ToHexString().Substring(0, 5) + " " + x.Length);//+ " " + x.GetStringUTF8());
			foreach (var x in parity)
				logger.Log(x.SHA256().ToHexString().Substring(0, 5) + " " + x.Length);// + " " + x.ToHexString());

			Func<byte[][], Parity.ParityInfo[]> genTuples = (datas) =>
			{
				Parity.ParityInfo[] tuples = new Parity.ParityInfo[datas.Length];
				for (int i = 0; i < tuples.Length; i++)
				{
					tuples[i] = new Parity.ParityInfo()
					{
						Broken = false,
						RealLength = (uint)datas[i].Length,
						Data = datas[i]
					};
				}
				return tuples;
			};

			Parity.ParityInfo[] data_tuples = genTuples(input);
			Parity.ParityInfo[] parity_tuples = genTuples(parity);

			Action<int, Parity.ParityInfo[]> breakTuple = (index, tuples) =>
			{
				tuples[index].Broken = true;
				tuples[index].Data = null;
			};

			for (int i = 0; i < parity.Length - 3; i++)
				breakTuple(i, parity_tuples);
			breakTuple(2, data_tuples);
			//breakTuple (3, data_tuples);
			breakTuple(1, data_tuples);
			logger.Log("repair");
			var success = Parity.RepairWithParity(ref data_tuples, ref parity_tuples);

			logger.Log("");

			if (!success)
			{
				Assert.Fail("Could not repair with parity!");
				return;
			}

			foreach (var x in data_tuples)
				logger.Log(x.Data.SHA256().ToHexString().Substring(0, 5) + " " + x.Data.Length);// + " " + x.Data.GetStringUTF8());
			foreach (var x in parity_tuples)
				logger.Log(x.Data.SHA256().ToHexString().Substring(0, 5) + " " + x.Data.Length);// + " " + x.Data.ToHexString());

			Assert.True(data_tuples.Length == input.Length);
			Assert.True(parity_tuples.Length == parity.Length);

			for (var i = 0; i < input.Length; i++)
			{
				Assert.True(input[i].SHA256().ToHexString() == data_tuples[i].Data.SHA256().ToHexString());
			}

			for (var i = 0; i < parity.Length; i++)
			{
				Assert.True(parity[i].SHA256().ToHexString() == parity_tuples[i].Data.SHA256().ToHexString());
			}
		}
	}
}
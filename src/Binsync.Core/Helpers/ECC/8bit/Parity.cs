using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Binsync.Core.Helpers;

namespace Binsync.Core.Integrity
{
	public static class Parity
	{
		public class ParityInfo
		{
			public byte[] Data;
			public bool Broken;
			public uint RealLength;
		}

		public static bool RepairWithParity(ref ParityInfo[] data, ref ParityInfo[] parity)
		{
			var indices = new List<byte>();
			var streams = new List<MemoryStream>();

			Action<ParityInfo[], uint, byte> convert = (infos, length, start) =>
			{
				for (byte i = 0; i < infos.Length; i++)
				{

					var ms_data = new byte[length];

					if (infos[i].Broken)
						indices.Add((byte)(i + start));
					else
						Array.Copy(infos[i].Data, ms_data, infos[i].Data.Length);

					streams.Add(new MemoryStream(ms_data));
				}
			};

			uint stream_length = parity[0].RealLength;
			convert(data, stream_length, 0);
			convert(parity, stream_length, (byte)data.Length);

			if (indices.Count > parity.Length)
			{
				foreach (var ms in streams)
					ms.Close();
				return false;
			}

			byte[] indices_array = indices.ToArray();
			MemoryStream[] streams_array = streams.ToArray();

			StreamParity.Repair(streams_array, parity.Length, indices_array);

			for (int i = 0; i < indices_array.Length; i++)
			{
				int index = indices_array[i];
				ParityInfo repairMe = index < data.Length ? data[index] : parity[index - data.Length];
				streams[index].SetLength(repairMe.RealLength);
				repairMe.Data = streams[index].ToArray();
				streams[index].Close();
			}

			return true;
		}

		public static byte[][] CreateParity(byte[][] input, int outputCount)
		{
			if (outputCount <= 0)
			{
				throw new Exception("Can't create zero parity");
			}

			if (input.Length + outputCount > 256)
			{
				throw new Exception("Can't handle more than 256 input + output data");
			}

			var output = new byte[outputCount][];

			var input_streams = new MemoryStream[input.Length];
			var output_streams = new MemoryStream[output.Length];

			Action<byte[][], int, MemoryStream[]> convert = (datas, length, streams) =>
			{
				for (byte i = 0; i < datas.Length; i++)
				{
					var ms_data = new byte[length];

					if (datas[i] != null)
						Array.Copy(datas[i], ms_data, datas[i].Length);

					streams[i] = new MemoryStream(ms_data);
				}
			};

			int stream_length = input.Max(x => x.Length);
			convert(input, stream_length, input_streams);
			convert(output, stream_length, output_streams);

			StreamParity.GenerateStreams(input_streams, output_streams);

			for (int i = 0; i < input_streams.Length; i++)
				input_streams[i].Close();

			for (int i = 0; i < output_streams.Length; i++)
			{
				output[i] = output_streams[i].ToArray();
				output_streams[i].Close();
			}
			return output;
		}
	}
}


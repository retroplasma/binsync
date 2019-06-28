using System;
using System.IO;
using System.IO.Compression;

namespace Binsync.Core.Helpers
{
	public static class Compression
	{
		enum Compressed
		{
			YES, NO
		}

		public static byte[] GetCompressed(this byte[] data)
		{
			return Compress(data);
		}

		public static byte[] Compress(byte[] data)
		{
			if (data.Length == 0)
				return data;
			using (MemoryStream input = new MemoryStream(data.Length + 1))
			{
				input.WriteByte((byte)Compressed.NO);
				input.Write(data, 0, data.Length);
				input.Position = 1;
				using (MemoryStream output = new MemoryStream())
				{
					output.WriteByte((byte)Compressed.YES);

					DeflateStream compressionStream;
					using (compressionStream = new DeflateStream(output, CompressionMode.Compress, true))
					{

						int read = 0;
						byte[] buffer = new byte[4096];
						while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
						{
							compressionStream.Write(buffer, 0, read);
						}

					}

					try
					{
						if (output.Length > input.Length)
							return input.ToArray();
						else
							return output.ToArray();
					}
					finally
					{
						compressionStream.Close();
					}
				}

			}
		}

		public static byte[] GetDecompressed(this byte[] data)
		{
			return Decompress(data);
		}

		public static byte[] Decompress(byte[] data)
		{
			if (data.Length == 0)
				return data;
			using (MemoryStream input = new MemoryStream(data))
			{
				Compressed flag = (Compressed)(byte)input.ReadByte();

				using (MemoryStream output = new MemoryStream())
				{
					int read = 0;
					byte[] buffer = new byte[4096];

					switch (flag)
					{
						case Compressed.YES:
							using (DeflateStream decompressionStream = new DeflateStream(input, CompressionMode.Decompress))
							{
								while ((read = decompressionStream.Read(buffer, 0, buffer.Length)) > 0)
								{
									output.Write(buffer, 0, read);
								}
							}
							break;
						case Compressed.NO:
							while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
							{
								output.Write(buffer, 0, read);
							}
							break;
					}
					return output.ToArray();
				}
			}
		}
	}
}


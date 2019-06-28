using System;

namespace Binsync.Core.Helpers
{
	public static class General
	{
		/// <summary>
		/// Gets the application path.
		/// </summary>
		/// <value>The application path.</value>
		public static string ApplicationPath
		{
			get
			{
				return System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
			}
		}


		static int m_z = 1337;
		static int m_w = 42;

		/// <summary>
		/// Returns weak pseudorandom bytes.
		/// </summary>
		/// <returns>The weak pseudorandom byte array.</returns>
		/// <param name="count">Count.</param>
		public static byte[] GetWeakPseudoRandomBytes(int count)
		{
			byte[] bytes = new byte[count];
			for (int i = 0; i < bytes.Length; i++)
			{
				m_z = 36969 * (m_z & 65535) + (m_z >> 16);
				m_w = 18000 * (m_w & 65535) + (m_w >> 16);
				int res = (m_z << 16) + m_w;

				bytes[i] = (byte)(res & 0xFF);

			}
			return bytes;
		}

		public static byte[] GetZeroBytes(int count)
		{
			return new byte[count];
		}

		public static byte[] SerializeContract<T>(this T obj) where T : Binsync.Core.Formats.ProtoBufSerializable
		{
			using (var ms = new System.IO.MemoryStream())
			{
				ProtoBuf.Serializer.Serialize(ms, obj);
				return ms.ToArray();
			}
		}

		public static T Deserialize<T>(byte[] b) where T : Binsync.Core.Formats.ProtoBufSerializable
		{
			using (var ms = new System.IO.MemoryStream(b))
				return ProtoBuf.Serializer.Deserialize<T>(ms);
		}
	}
}


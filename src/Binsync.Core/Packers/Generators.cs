using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography;
using System.IO;
using Binsync.Core.Helpers;
using Binsync.Core.Formats;

namespace Binsync.Core
{
	public class Generator
	{
		readonly Identifier identifier;

		public Generator(Identifier identifier)
		{
			this.identifier = identifier;
		}

		#region Helpers
		// Raw
		public byte[] GenerateRawOrParityID(byte[] hash)
		{
			var indexId = DeriveIndexID(Generator.DataType.RAW, 0, hash);
			return indexId;
		}

		// Meta
		public byte[] GenerateMetaFolderID(uint index, string path)
		{
			return GenerateMetaID(index, path, MetaExtraIndexFormat.TYPE.FOLDER);
		}
		public byte[] GenerateMetaFileID(uint index, string path)
		{
			return GenerateMetaID(index, path, MetaExtraIndexFormat.TYPE.FILE);
		}
		byte[] GenerateMetaID(uint index, string path, MetaExtraIndexFormat.TYPE metaType)
		{
			var data = new MetaExtraIndexFormat { MetaType = metaType, LocalPath = path }.ToByteArray();
			var indexId = DeriveIndexID(Generator.DataType.META, index, data);
			return indexId;
		}
		// Assurance
		public byte[] GenerateAssuranceID(uint index)
		{
			var indexId = DeriveIndexID(Generator.DataType.ASSURANCE, index);
			return indexId;
		}
		#endregion

		#region Key, ID and Code Generators

		/// <summary>
		/// Gets the key from a storage code and password
		/// </summary>
		/// <returns>The key.</returns>
		/// <param name="storageCode">Storage code.</param>
		/// <param name="password">Password.</param>
		public static byte[] DeriveKey(string storageCode, string password)
		{
			var code = storageCode.FromHexToBytes();
			Rfc2898DeriveBytes db = new Rfc2898DeriveBytes(password, code, 1337 * 42);
			return db.GetBytes(256 / 8);
		}

		public enum DataType
		{
			RAW = 1, // extraData by hash index
			PARITY = 2, // exactly like raw. needs own type? extraData by hash index
			META = 3, // meta serialized path index
			ASSURANCE = 4 // index counter
		}

		/// <summary>
		/// Derives the locator.
		/// </summary>
		/// <returns>The locator.</returns>
		/// <param name="index_id">Index_id.</param>
		/// <param name="replication">Replication.</param>
		public string DeriveLocator(byte[] index_id, UInt32 replication)
		{
			string final;
			using (MemoryStream ms = new MemoryStream())
			{
				byte[] replication_bytes = BitConverter.GetBytes(replication);
				if (replication_bytes.Length != 4)
					throw new Exception("replication length mismatch");

				ms.Write(replication_bytes, 0, replication_bytes.Length);
				ms.Write(index_id, 0, index_id.Length);

				long written = ms.Position;
				using (var hmacAlgorithm = new HMACSHA512(identifier.PinpointingKey))
				{
					byte[] result = Helpers.Cryptography.HashBlock(hmacAlgorithm, ms, 0, written);
					final = Helpers.Encoding.ByteArrayToHexString(result);
					return final;
				}
			}
		}

		/// <summary>
		/// Calculates an index ID from subindices.
		/// </summary>
		/// <returns>The index ID.</returns>
		/// <param name="dataType">Data type.</param>
		/// <param name="index">Index.</param>
		public byte[] DeriveIndexID(DataType dataType, UInt32 index)
		{
			return DeriveIndexID(dataType, index, null);
		}

		/// <summary>
		/// Calculates an index ID from subindices and additional data.
		/// </summary>
		/// <returns>The index ID.</returns>
		/// <param name="dataType">Data type.</param>
		/// <param name="index">Index.</param>
		/// <param name="extraData">Data.</param>
		public byte[] DeriveIndexID(DataType dataType, UInt32 index, byte[] extraData) // epoch/retention index?
		{
			byte[] final;
			using (MemoryStream ms = new MemoryStream())
			{
				byte[] dataTypeBytes = BitConverter.GetBytes((UInt32)dataType);
				byte[] indexBytes = BitConverter.GetBytes(index);

				if (dataTypeBytes.Length != 4 | indexBytes.Length != 4)
					throw new Exception("indices length mismatch");

				ms.Write(dataTypeBytes, 0, dataTypeBytes.Length);
				ms.Write(indexBytes, 0, indexBytes.Length);

				if (extraData != null)
					ms.Write(extraData, 0, extraData.Length);

				long written = ms.Position;
				using (var hmacAlgorithm = new HMACSHA512(identifier.PinpointingKey))
				{
					byte[] result = Helpers.Cryptography.HashBlock(hmacAlgorithm, ms, 0, written);
					final = result;
					return final;
				}
			}

		}

		#endregion
	}
}


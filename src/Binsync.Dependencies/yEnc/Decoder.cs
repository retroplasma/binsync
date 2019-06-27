using System;
using System.Security.Cryptography;

namespace yEnc
{
	/// <summary>
	/// Decoder for the yEnc spec
	/// </summary>
	public class YEncDecoder:
		ICryptoTransform
	{
		const byte life = 42;		//meaning of life (or was that the answer to everything?) from hitchhikers guide - delta value used by yEnc
		const byte death = 64;		//
		const byte escapeByte = 61;	//escape byte used by yEnc
		int lineBytes = 0;
		CRC32 crc32Hasher = new CRC32();
		byte[] storedHash = null;
		bool escapeNextByte = false;

		public byte[] CRCHash 
		{
			get 
			{
				return storedHash;
			}
		}

		public int GetByteCount(
			byte[] source,
			int index,
			int count,
			bool flush
			)
		{
			if (source == null)
				throw new ArgumentNullException();

			int bytes = 0;
			//int lineBytes = this.lineBytes;
			bool escapeNextByte = this.escapeNextByte;	//keep our own copy
			for(int i=index; i<index+count; i++)
			{
				bool newline = false;
				bool abort = false;
				byte b;
				try
				{
					b = source[i];
					if (!escapeNextByte)
					{
						switch (b)
						{
							case escapeByte:
								i++;
								if (i<index+count)
								{}
								else
								{
									//what a pain.  The bytes stopped on an escape character
									abort = true;
									escapeNextByte = true;
								}
								break;
							case 10:
							case 13:
								newline = true;
								break;
						}
					}
				} 
				catch 
				{
					throw new ArgumentOutOfRangeException();
				}

				if ((!newline) && (!abort))
				{
					bytes++;
					escapeNextByte = false;
				}
			}

			return bytes;
		}

		public int GetBytes(
			byte[] source,
			int sourceIndex,
			int sourceCount,
			byte[] dest,
			int destIndex,
			bool flush
			)
		{
			if (source == null || source == null)
				throw new ArgumentNullException();

			int bytes = 0;
			int newDestIndex = destIndex;
			for(int i=sourceIndex; i<sourceIndex+sourceCount; i++)
			{
				bool escape = false;
				bool newline = false;
				bool abort = false;
				byte b;
				try
				{
					b = source[i];
					if (!escapeNextByte)
					{
						switch (b)
						{
							case escapeByte:
								i++;
								escape = true;
								if (i<sourceIndex+sourceCount)
								{
									b = source[i];
									lineBytes ++;
								} 
								else
								{
									//what a pain, we cannot get the next character now, so 
									//we set a flag to tell us to do it next time
									escapeNextByte = true;
									abort = true;
								}
								break;
							case 10:
							case 13:
								newline = true;
								break;
						}
					}
				} 
				catch 
				{
					throw new ArgumentOutOfRangeException();
				}

				if ((!newline) && (!abort))
				{
					b = DecodeByte(b, escape | escapeNextByte);
					escapeNextByte = false; 

					try
					{
						dest[newDestIndex] = b;
						newDestIndex++;
						bytes++;
					} 
					catch 
					{
						throw new ArgumentException();
					}
				}
			}

			if (flush)
			{
				crc32Hasher.TransformFinalBlock(dest, destIndex, bytes);
				storedHash = crc32Hasher.Hash;
				crc32Hasher = new CRC32();
			}
			else
				crc32Hasher.TransformBlock(dest, destIndex, bytes, dest, destIndex);

			return bytes;
		}

		private byte DecodeByte(byte b, bool escape)
		{
			unchecked
			{
				if (escape)
					b -= death;

				b -= life;
			}

			return b;
		}

		#region ICryptoTransform
		int ICryptoTransform.TransformBlock(
			byte[] inputBuffer,
			int inputOffset,
			int inputCount,
			byte[] outputBuffer,
			int outputOffset
			)
		{
			return GetBytes(inputBuffer, inputOffset, inputCount, outputBuffer, outputOffset, false);
		}

		byte[] ICryptoTransform.TransformFinalBlock(
			byte[] inputBuffer,
			int inputOffset,
			int inputCount
			)
		{
			int count = GetByteCount(inputBuffer, inputOffset, inputCount, true);
			byte[] output = new byte[count];
			GetBytes(inputBuffer, inputOffset, inputCount, output, 0, true);

			return output;
		}

		void IDisposable.Dispose()
		{

		}
		bool ICryptoTransform.CanReuseTransform {get { return true;} }
		bool ICryptoTransform.CanTransformMultipleBlocks {get {return true; } }
		int ICryptoTransform.InputBlockSize {get { return 1; } }
		int ICryptoTransform.OutputBlockSize {get { return 1; } }
		#endregion
	}
}

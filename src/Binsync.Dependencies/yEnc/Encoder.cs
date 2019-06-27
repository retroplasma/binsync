using System;
using System.Text;
using System.Security.Cryptography;

namespace yEnc
{
	/// <summary>
	/// Implementation of yEnc encoder
	/// </summary>
	public class YEncEncoder: 
		ICryptoTransform
	{
		const byte life = 42;		//meaning of life (answer to everything) from hitchhikers guide - delta value used by yEnc
		const byte death = 64;		//
		const byte escapeByte = 61;	//escape byte used by yEnc
		int lineLength = 0;			//length of output line
		int lineBytes = 0;			//number of bytes on the current line
		byte[] additionalEscapeBytes;	//user-requested bytes to escape-out
		byte[] standardEscapeBytes = new byte[] {10,13,0,escapeByte};	//left out the tab, not required from v1.2 of spec
		bool doLineFeedAfterFlush = false;		//whether we should end with a line-feed
		CRC32 crc32Hasher = new CRC32();		//does the work of the CRC32
		byte[] storedHash = null;

		/// <summary>
		/// Default constructor, uses standard v1.2 implementation
		/// </summary>
		public YEncEncoder(): this(128, new byte[] {}, false)
		{
		}

		public YEncEncoder(int lineLength, byte[] escapeThese, bool CRLFAfter)
		{
			this.lineLength = lineLength;
			this.additionalEscapeBytes = escapeThese;
			this.doLineFeedAfterFlush = CRLFAfter;
		}

		/// <summary>
		/// After the encoder has completed, returns the CRC32 of the source data.
		/// </summary>
		public byte[] CRCHash {
			get { return storedHash; }
		}

		/// <summary>
		/// Core encoding algorithm.  Encodes from the source bytes into the destination bytes.
		/// </summary>
		/// <param name="source"></param>
		/// <param name="sourceIndex">the offset in the source from where to start encoding</param>
		/// <param name="sourceCount">the number of bytes to encode</param>
		/// <param name="dest"></param>
		/// <param name="destIndex">the offset in the destination to start writing to</param>
		/// <param name="flush">set to true if this is the last block of data</param>
		/// <returns>the number of bytes output to the destination</returns>
		public int GetBytes(
			byte[] source,
			int sourceIndex,
			int sourceCount,
			byte[] dest,
			int destIndex,
			bool flush
			)
		{
			if (source == null || dest == null)
				throw new ArgumentNullException();

			int byteCount = 0;
			int lineBytes = this.lineBytes;

			for(int i=sourceIndex; i<sourceCount+sourceIndex; i++)
			{
				byte c;
				try
				{
					c = source[i];
				}
				catch
				{
					throw new ArgumentOutOfRangeException();
				}

				bool escape = false;
				byte b = EncodeByte(c, out escape);

				try
				{
					if (escape)
					{
						dest[destIndex] = escapeByte;
						destIndex ++;
						byteCount ++;
						lineBytes ++;
					}

					dest[destIndex] = b;
					destIndex ++;
					lineBytes ++;
					byteCount ++;

					if (lineBytes >= this.lineLength)
					{
						//line termination
						dest[destIndex] = 13;
						destIndex ++;
						byteCount ++;
						dest[destIndex] = 10;
						destIndex ++;
						byteCount ++;
						lineBytes = 0;
					}
				} 
				catch
				{
					throw new ArgumentException();
				}
			}

			if (flush) 
			{
				if (doLineFeedAfterFlush)
				{
					dest[destIndex] = 13;
					destIndex ++;
					byteCount ++;
					dest[destIndex] = 10;
					destIndex ++;
					byteCount ++;
				}

				crc32Hasher.TransformFinalBlock(source, sourceIndex, sourceCount);
				storedHash = crc32Hasher.Hash;

				crc32Hasher = new CRC32();
				this.lineBytes = 0;
			}
			else
			{
				crc32Hasher.TransformBlock(source, sourceIndex, sourceCount, source, sourceIndex);
				this.lineBytes = lineBytes;
			}

			return byteCount;
		}

		/// <summary>
		/// Encodes a single byte.
		/// </summary>
		/// <param name="b"></param>
		/// <param name="escape">returns true if the returned byte needs to be escaped</param>
		/// <returns>the encoded byte</returns>
		private byte EncodeByte(byte b, out bool escape)
		{
			unchecked
			{
				b += life;

				escape = false;
				for (int i=0; i<standardEscapeBytes.Length; i++)
				{
					if (b == standardEscapeBytes[i])
					{
						escape = true;
						b += death;
						break;
					}
				}

				if (!escape)
				{
					for (int i=0; i<additionalEscapeBytes.Length; i++)
					{
						if (b == additionalEscapeBytes[i])
						{
							b += death;
							escape = true;
							break;
						}
					}
				}
			}

			return b;
		}

		/// <summary>
		/// Returns the number of bytes that will be encoded if GetBytes is called
		/// with the same parameters.  Does not alter the state of the encoder.
		/// </summary>
		/// <param name="bytes"></param>
		/// <param name="index"></param>
		/// <param name="count"></param>
		/// <param name="flush"></param>
		/// <returns></returns>
		public int GetByteCount(
			byte[] bytes,
			int index,
			int count,
			bool flush
			)
		{
			if (bytes == null)
				throw new ArgumentNullException();

			int byteCount = 0;
			int lineBytes = this.lineBytes;

			for(int i=index; i<count+index; i++)
			{
				byte c;
				try
				{
					c = bytes[i];
				}
				catch
				{
					throw new ArgumentOutOfRangeException();
				}

				bool escape = false;
				//byte b = EncodeByte(c, out escape);
				EncodeByte(c, out escape);

				try
				{
					if (escape)
					{
						byteCount ++;
						lineBytes ++;
					}

					lineBytes ++;
					byteCount ++;

					if (lineBytes >= this.lineLength)
					{
						//line termination
						byteCount ++;
						byteCount ++;
						lineBytes = 0;
					}
				} 
				catch
				{
					throw new ArgumentException();
				}
			}

			if ((flush) && (doLineFeedAfterFlush))
			{
				byteCount ++;
				byteCount ++;
			}

			return byteCount;
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
		int ICryptoTransform.OutputBlockSize {get { return 3; } }
		#endregion
	}
}

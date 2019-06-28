using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Threading;

namespace Binsync.Core.Helpers
{
    static class StreamParity
    {
		const int maxBufferSize = 1024*32;

		/// <summary>
		/// Repair the broken streams specified in the array, needs originalParityCount and reparationIndizes.
		/// </summary>
		/// <param name="streams">Streams with equal sizes.</param>
		/// <param name="originalParityCount">Original parity count.</param>
		/// <param name="reparationIndizes">Reparation indizes.</param>
        public static void Repair (Stream[] streams, int originalParityCount, byte[] reparationIndizes)
        {

			int allCount = streams.Length;
			int parityCount = originalParityCount;
            ECC.ReedSolomon rs = new ECC.ReedSolomon(parityCount);
			rs.CacheQ (allCount, reparationIndizes);

			long streamLength = streams[0].Length;

			Parallel.For(0, (int)Math.Ceiling((double)streamLength/(double)maxBufferSize), iii =>
            {
				int bufferSize = maxBufferSize;

				if (streamLength - iii * maxBufferSize < bufferSize){
					bufferSize = (int)(streamLength - iii * maxBufferSize);
				}


                byte[][] buffers = new byte[streams.Length][];


                for (int i = 0; i < streams.Length; i++)
                {
                    buffers[i] = new byte[bufferSize];

					lock(streams[i]){
						streams[i].Position = iii * maxBufferSize;
	                    streams[i].Read(buffers[i], 0, buffers[i].Length);
					}
                }

				#region COMMENTED faster?
				/*
				for (int i = 0; i < streams.Length; i++)
                {
                    buffers[i] = new byte[bufferSize];

					//bool doIt = true;
					for(int j = 0; j<reparationIndizes.Length; j++){
						if(reparationIndizes[j] == i)
						{
							goto supercontinue;
							//doIt = false;
							//break;
						}
					}
					//if(!doIt){
					//	continue;
					//}
					lock(streams[i]){
						streams[i].Position = iii * maxBufferSize;
	                    streams[i].Read(buffers[i], 0, buffers[i].Length);
					}
				supercontinue:;

                }
                */
				#endregion

                byte[][] outbuf = new byte[bufferSize][];
                for (int i = 0; i < bufferSize; i++)
                {
                    byte[] msg = new byte[allCount];

					for(int j = 0; j < allCount; j++)
					{
						msg[j] = buffers[j][i];
					}

                    rs.Decode(msg, reparationIndizes,true);
					outbuf[i] = msg;

                }

                byte[][] outbuf2 = new byte[allCount][];
                for (int i = 0; i < allCount; i++)
                {
                    outbuf2[i] = new byte[bufferSize];
                }

                for (int i = 0; i < bufferSize; i++)
                {
					for (int j = 0; j < allCount; j++)
                    {
                        outbuf2[j][i] = outbuf[i][j];
                    }
                }

				for (int r = 0; r < reparationIndizes.Length; r++)
                {
					int i = reparationIndizes[r];

					lock(streams[i]){
						streams[i].Position = iii * maxBufferSize;
						streams[i].Write(outbuf2[i], 0, outbuf2[i].Length);
					}
                }
            }
			);
        }

		/// <summary>
		/// Generates parity streams from input streams. All streams should have the same length.
		/// </summary>
		/// <param name="iStreams">Input streams</param>
		/// <param name="oStreams">Output streams</param>
		public static void GenerateStreams(Stream[] iStreams, Stream[] oStreams)
		{
			int messageCount = iStreams.Length;
			int parityCount = oStreams.Length;
			
			var rs = new ECC.ReedSolomon(parityCount);

			long streamLength = iStreams[0].Length;

			Parallel.For(0, (int)Math.Ceiling((double)streamLength/(double)maxBufferSize), iii =>
			{
				int bufferSize = maxBufferSize;

				if (streamLength - iii * maxBufferSize < bufferSize){
					bufferSize = (int)(streamLength - iii * maxBufferSize);
				}

				byte[][] buffers = new byte[iStreams.Length][];
				for (int i = 0; i < iStreams.Length; i++)
				{
					buffers[i] = new byte[bufferSize];

					lock(iStreams[i]){
						iStreams[i].Position = iii * maxBufferSize;
						iStreams[i].Read(buffers[i], 0, buffers[i].Length);
					}
				}

				byte[][] outbuf = new byte[bufferSize][];
				for (int i = 0; i < bufferSize; i++)
				{
					byte[] msg = new byte[messageCount];

					for (int j = 0; j < messageCount; j++)
					{
						msg[j] = buffers[j][i];
					}
					outbuf[i] = rs.Encode(msg);
				}

				byte[][] outbuf2 = new byte[parityCount][];
				for (int i = 0; i < parityCount; i++)
				{
					outbuf2[i] = new byte[bufferSize];
				}

				for (int i = 0; i < bufferSize; i++)
				{
					for (int j = 0; j < parityCount; j++)
					{
						outbuf2[j][i] = outbuf[i][messageCount + j];
					}
				}

				for (int i = 0; i < parityCount; i++)
				{
					lock(oStreams[i]){
						oStreams[i].Position = iii * maxBufferSize;
						oStreams[i].Write(outbuf2[i], 0, outbuf2[i].Length);
					}
				}
			});
		}
	}
}

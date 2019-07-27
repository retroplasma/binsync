using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Binsync.Core;
using Binsync.Core.Formats;
using Binsync.Core.Helpers;

namespace Binsync.Util
{
	// Write-only stream that pushes meta when closed without prior cancellation or exceptions.
	public class BinsyncStream2 : Stream
	{
		// TODO: concurrent uploads
		// TODO: get rid of "/" prefixing

		Engine engine;
		string path;
		long length;
		CancellationToken cancellationToken;

		public BinsyncStream2(Engine engine, string path, CancellationToken cancellationToken)
		{
			this.engine = engine;
			this.path = path;
			this.cancellationToken = cancellationToken;
		}

		public override long Position { get; set; } = 0;

		public override long Length => length;
		public override bool CanWrite => true;
		public override bool CanSeek => false;
		public override bool CanRead => false;

		SemaphoreSlim _writeSem = new SemaphoreSlim(1, 1);

		List<MetaSegment.Command.FileOrigin> metaSegments = new List<MetaSegment.Command.FileOrigin>();

		int tmpSize = 0;
		byte[] tmpBuf = new byte[Binsync.Core.Constants.SegmentSize];
		bool faulted = false;

		public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			await _writeSem.WaitAsync().ConfigureAwait(false);
			try
			{
				var written = 0;
				do
				{
					cancellationToken.ThrowIfCancellationRequested();
					var newCount = 65536;
					if (written + newCount > count)
					{
						newCount = count - written;
					}
					await _writeAsync(buffer, offset + written, newCount, cancellationToken).ConfigureAwait(false);
					written += newCount;
				} while (written < count);
			}
			finally
			{
				_writeSem.Release();
			}
		}

		public async Task _writeAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			Console.WriteLine("writing to buffer with O = {0}, C = {1}", offset, count);

			if (tmpSize + count < Binsync.Core.Constants.SegmentSize)
			{
				Array.Copy(buffer, offset, tmpBuf, tmpSize, count);
				tmpSize += count;
			}
			else
			{
				var left = Binsync.Core.Constants.SegmentSize - tmpSize;
				// count >= left
				Console.WriteLine("left: {0}, count: {1}", left, count);
				Array.Copy(buffer, offset, tmpBuf, tmpSize, left);
				tmpSize += left;
				if (tmpSize != Binsync.Core.Constants.SegmentSize) throw new ArithmeticException();

				Console.WriteLine("uploading chunk ({0} bytes)", tmpSize);
				var bytes = new byte[tmpSize];
				Array.Copy(tmpBuf, bytes, tmpSize);
				var hash = bytes.SHA256();
				metaSegments.Add(new MetaSegment.Command.FileOrigin { Hash = hash, Start = length, Size = (uint)tmpSize });
				length += tmpSize;
				try
				{
					await engine.UploadFileChunk(bytes, hash).ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					faulted = true;
					throw ex;
				}

				Array.Copy(buffer, offset + left, tmpBuf, 0, count - left);
				tmpSize = count - left;
			}
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			var task = Task.Run(() =>
			{
				return WriteAsync(buffer, offset, count, cancellationToken);
			});
			task.Wait();
			if (task.IsFaulted) throw task.Exception;
			return;
		}

		public override void Close()
		{
			var task = Task.Run(() =>
			{
				return closeAsync(cancellationToken);
			});
			task.Wait();
			if (task.IsFaulted) throw task.Exception;
			base.Close();
		}

		async Task closeAsync(CancellationToken cancellationToken)
		{
			await _writeSem.WaitAsync().ConfigureAwait(false);
			try
			{
				if (cancellationToken.IsCancellationRequested || faulted)
				{
					Console.WriteLine("is cancelling or faulted. don't write residual data or meta");
					return;
				}

				if (tmpSize > 0)
				{
					Console.WriteLine("writing residual data ({0} bytes)", tmpSize);
					var bytes = new byte[tmpSize];
					Array.Copy(tmpBuf, bytes, tmpSize);
					var hash = bytes.SHA256();
					metaSegments.Add(new MetaSegment.Command.FileOrigin { Hash = hash, Start = length, Size = (uint)tmpSize });
					length += tmpSize;
					await engine.UploadFileChunk(bytes, hash);
				}

				Console.WriteLine("writing meta");
				await engine.PushFileToMeta(metaSegments, length, "/" + path);
				Console.WriteLine("done");
			}
			finally
			{
				_writeSem.Release();
			}
		}

		public override Task<int> ReadAsync(byte[] buffer, int _offset, int count, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			throw new NotImplementedException();
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			throw new NotImplementedException();
		}

		public override void Flush()
		{
			throw new NotImplementedException();
		}

		public override void SetLength(long value)
		{
			throw new NotImplementedException();
		}
	}
}
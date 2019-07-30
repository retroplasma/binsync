using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Binsync.Core;
using Binsync.Core.Formats;

namespace Binsync.Util
{
	public class BinsyncStream : Stream
	{
		Engine engine;
		string path;
		long length;
		CancellationToken cancellationToken;

		public BinsyncStream(Engine engine, string path, long length, CancellationToken cancellationToken)
		{
			this.engine = engine;
			this.path = path;
			this.length = length;
			this.cancellationToken = cancellationToken;
		}

		public override long Position { get; set; } = 0;

		public override long Length => length;
		public override bool CanWrite => false;
		public override bool CanSeek => true;
		public override bool CanRead => true;

		SemaphoreSlim _readSem = new SemaphoreSlim(1, 1);

		Engine.Meta cachedMeta;

		public override async Task<int> ReadAsync(byte[] buffer, int _offset, int count, CancellationToken cancellationToken)
		{
			if (length == 0)
				return 0;

			var pos = Position;
			await _readSem.WaitAsync().ConfigureAwait(false);
			try
			{
				var seg = cachedMeta ?? await engine.DownloadMetaForPath(path).ConfigureAwait(false);
				if (seg == null)
					throw new FileNotFoundException();
				cachedMeta = seg;

				Int64 len = Length;
				long size = count;
				long offset = _offset + pos;

				Console.WriteLine("offset: {0}, count: {1}, path: {2}", offset, count, path);

				if (offset < len)
				{
					if (offset + size > len)
						size = len - offset;

					var read = (long)0;

					int extraBlockCounter = 4;

					// MAYBE: make concurrent and cap concurrency? or look ahead                    
					foreach (var c in seg.Commands
						.Where(c => c.CMD == MetaSegment.Command.CMDV.ADD)
						.Where(c => c.TYPE == MetaSegment.Command.TYPEV.BLOCK)
						.Select(c => c.FILE_ORIGIN)
						.SkipWhile(c => c.Start + c.Size <= offset)
					)
					{
						if (read >= size || c.Start >= offset + size)
						{
							if (extraBlockCounter == 0)
								break;

							extraBlockCounter--;
							_ = engine.DownloadChunk(engine.Generator.GenerateRawOrParityID(c.Hash));
							continue;
						}
						var mspos1 = read;
						var offset1 = (int)Math.Max(0, (offset + read) - c.Start);
						var size1 = (int)Math.Min(c.Size - offset1, size - read);

						if (size1 < 0)
						{
							throw new Exception("size1 < 0");
						}
						read += size1;

						var data = await engine.DownloadChunk(engine.Generator.GenerateRawOrParityID(c.Hash)).ConfigureAwait(false);
						Array.Copy(data, offset1, buffer, mspos1, size1);
					}

					Position += read;
					return (int)read;
				}
				else
				{
					return 0;
				}
			}
			finally
			{
				_readSem.Release();
			}
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			_readSem.Wait();
			try
			{
				long pos = Position;
				switch (origin)
				{
					case SeekOrigin.Begin:
						pos = offset;
						break;
					case SeekOrigin.Current:
						pos += offset;
						break;
					case SeekOrigin.End:
						pos = Length + offset;
						break;
				}
				if (pos < 0 || pos > Length)
					throw new IOException($"Invalid position {pos} in stream with length {Length}");
				Position = pos;
				return Position;
			}
			finally
			{
				_readSem.Release();
			}
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			var task = Task.Run(() =>
			{
				return ReadAsync(buffer, offset, count, cancellationToken);
			});
			var result = task.Result;
			task.Wait();
			if (task.IsFaulted) throw task.Exception;
			return task.Result;
		}

		public override void Flush()
		{
			throw new NotImplementedException();
		}

		public override void SetLength(long value)
		{
			throw new NotImplementedException();
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			throw new NotImplementedException();
		}
	}
}
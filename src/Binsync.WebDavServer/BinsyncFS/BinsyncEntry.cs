using System;
using System.Threading;
using System.Threading.Tasks;

using FubarDev.WebDavServer;
using FubarDev.WebDavServer.Model.Headers;
using FubarDev.WebDavServer.FileSystem;

using JetBrains.Annotations;

namespace Binsync.WebDavServer
{
	/// <summary>
	/// Am in-memory implementation of a WebDAV entry (collection or document)
	/// </summary>
	public abstract class BinsyncEntry : IEntry, IEntityTagEntry
	{
		private readonly ICollection _parent;

		/// <summary>
		/// Initializes a new instance of the <see cref="BinsyncEntry"/> class.
		/// </summary>
		/// <param name="fileSystem">The file system this entry belongs to</param>
		/// <param name="parent">The parent collection</param>
		/// <param name="path">The root-relative path of this entry</param>
		/// <param name="name">The name of the entry</param>
		protected BinsyncEntry(BinsyncFileSystem fileSystem, ICollection parent, Uri path, string name)
		{
			_parent = parent;
			Name = name;
			FileSystem = BinsyncFS = fileSystem;
			Path = path;
			CreationTimeUtc = LastWriteTimeUtc = new DateTime(1970, 1, 1);//DateTime.UtcNow;
		}

		/// <inheritdoc />
		public string Name { get; }

		/// <inheritdoc />
		public IFileSystem FileSystem { get; }

		/// <inheritdoc />
		public ICollection Parent => _parent;

		/// <inheritdoc />
		public Uri Path { get; }

		/// <inheritdoc />
		public DateTime LastWriteTimeUtc { get; protected set; }

		/// <inheritdoc />
		public DateTime CreationTimeUtc { get; protected set; }

		/// <inheritdoc />
		public EntityTag ETag { get; protected set; } = new EntityTag(false);

		/// <summary>
		/// Gets the file system
		/// </summary>
		protected BinsyncFileSystem BinsyncFS { get; }

		/// <summary>
		/// Gets the parent collection
		/// </summary>
		protected BinsyncDirectory BinsyncParent => _parent as BinsyncDirectory;

		/// <inheritdoc />
		public Task<EntityTag> UpdateETagAsync(CancellationToken cancellationToken)
		{
			if (BinsyncFS.IsReadOnly)
				throw new UnauthorizedAccessException("Failed to modify a read-only file system");

			return Task.FromResult(ETag = new EntityTag(false));
		}

		/// <inheritdoc />
		public abstract Task<DeleteResult> DeleteAsync(CancellationToken cancellationToken);

		/// <inheritdoc />
		public Task SetLastWriteTimeUtcAsync(DateTime lastWriteTime, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
			/*
			if (BinsyncFS.IsReadOnly)
				throw new UnauthorizedAccessException("Failed to modify a read-only file system");

			LastWriteTimeUtc = lastWriteTime;
			return Task.FromResult(0);
			*/
		}

		/// <inheritdoc />
		public Task SetCreationTimeUtcAsync(DateTime creationTime, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
			/*
			if (BinsyncFS.IsReadOnly)
				throw new UnauthorizedAccessException("Failed to modify a read-only file system");

			CreationTimeUtc = creationTime;
			return Task.FromResult(0);
			*/
		}
	}
}

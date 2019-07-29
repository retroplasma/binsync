using System;
using System.Collections.Generic;
using System.Security.Principal;

using FubarDev.WebDavServer;
using FubarDev.WebDavServer.Locking;
using FubarDev.WebDavServer.Props.Store;
using FubarDev.WebDavServer.Utils;
using FubarDev.WebDavServer.FileSystem;

using JetBrains.Annotations;

namespace Binsync.WebDavServer
{
	/// <summary>
	/// An in-memory implementation of the <see cref="IFileSystemFactory"/>
	/// </summary>
	public class BinsyncFileSystemFactory : IFileSystemFactory
	{
		private readonly Dictionary<FileSystemKey, BinsyncFileSystem> _fileSystems = new Dictionary<FileSystemKey, BinsyncFileSystem>();

		private readonly IPathTraversalEngine _pathTraversalEngine;

		private readonly ISystemClock _systemClock;

		private readonly ILockManager _lockManager;

		private readonly IPropertyStoreFactory _propertyStoreFactory;

		/// <summary>
		/// Initializes a new instance of the <see cref="BinsyncFileSystemFactory"/> class.
		/// </summary>
		/// <param name="pathTraversalEngine">The engine to traverse paths</param>
		/// <param name="systemClock">Interface for the access to the systems clock</param>
		/// <param name="lockManager">The global lock manager</param>
		/// <param name="propertyStoreFactory">The store for dead properties</param>
		public BinsyncFileSystemFactory(
			 IPathTraversalEngine pathTraversalEngine,
			 ISystemClock systemClock,
			ILockManager lockManager = null,
			IPropertyStoreFactory propertyStoreFactory = null)
		{
			_pathTraversalEngine = pathTraversalEngine;
			_systemClock = systemClock;
			_lockManager = lockManager;
			_propertyStoreFactory = propertyStoreFactory;
		}

		/// <inheritdoc />
		public virtual IFileSystem CreateFileSystem(ICollection mountPoint, IPrincipal principal)
		{
			var userName = !principal.Identity.IsAnonymous()
				? principal.Identity.Name
				: string.Empty;

			var key = new FileSystemKey(userName, mountPoint?.Path.OriginalString ?? string.Empty);
			BinsyncFileSystem fileSystem;
			if (!_fileSystems.TryGetValue(key, out fileSystem))
			{
				fileSystem = new BinsyncFileSystem(mountPoint, _pathTraversalEngine, _systemClock, _lockManager, _propertyStoreFactory);
				_fileSystems.Add(key, fileSystem);
				InitializeFileSystem(mountPoint, principal, fileSystem);
			}
			else
			{
				UpdateFileSystem(mountPoint, principal, fileSystem);
			}

			return fileSystem;
		}

		/// <summary>
		/// Called when file system will be initialized
		/// </summary>
		/// <param name="mountPoint">The mount point</param>
		/// <param name="principal">The principal the file system was created for</param>
		/// <param name="fileSystem">The created file system</param>
		protected virtual void InitializeFileSystem(ICollection mountPoint, IPrincipal principal, BinsyncFileSystem fileSystem)
		{
		}

		/// <summary>
		/// Called when the file system will be updated
		/// </summary>
		/// <param name="mountPoint">The mount point</param>
		/// <param name="principal">The principal the file system was created for</param>
		/// <param name="fileSystem">The created file system</param>
		protected virtual void UpdateFileSystem(ICollection mountPoint, IPrincipal principal, BinsyncFileSystem fileSystem)
		{
		}

		private class FileSystemKey : IEquatable<FileSystemKey>
		{
			private static readonly IEqualityComparer<string> _comparer = StringComparer.OrdinalIgnoreCase;

			private readonly string _userName;

			private readonly string _mountPoint;

			public FileSystemKey(string userName, string mountPoint)
			{
				_userName = userName;
				_mountPoint = mountPoint;
			}

			public bool Equals(FileSystemKey other)
			{
				if (ReferenceEquals(null, other))
					return false;
				if (ReferenceEquals(this, other))
					return true;
				return _comparer.Equals(_userName, other._userName) && _comparer.Equals(_mountPoint, other._mountPoint);
			}

			public override bool Equals(object obj)
			{
				if (ReferenceEquals(null, obj))
					return false;
				if (ReferenceEquals(this, obj))
					return true;
				if (obj.GetType() != GetType())
					return false;
				return Equals((FileSystemKey)obj);
			}

			public override int GetHashCode()
			{
				unchecked
				{
					return ((_userName != null ? _comparer.GetHashCode(_userName) : 0) * 397) ^ (_mountPoint != null ? _comparer.GetHashCode(_mountPoint) : 0);
				}
			}
		}
	}
}

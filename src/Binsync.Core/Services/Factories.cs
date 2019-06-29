using System;
using Binsync.Core.Services;

namespace Binsync.Core.Services
{
	public interface IServiceFactory
	{
		int MaxConnections { get; }
		IService Give();
	}

	class TestFactory : IServiceFactory
	{
		public int MaxConnections { get { return 3; } }

		readonly string storagePath;
		public TestFactory(string storagePath)
		{
			this.storagePath = storagePath;
		}

		public IService Give()
		{
			return new TestDummy(storagePath);
		}
	}

	public class SimpleServiceFactory : IServiceFactory
	{
		readonly Type serviceType = typeof(Usenet);

		public int MaxConnections { get { return 3; } }

		public IService Give()
		{
			return (IService)Activator.CreateInstance(serviceType);
		}
	}

	public class DelegateFactory<T> : IServiceFactory where T : IService
	{
		readonly Func<T> svcGen;

		int maxConnections;
		public int MaxConnections { get { return maxConnections; } }

		public DelegateFactory(int maxConnections, Func<T> svcGen)
		{
			this.maxConnections = maxConnections;
			this.svcGen = svcGen;
		}

		public IService Give()
		{
			return svcGen();
		}
	}
}


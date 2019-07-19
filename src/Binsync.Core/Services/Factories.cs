using System;
using Binsync.Core.Services;

namespace Binsync.Core.Services
{
	public interface IServiceFactory
	{
		IService Give();
	}

	class TestFactory : IServiceFactory
	{
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

		public IService Give()
		{
			return (IService)Activator.CreateInstance(serviceType);
		}
	}

	public class DelegateFactory<T> : IServiceFactory where T : IService
	{
		readonly Func<T> svcGen;

		public DelegateFactory(Func<T> svcGen)
		{
			this.svcGen = svcGen;
		}

		public IService Give()
		{
			return svcGen();
		}
	}
}


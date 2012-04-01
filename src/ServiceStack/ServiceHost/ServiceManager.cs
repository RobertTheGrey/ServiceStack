﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ServiceStack.Configuration;
using ServiceStack.Logging;
using Funq;
using ServiceStack.Text;

namespace ServiceStack.ServiceHost
{
	public class ServiceManager
		: IDisposable
	{
		private static readonly ILog Log = LogManager.GetLogger(typeof(ServiceManager));

		public Container Container { get; private set; }
		public IServiceController ServiceController { get; private set; }

		public ServiceOperations ServiceOperations { get; set; }
		public ServiceOperations AllServiceOperations { get; set; }

		private ContainerResolveCache typeFactory;

		public ServiceManager()
		{
			this.Container = new Container();
			this.ServiceController = new ServiceController();
			this.typeFactory = new ContainerResolveCache(this.Container);
		}

		public ServiceManager(params Assembly[] assembliesWithServices)
			: this()
		{
			if (assembliesWithServices == null || assembliesWithServices.Length == 0)
				throw new ArgumentException(
					"No Assemblies provided in your AppHost's base constructor.\n"
					+ "To register your services, please provide the assemblies where your web services are defined.");

			var serviceTypes = this.GetAssemblyTypes(assembliesWithServices);
			this.ServiceController.RegisterServices(typeFactory, serviceTypes);
			this.Container.RegisterAutoWiredTypes(serviceTypes);

			ReloadServiceOperations();
		}

		/// <summary>
		/// Inject alternative container and strategy for resolving Service Types
		/// </summary>
		public ServiceManager(Container container, IServiceController serviceController)
		{
			if (serviceController == null)
				throw new ArgumentNullException("serviceController");

			this.Container = container ?? new Container();
			this.ServiceController = serviceController;
			this.typeFactory = new ContainerResolveCache(this.Container);
		}

		private List<Type> GetAssemblyTypes(Assembly[] assembliesWithServices)
		{
			var results = new List<Type>();
			string assemblyName = null;
			string typeName = null;

			try
			{
				foreach (var assembly in assembliesWithServices)
				{
					assemblyName = assembly.FullName;
					foreach (var type in assembly.GetTypes())
					{
						typeName = type.Name;
						results.Add(type);
					}
				}
				return results;
			}
			catch (Exception ex)
			{
				var msg = string.Format("Failed loading types, last assembly '{0}', type: '{1}'", assemblyName, typeName);
				Log.Error(msg, ex);
				throw new Exception(msg, ex);
			}
		}

		public void ReloadServiceOperations()
		{
			this.ServiceOperations = new ServiceOperations(this.ServiceController.OperationTypes);
			this.AllServiceOperations = new ServiceOperations(this.ServiceController.AllOperationTypes);
		}

		public void RegisterService<T>()
		{
			if (!typeof(T).IsGenericType
				|| typeof(T).GetGenericTypeDefinition() != typeof(IService<>))
				throw new ArgumentException("Type {0} is not a Web Service that inherits IService<>".Fmt(typeof(T).FullName));

			this.ServiceController.RegisterService(typeFactory, typeof(T));
			this.Container.RegisterAutoWired<T>();
		}

		public Type RegisterService(Type serviceType)
		{
			var genericServiceType = serviceType.GetTypeWithGenericTypeDefinitionOf(typeof(IService<>));
			if (genericServiceType == null)
				throw new ArgumentException("Type {0} is not a Web Service that inherits IService<>".Fmt(serviceType.FullName));

			try
			{
				this.ServiceController.RegisterService(typeFactory, serviceType);
				this.Container.RegisterAutoWiredType(serviceType);
			}
			catch (Exception ex)
			{
				Log.Error(ex);
			}			
			return genericServiceType;
		}

		public object Execute(object dto)
		{
			return this.ServiceController.Execute(dto, null);
		}

		public void Dispose()
		{
			if (this.Container != null)
			{
				this.Container.Dispose();
			}
		}
	}

}

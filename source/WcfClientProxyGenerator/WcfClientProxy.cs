﻿using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.ServiceModel;
using WcfClientProxyGenerator.Async;
using WcfClientProxyGenerator.Util;

namespace WcfClientProxyGenerator
{
    /// <summary>
    /// Factory class for generating WCF clients
    /// </summary>
    public static class WcfClientProxy
    {
        private static readonly ConcurrentDictionary<Type, Lazy<Type>> ProxyCache 
            = new ConcurrentDictionary<Type, Lazy<Type>>();

        /// <summary>
        /// Creates a proxy instance of <typeparamref name="TServiceInterface"/> that
        /// is used to initiate calls to a WCF service.
        /// </summary>
        /// <typeparam name="TServiceInterface">Interface of the WCF service</typeparam>
        /// <returns>The WCF client proxy</returns>
        public static TServiceInterface Create<TServiceInterface>()
            where TServiceInterface : class
        {
            return Create<TServiceInterface>((Action<IRetryingProxyConfigurator>)null);
        }

        /// <summary>
        /// Creates a proxy instance of <typeparamref name="TServiceInterface"/> that
        /// is used to initiate calls to a WCF service.
        /// </summary>
        /// <typeparam name="TServiceInterface">Interface of the WCF service</typeparam>
        /// <param name="endpointConfigurationName">Name of the WCF service configuration</param>
        /// <returns>The WCF client proxy</returns>
        public static TServiceInterface Create<TServiceInterface>(string endpointConfigurationName)
            where TServiceInterface : class
        {
            return Create<TServiceInterface>(c => c.SetEndpoint(endpointConfigurationName));
        }

        /// <summary>
        /// Creates a proxy instance of <typeparamref name="TServiceInterface"/> that
        /// is used to initiate calls to a WCF service.
        /// </summary>
        /// <typeparam name="TServiceInterface">Interface of the WCF service</typeparam>
        /// <param name="configurator">Lambda that defines how the proxy is configured</param>
        /// <returns>The WCF client proxy</returns>
        public static TServiceInterface Create<TServiceInterface>(Action<IRetryingProxyConfigurator> configurator)
            where TServiceInterface : class
        {
            var proxy = CreateProxy<TServiceInterface>();

            if (configurator != null)
            {
                configurator(proxy as IRetryingProxyConfigurator);
            }
            else
            {
                DefaultProxyConfigurator.Configure<TServiceInterface>(proxy as IRetryingProxyConfigurator);
            }

            return proxy;            
        }

        #region Async Proxy

        /// <summary>
        /// Creates an async-friendly wrapped proxy instance of <typeparamref name="TServiceInterface"/> that
        /// is used to initiate calls to a WCF service. 
        /// <para>
        /// Synchronous calls can still be made via the <see cref="IAsyncProxy{TServiceInterface}.Client"/>
        /// property.
        /// </para>
        /// </summary>
        /// <typeparam name="TServiceInterface">Interface of the WCF service</typeparam>
        /// <returns>Async friendly wrapper around <typeparamref name="TServiceInterface"/></returns>
        public static IAsyncProxy<TServiceInterface> CreateAsync<TServiceInterface>()
            where TServiceInterface : class
        {
            var proxy = Create<TServiceInterface>();
            return new AsyncProxy<TServiceInterface>(proxy);
        }

        /// <summary>
        /// Creates an async-friendly wrapped proxy instance of <typeparamref name="TServiceInterface"/> that
        /// is used to initiate calls to a WCF service. 
        /// <para>
        /// Synchronous calls can still be made via the <see cref="IAsyncProxy{TServiceInterface}.Client"/>
        /// property.
        /// </para>
        /// </summary>
        /// <typeparam name="TServiceInterface">Interface of the WCF service</typeparam>
        /// <param name="endpointConfigurationName">Name of the WCF service configuration</param>
        /// <returns>Async friendly wrapper around <typeparamref name="TServiceInterface"/></returns>
        public static IAsyncProxy<TServiceInterface> CreateAsync<TServiceInterface>(string endpointConfigurationName)
            where TServiceInterface : class
        {
            var proxy = Create<TServiceInterface>(endpointConfigurationName);
            return new AsyncProxy<TServiceInterface>(proxy);
        }

        /// <summary>
        /// Creates an async-friendly wrapped proxy instance of <typeparamref name="TServiceInterface"/> that
        /// is used to initiate calls to a WCF service. 
        /// <para>
        /// Synchronous calls can still be made via the <see cref="IAsyncProxy{TServiceInterface}.Client"/>
        /// property.
        /// </para>
        /// </summary>
        /// <typeparam name="TServiceInterface">Interface of the WCF service</typeparam>
        /// <param name="configurator">Lambda that defines how the proxy is configured</param>
        /// <returns>Async friendly wrapper around <typeparamref name="TServiceInterface"/></returns>
        public static IAsyncProxy<TServiceInterface> CreateAsync<TServiceInterface>(
            Action<IRetryingProxyConfigurator> configurator)
            where TServiceInterface : class
        {
            var proxy = Create<TServiceInterface>(configurator);

            var interfaces = proxy.GetType().GetInterfaces();

//            var dynamicAsyncType = Type.GetType(
//                "WcfClientProxyGenerator.DynamicProxy." + typeof(TServiceInterface).Name + "Async, WcfClientProxyGenerator.DynamicProxy");

            var dynamicAsyncType = interfaces.Single(m => m.FullName == "WcfClientProxyGenerator.DynamicProxy." + typeof(TServiceInterface).Name + "Async");

            var asyncProxyType = typeof(AsyncProxy<>)
                .MakeGenericType(typeof(TServiceInterface));

            var ctors = asyncProxyType.GetConstructor(new [] { dynamicAsyncType });

            var asyncProxy = Activator.CreateInstance(asyncProxyType, new object[] { proxy });

            var cast = asyncProxy as IAsyncProxy<TServiceInterface>;
            return cast;
            //return new AsyncProxy<TServiceInterface>(proxy);
        }

        #endregion


        private static TServiceInterface CreateProxy<TServiceInterface>(params object[] arguments)
            where TServiceInterface : class
        {
            var proxyType = GetProxyType<TServiceInterface>();
            return (TServiceInterface) FastActivator.CreateInstance(proxyType);
        }

        private static Type GetProxyType<TServiceInterface>()
            where TServiceInterface : class
        {
            return ProxyCache.GetOrAddSafe(
                typeof(TServiceInterface), 
                _ => DynamicProxyTypeGenerator<TServiceInterface>.GenerateType(typeof(RetryingWcfActionInvokerProvider<>)));
        }  
    }
}

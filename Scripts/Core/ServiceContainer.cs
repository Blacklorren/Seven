using System;
using System.Collections.Generic;
using UnityEngine;

namespace HandballManager.Core
{
    /// <summary>
    /// Interface for a service container that provides dependency injection capabilities.
    /// </summary>
    public interface IServiceContainer
    {
        /// <summary>
        /// Binds an interface to a concrete implementation.
        /// </summary>
        /// <typeparam name="TInterface">The interface type.</typeparam>
        /// <typeparam name="TImplementation">The implementation type.</typeparam>
        void Bind<TInterface, TImplementation>() where TImplementation : TInterface, new();
        
        /// <summary>
        /// Binds a MonoBehaviour type that will be created on demand.
        /// </summary>
        /// <typeparam name="T">The MonoBehaviour type.</typeparam>
        void BindMonoBehaviour<T>() where T : MonoBehaviour;
        
        /// <summary>
        /// Binds an instance to a type.
        /// </summary>
        /// <typeparam name="T">The type to bind to.</typeparam>
        /// <param name="instance">The instance to bind.</param>
        void BindInstance<T>(T instance);
        
        /// <summary>
        /// Gets a service of the specified type.
        /// </summary>
        /// <typeparam name="T">The type of service to get.</typeparam>
        /// <returns>The service instance.</returns>
        T Get<T>();
    }
    
    /// <summary>
    /// Implementation of the service container interface.
    /// </summary>
    public class ServiceContainer : IServiceContainer
    {
        private readonly Dictionary<Type, Func<object>> _bindings = new Dictionary<Type, Func<object>>();
        private readonly Dictionary<Type, object> _instances = new Dictionary<Type, object>();
        
        /// <summary>
        /// Binds an interface to a concrete implementation.
        /// </summary>
        /// <typeparam name="TInterface">The interface type.</typeparam>
        /// <typeparam name="TImplementation">The implementation type.</typeparam>
        public void Bind<TInterface, TImplementation>() where TImplementation : TInterface, new()
        {
            _bindings[typeof(TInterface)] = () => new TImplementation();
        }
        
        /// <summary>
        /// Binds a MonoBehaviour type that will be created on demand.
        /// </summary>
        /// <typeparam name="T">The MonoBehaviour type.</typeparam>
        public void BindMonoBehaviour<T>() where T : MonoBehaviour
        {
            _bindings[typeof(T)] = () =>
            {
                var gameObject = new GameObject(typeof(T).Name);
                return gameObject.AddComponent<T>();
            };
        }
        
        /// <summary>
        /// Binds an instance to a type.
        /// </summary>
        /// <typeparam name="T">The type to bind to.</typeparam>
        /// <param name="instance">The instance to bind.</param>
        public void BindInstance<T>(T instance)
        {
            _instances[typeof(T)] = instance;
        }
        
        /// <summary>
        /// Gets a service of the specified type.
        /// </summary>
        /// <typeparam name="T">The type of service to get.</typeparam>
        /// <returns>The service instance.</returns>
        public T Get<T>()
        {
            Type type = typeof(T);
            
            // Check if we already have an instance
            if (_instances.TryGetValue(type, out object instance))
            {
                return (T)instance;
            }
            
            // Check if we have a binding for this type
            if (_bindings.TryGetValue(type, out Func<object> factory))
            {
                // Create the instance
                instance = factory();
                
                // Cache the instance
                _instances[type] = instance;
                
                return (T)instance;
            }
            
            throw new InvalidOperationException($"No binding found for type {type.Name}");
        }
    }
}
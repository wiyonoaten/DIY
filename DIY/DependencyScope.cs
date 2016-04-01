using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DIY
{
    /// <summary>
    /// Pass down an instance of this to objects created within a class for defining 
    /// a further-scoped dependency container down the chain.
    /// 
    /// By convention, there is one instance at the application root, typically accessed
    /// through static property of the application class. Further instances are passed down 
    /// through constructors at each level, ie. thus effectively an object of this class 
    /// becomes the only 'dependency' injected through constructors.
    /// </summary>
    public class DependencyScope
    {
        private readonly IDictionary<Type, object> _instanceRegistry = new ConcurrentDictionary<Type, object>();
        private readonly IDictionary<Type, Func<object>> _factoryRegistry = new ConcurrentDictionary<Type, Func<object>>();

        private readonly DependencyScope _parentScope;

        public DependencyScope()
        {
            _parentScope = null;
        }

        private DependencyScope(DependencyScope parentScope)
        {
            _parentScope = parentScope;
        }

        public DependencyScope MakeChildScope()
        {
            return new DependencyScope(this);
        }

        public void RegisterSingleton<I>(I service)
        {
            _instanceRegistry[typeof(I)] = service;
        }

        public void RegisterFactory<I>(Func<I> serviceFactory)
        {
            _factoryRegistry[typeof(I)] = () => serviceFactory.Invoke();
        }

        public void RegisterFactory<I, T>()
            where T : I
        {
            _factoryRegistry[typeof(I)] = () =>
            {
                var derivedType = typeof(T);
                if (_GetDefaultConstructor(derivedType) != null)
                {
                    return Activator.CreateInstance(derivedType);
                }
                else
                {
                    return Activator.CreateInstance(derivedType, this);
                }
            };
        }

        public void RegisterFactoryOfSingleton<I>(Func<I> serviceFactory)
        {
            _factoryRegistry[typeof(I)] = () =>
            {
                I instance = serviceFactory.Invoke();

                _instanceRegistry[typeof(I)] = instance;

                return instance;
            };
        }

        public void RegisterFactoryOfSingleton<I, T>()
            where T : I
        {
            _factoryRegistry[typeof(I)] = () =>
            {
                I instance;

                var derivedType = typeof(T);
                if (_GetDefaultConstructor(derivedType) != null)
                {
                    instance = (T) Activator.CreateInstance(derivedType);
                }
                else
                {
                    instance = (T) Activator.CreateInstance(derivedType, this);
                }

                _instanceRegistry[typeof(I)] = instance;

                return instance;
            };
        }

        private static ConstructorInfo _GetDefaultConstructor(Type type)
        {
            foreach (var constructor in type.GetTypeInfo().DeclaredConstructors)
            {
                if (constructor.GetParameters().Length == 0)
                {
                    return constructor;
                }
            }

            return null;
        }

        /// <summary>
        /// Call this method at appropriate place to initialise all the declared dependencies accordingly, 
        /// where declared dependencies are instance fields/properties marked with Dependency attribute).
        /// 
        /// By convention, place the call inside the obj's constructor passing in itself (ie. this).
        /// </summary>
        public void ResolveDependencies(object obj)
        {
            var typeInfo = obj.GetType().GetTypeInfo();

            var fields = typeInfo.DeclaredFields;
            foreach (var field in fields)
            {
                if (!field.IsStatic)
                {
                    _ResolveMemberDependency(obj, field);
                }
            }

            var props = typeInfo.DeclaredProperties;
            foreach (var prop in props)
            {
                if (prop.GetMethod != null && prop.SetMethod != null
                    && !prop.GetMethod.IsStatic && !prop.SetMethod.IsStatic)
                {
                    _ResolveMemberDependency(obj, prop);
                }
            }
        }

        private void _ResolveMemberDependency(object obj, MemberInfo member)
        {
            var dependencyAttr = member.GetCustomAttributes(typeof(DependencyAttribute), true).FirstOrDefault() as DependencyAttribute;
            if (dependencyAttr == null)
            {
                return;
            }

            Type dependencyType;

            if (member is FieldInfo)
            {
                FieldInfo field = member as FieldInfo;

                dependencyType = field.FieldType;
            }
            else if (member is PropertyInfo)
            {
                PropertyInfo prop = member as PropertyInfo;
                if (!prop.CanWrite)
                {
                    return;
                }

                dependencyType = prop.PropertyType;
            }
            else
            {
                return;
            }

            object service = _GetDependency(dependencyType);

            if (service != null)
            {
                if (member is FieldInfo)
                {
                    (member as FieldInfo).SetValue(obj, service);
                }
                else if (member is PropertyInfo)
                {
                    (member as PropertyInfo).SetValue(obj, service);
                }
            }
            else if (!dependencyAttr.IsOptional)
            {
                throw new DependencyNotResolvedException(dependencyType);
            }
        }

        private object _GetDependency(Type dependencyType)
        {
            if (_instanceRegistry.ContainsKey(dependencyType))
            {
                return _instanceRegistry[dependencyType];
            }
            else if (_factoryRegistry.ContainsKey(dependencyType))
            {
                Func<object> serviceFactory = _factoryRegistry[dependencyType];

                return serviceFactory.Invoke();
            }
            else if (_parentScope != null)
            {
                return _parentScope._GetDependency(dependencyType);
            }

            return null;
        }
    }
}

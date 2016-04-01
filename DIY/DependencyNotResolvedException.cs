using System;

namespace DIY
{
    public class DependencyNotResolvedException : Exception
    {
        public DependencyNotResolvedException(Type serviceType)
             : base("Dependency of type '" + serviceType.FullName + "' could not be resolved and is not an optional dependency.")
        {
        }
    }
}
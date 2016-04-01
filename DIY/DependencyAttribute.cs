using System;

namespace DIY
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class DependencyAttribute : Attribute
    {
        public DependencyAttribute()
        {
        }

        public bool IsOptional { get; set; }
    }
}

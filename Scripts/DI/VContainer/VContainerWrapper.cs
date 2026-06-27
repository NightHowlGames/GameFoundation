#if GDK_VCONTAINER
#nullable enable
#nullable enable
namespace GameFoundation.DI
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using UnityEngine;
    using VContainer;
    using VContainer.Internal;
    using VContainer.Unity;
    using PreserveAttribute = UnityEngine.Scripting.PreserveAttribute;

    public sealed class VContainerWrapper : IDependencyContainer
    {
        private readonly IObjectResolver container;

        [Preserve]
        public VContainerWrapper(IObjectResolver container)
        {
            this.container = container;
        }

        bool IDependencyContainer.TryResolve(Type type, [MaybeNullWhen(false)] out object instance)
        {
            return this.container.TryResolve(type, out instance);
        }

        bool IDependencyContainer.TryResolve<T>([MaybeNullWhen(false)] out T instance)
        {
            return this.container.TryResolve(out instance);
        }

        object IDependencyContainer.Resolve(Type type)
        {
            return this.container.Resolve(type);
        }

        T IDependencyContainer.Resolve<T>()
        {
            return this.container.Resolve<T>();
        }

        object[] IDependencyContainer.ResolveAll(Type type)
        {
            return ((IEnumerable)this.container.Resolve(typeof(IEnumerable<>).MakeGenericType(type))).Cast<object>().ToArray();
        }

        T[] IDependencyContainer.ResolveAll<T>()
        {
            return this.container.Resolve<IEnumerable<T>>().ToArray();
        }

        object IDependencyContainer.Instantiate(Type type, params object[] @params)
        {
            return InjectorCache.GetOrBuild(type).CreateInstance(this.container, @params.Select(param => new Parameter(param)).ToArray());
        }

        T IDependencyContainer.Instantiate<T>(params object[] @params)
        {
            return (T)((IDependencyContainer)this).Instantiate(typeof(T), @params);
        }

        void IDependencyContainer.Inject(object instance)
        {
            this.container.Inject(instance);
        }

        void IDependencyContainer.InjectGameObject(GameObject instance)
        {
            this.container.InjectGameObject(instance);
        }

        GameObject IDependencyContainer.InstantiatePrefab(GameObject prefab)
        {
            return this.container.Instantiate(prefab);
        }

        private sealed class Parameter : IInjectParameter
        {
            private readonly object value;

            public Parameter(object value)
            {
                this.value = value;
            }

            public bool Match(Type parameterType, string parameterName)
            {
                return parameterType.IsInstanceOfType(this.value);
            }

            public object GetValue(IObjectResolver resolver)
            {
                return this.value;
            }
        }
    }
}
#endif

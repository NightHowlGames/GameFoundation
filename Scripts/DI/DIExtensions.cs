#nullable enable
namespace GameFoundation.DI
{
    #if GDK_VCONTAINER
    using System;
    using System.Linq;
    using VContainer;
    using Object = UnityEngine.Object;

    public sealed class InjectAttribute : VContainer.InjectAttribute
    {
    }

    public static class DIExtensions
    {
        private static SceneScope? CurrentSceneContext;

        /// <summary>
        ///     Get current scene <see cref="IDependencyContainer"/>
        /// </summary>
        public static IDependencyContainer GetCurrentContainer()
        {
            if (CurrentSceneContext == null) CurrentSceneContext = Object.FindObjectOfType<SceneScope>();
            return CurrentSceneContext.Container.Resolve<IDependencyContainer>();
        }

        /// <inheritdoc cref="GetCurrentContainer()"/>
        public static IDependencyContainer GetCurrentContainer(this object _)
        {
            return GetCurrentContainer();
        }

        public static void RegisterDerivedTypes<T>(this IContainerBuilder builder, Lifetime lifetime = Lifetime.Singleton)
        {
            var baseType = typeof(T);
            var derivedTypes = AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => !assembly.IsDynamic)
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => !type.IsAbstract && baseType.IsAssignableFrom(type));

            foreach (var type in derivedTypes)
            {
                builder.Register(type, lifetime);
            }
        }
    }
    #else
    using System;

    public static class DIExtensions
    {
        public static IDependencyContainer GetCurrentContainer()
        {
            throw new NotSupportedException("Please use Zenject or VContainer");
        }

        public static IDependencyContainer GetCurrentContainer(this object _) => GetCurrentContainer();
    }
    #endif
}

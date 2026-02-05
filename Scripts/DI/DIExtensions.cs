#nullable enable
namespace GameFoundation.DI
{
    #if GDK_ZENJECT
    using UnityEngine;
    using Zenject;

    public static class DIExtensions
    {
        private static SceneContext? CurrentSceneContext;

        /// <summary>
        ///     Get current scene <see cref="IDependencyContainer"/>
        /// </summary>
        public static IDependencyContainer GetCurrentContainer()
        {
            if (CurrentSceneContext == null)
            {
                CurrentSceneContext = Object.FindObjectOfType<SceneContext>();
            }
            return CurrentSceneContext.Container.Resolve<IDependencyContainer>();
        }

        /// <inheritdoc cref="GetCurrentContainer()"/>
        public static IDependencyContainer GetCurrentContainer(this object _) => GetCurrentContainer();
    }
    #elif GDK_VCONTAINER
    using UniT.Extensions;
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
            typeof(T).GetDerivedTypes().ForEach(type => builder.Register(type, lifetime));
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
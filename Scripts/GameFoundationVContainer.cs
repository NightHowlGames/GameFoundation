using GDKConfig = Models.GDKConfig;

#if GDK_VCONTAINER
#nullable enable
namespace GameFoundation.Scripts
{
    using GameFoundation.BlueprintFlow;
    using GameFoundation.DI;
    using GameFoundation.Scripts.UserData;
    using GameFoundation.Scripts.Utilities;
    using GameFoundation.Signals;
    using GameFoundation.UIModule.UIModule;
    using GameFoundation.Utilities.ApplicationServices;
    using GameFoundation.Utilities.GameQueueAction;
    using UniT.Extensions;
    using UniT.Logging.DI;
    using UniT.Pooling.DI;
    using UniT.ResourceManagement.DI;
    using UnityEngine;
    using UnityEngine.EventSystems;
    using VContainer;

    public static class GameFoundationVContainer
    {
        public static void RegisterGameFoundation(this IContainerBuilder builder, Transform rootTransform)
        {
            builder.Register<VContainerWrapper>(Lifetime.Scoped).AsImplementedInterfaces();
            builder.Register<VContainerAdapter>(Lifetime.Scoped).AsImplementedInterfaces();

            builder.RegisterSignalBus();
            builder.RegisterBlueprints();
            builder.RegisterScreenManager();
            builder.RegisterApplicationServices(rootTransform);
            builder.RegisterGameQueueActionService();

            builder.RegisterInstance(Resources.Load<GDKConfig>("GameConfigs/GDKConfig"));

            builder.RegisterLoggerManager();
            builder.RegisterAssetsManager();
            builder.RegisterScenesManager();
            builder.RegisterExternalAssetsManager();
            builder.RegisterObjectPoolManager();

            builder.Register<AudioService>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<UserDataManager>(Lifetime.Singleton).AsSelf().AsImplementedInterfaces();
            builder.Register(typeof(ICloudDataHandler).GetSingleDerivedType(), Lifetime.Singleton).AsImplementedInterfaces();

            builder.DeclareSignal<UserDataLoadedSignal>();
            
            builder.RegisterComponentInNewPrefabResource<EventSystem>(nameof(EventSystem), Lifetime.Singleton).UnderTransform(rootTransform);
            builder.AutoResolve<EventSystem>();
        }
    }
}
#endif
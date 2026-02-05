#if GDK_VCONTAINER
#nullable enable
namespace GameFoundation.Utilities.ApplicationServices
{
    using GameFoundation.Scripts.Utilities.ApplicationServices;
    using GameFoundation.Signals;
    using UnityEngine;
    using VContainer;
    using VContainer.Unity;

    public static class ApplicationServicesVContainer
    {
        public static void RegisterApplicationServices(this IContainerBuilder builder, Transform rootTransform)
        {
            builder.RegisterComponentOnNewGameObject<MinimizeAppService>(Lifetime.Singleton).UnderTransform(rootTransform).AsSelf().AsImplementedInterfaces();
            builder.AutoResolve<MinimizeAppService>();

            builder.DeclareSignal<ApplicationPauseSignal>();
            builder.DeclareSignal<ApplicationQuitSignal>();
            builder.DeclareSignal<UpdateTimeAfterFocusSignal>();
        }
    }
}
#endif
namespace GameFoundation.Scripts.Utilities.ApplicationServices
{
    using System;
    using Cysharp.Threading.Tasks;
    using GameFoundation.DI;
    using GameFoundation.Scripts.UserData;
    using GameFoundation.Signals;
    using UnityEngine;

    /// <summary>Catch application event ex pause, focus and more.... </summary>
    public class MinimizeAppService : MonoBehaviour, IInitializable, IDisposable
    {
        private SignalBus       signalBus;
        private UserDataManager userDataManager;

        [Inject]
        public void Construct(SignalBus signalBus, UserDataManager userDataManager)
        {
            this.signalBus       = signalBus;
            this.userDataManager = userDataManager;
        }

        private readonly ApplicationPauseSignal     applicationPauseSignal     = new(false);
        private readonly ApplicationQuitSignal      applicationQuitSignal      = new();
        private readonly UpdateTimeAfterFocusSignal updateTimeAfterFocusSignal = new();

        private DateTime timeBeforeAppPause = DateTime.Now;
        private bool     isUserDataLoaded;

        //Todo need
        private const int MinimizeTimeToReload = 5;

        private void OnApplicationPause(bool pauseStatus)
        {
            this.applicationPauseSignal.PauseStatus = pauseStatus;
            this.signalBus.Fire(this.applicationPauseSignal); // Active this signal later, when need

            if (pauseStatus)
            {
                this.timeBeforeAppPause = DateTime.Now;

                // save local data to storage
                if (this.isUserDataLoaded) this.userDataManager.SaveAllAsync().Forget();
            }
            else
            {
                //TODO: Reload when open minimized game

                var intervalTimeMinimize = DateTime.Now - this.timeBeforeAppPause;

                if (MinimizeTimeToReload > 0 && intervalTimeMinimize.TotalMinutes >= MinimizeTimeToReload)
                {
                    // Reload game
                }

                this.updateTimeAfterFocusSignal.MinimizeTime = intervalTimeMinimize.TotalSeconds;
                this.signalBus.Fire(this.updateTimeAfterFocusSignal); // temporary disable this function, re-active later when game specs require
            }
        }

        private void OnApplicationQuit()
        {
            this.userDataManager.SaveAllAsync(true).Forget();
            this.signalBus.Fire(this.applicationQuitSignal);
        }

        void IInitializable.Initialize()
        {
            this.signalBus.Subscribe<UserDataLoadedSignal>(this.OnUserDataLoaded);
        }

        private void OnUserDataLoaded()
        {
            this.isUserDataLoaded = true;
        }

        void IDisposable.Dispose()
        {
            this.signalBus.Unsubscribe<UserDataLoadedSignal>(this.OnUserDataLoaded);
        }
    }
}
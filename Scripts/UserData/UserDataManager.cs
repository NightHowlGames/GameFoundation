namespace GameFoundation.Scripts.UserData
{
    using System;
    using System.Linq;
    using Cysharp.Threading.Tasks;
    using GameFoundation.DI;
    using GameFoundation.Signals;
    using Newtonsoft.Json;
    using UniT.Data;
    using UniT.Extensions;
    using UniT.Logging;
    using UnityEngine;
    using ILogger = UniT.Logging.ILogger;

    public sealed class UserDataManager : IInitializable
    {
        private readonly IDataManager      dataManager;
        private readonly ICloudDataHandler cloudDataHandler;
        private readonly SignalBus         signalBus;
        private readonly ILogger           logger;

        private Type[] allUserDataTypes;
        private Type[] cloudUserDataTypes;

        public const string KeyPrefix = "LD-";

        public UserDataManager(IDataManager dataManager, ICloudDataHandler cloudDataHandler, SignalBus signalBus, ILoggerManager loggerManager)
        {
            this.dataManager      = dataManager;
            this.cloudDataHandler = cloudDataHandler;
            this.signalBus        = signalBus;
            this.logger           = loggerManager.GetLogger(this);
        }

        void IInitializable.Initialize()
        {
            this.allUserDataTypes   = typeof(IWritableData).GetDerivedTypes().ToArray();
            this.cloudUserDataTypes = this.allUserDataTypes.Where(type => type.GetCustomAttributes(typeof(LocalDataOnlyAttribute), false).Length == 0).ToArray();
        }

        public T Get<T>() where T : class, IWritableData => (T)this.InternalLoad(typeof(T));

        public UniTask SaveAllAsync(bool force = false)
        {
            return UniTask.WhenAll(this.InternalSyncToRemoteAsync(force), this.InternalSaveAllAsync());
        }

        public async UniTask LoadAllAsync()
        {
            await UniTask.NextFrame(); // Wait for one frame to ensure all systems are initialized
            await UniTask.WhenAll(this.allUserDataTypes.Select(this.InternalLoadAsync));
            this.signalBus.Fire(new UserDataLoadedSignal());
            await UniTask.WhenAll(FetchFromRemoteAsyncIfStale(), SyncToRemoteAsyncIfStale());
            return;


            async UniTask FetchFromRemoteAsyncIfStale()
            {
                if (await this.cloudDataHandler.CheckLocalStaleAsync())
                {
                    await this.FetchFromRemoteAsync();
                }
            }

            async UniTask SyncToRemoteAsyncIfStale()
            {
                if (await this.cloudDataHandler.CheckRemoteStaleAsync())
                {
                    this.InternalSyncToRemoteAsync(true).Forget();
                }
            }
        }

        public void Update<T>(T data) where T : class, IWritableData => this.InternalUpdate(data);

        public void Save<T>() where T : class, IWritableData => this.InternalSaveAsync(typeof(T)).Forget();

        public UniTask SaveAsync<T>() where T : class, IWritableData => this.InternalSaveAsync(typeof(T));

        public async UniTask<T> LoadAsync<T>() where T : class, IWritableData
        {
            return (T)await this.InternalLoadAsync(typeof(T));
        }

        public async UniTask FetchFromRemoteAsync()
        {
            var keys      = this.cloudUserDataTypes.Select(KeyOf).ToArray();
            var jsonDatas = await this.cloudDataHandler.FetchFromRemoteAsync(keys);

            if (jsonDatas == null) return;
            IterTools.Zip(this.cloudUserDataTypes, jsonDatas).ForEach((type, json) =>
            {
                if (string.IsNullOrEmpty(json)) return;
                object data;
                try
                {
                    data = JsonConvert.DeserializeObject(json, type);
                }
                catch (Exception e)
                {
                    this.logger.Error($"Failed to deserialize data from cloud data with {type}: {e}");
                    return;
                }

                if (data != null) this.InternalUpdate(data);
            });
            await this.InternalSaveAllAsync();
        }

#region Private

        private static string KeyOf(Type type)
        {
            return $"{KeyPrefix}{type.Name}";
        }

        private UniTask InternalSyncToRemoteAsync(bool force)
        {
            var keys  = this.cloudUserDataTypes.Select(KeyOf);
            var jsons = this.cloudUserDataTypes.Select(type => JsonConvert.SerializeObject(this.InternalLoad(type)));

            return this.cloudDataHandler.SyncToRemoteAsync(IterTools.Zip(keys, jsons).ToArray(), force);
        }
        private object InternalLoad(Type type) => this.dataManager.Load(KeyOf(type), type);

        private UniTask<object> InternalLoadAsync(Type type) => this.dataManager.LoadAsync(KeyOf(type), type);

        private void InternalUpdate(object data) => this.dataManager.Update(KeyOf(data.GetType()), data);

        private UniTask InternalSaveAsync(Type type) => this.dataManager.SaveAndFlushAsync(KeyOf(type));

        private async UniTask InternalSaveAllAsync()
        {
            await UniTask.WhenAll(this.allUserDataTypes.Select(this.InternalSaveAsync));
            PlayerPrefs.Save();
        }

#endregion
    }
}
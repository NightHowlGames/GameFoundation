namespace GameFoundation.Scripts.UserData
{
    using System.Collections.Generic;
    using Cysharp.Threading.Tasks;

    public interface ICloudDataHandler
    {
        public void ResetLocalDataVersion();

        protected internal UniTask<bool> CheckRemoteStaleAsync();

        protected internal UniTask<bool> CheckLocalStaleAsync();

        protected internal UniTask SyncToRemoteAsync(IReadOnlyCollection<(string key, string json)> datas, bool force = false);

        protected internal UniTask<IReadOnlyCollection<string>> FetchFromRemoteAsync(string[] keys);
    }
}
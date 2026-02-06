#if !UNIT_CLOUDSAVE
namespace GameFoundation.Scripts.UserData
{
    using System.Collections.Generic;
    using Cysharp.Threading.Tasks;

    public sealed class DummyCloudDataHandler : ICloudDataHandler
    {
        void ICloudDataHandler.ResetLocalDataVersion() { }

        UniTask<bool> ICloudDataHandler.CheckRemoteStaleAsync() => UniTask.FromResult(false);

        UniTask<bool> ICloudDataHandler.CheckLocalStaleAsync() => UniTask.FromResult(false);

        UniTask ICloudDataHandler.SyncToRemoteAsync(IReadOnlyCollection<(string key, string json)> datas, bool force) => UniTask.CompletedTask;

        UniTask<IReadOnlyCollection<string>> ICloudDataHandler.FetchFromRemoteAsync(string[] keys) => UniTask.FromResult<IReadOnlyCollection<string>>(null);
    }
}
#endif
#if GDK_VCONTAINER
namespace GameFoundation.UIModule.UITK.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using Cysharp.Threading.Tasks;
    using GameFoundation.DI;
    using GameFoundation.Scripts.Utilities;
    using UniT.ResourceManagement;
    using UnityEngine;
    using VContainer;
    using Object = UnityEngine.Object;

    /// <summary>A <c>SceneScope</c> whose registrations the test supplies.</summary>
    /// <remarks>
    /// <c>DIExtensions.GetCurrentContainer()</c> — which <c>ScreenManager</c> uses to
    /// instantiate a presenter — looks for a <c>SceneScope</c> in the loaded scenes, so a
    /// test that goes through the manager needs a real one. Build it inactive, set
    /// <see cref="Installer"/>, then activate: <c>LifetimeScope</c> builds its container in
    /// <c>Awake</c>, which fires the moment the GameObject becomes active.
    /// </remarks>
    public sealed class TestSceneScope : SceneScope
    {
        public Action<IContainerBuilder> Installer;

        protected override void Configure(IContainerBuilder builder)
        {
            this.Installer?.Invoke(builder);
        }
    }

    /// <summary>
    /// An <see cref="IAssetsManager"/> serving assets the test hands it, by key.
    /// </summary>
    /// <remarks>
    /// The real one is Addressables, and the UI Toolkit popup has no Addressables entry
    /// anywhere yet — creating one means editing the consuming Unity project, not this
    /// package. Serving the same <c>VisualTreeAsset</c> from a dictionary exercises the
    /// identical call the manager makes (<c>LoadAsync&lt;VisualTreeAsset&gt;(key)</c>)
    /// without needing that entry to exist.
    /// </remarks>
    public sealed class StubAssetsManager : IAssetsManager
    {
        private readonly Dictionary<string, Object> assets = new();

        public readonly List<string> UnloadedKeys = new();

        public void Add(string key, Object asset) { this.assets[key] = asset; }

        public T Load<T>(string key) where T : Object
        {
            if (!this.assets.TryGetValue(key, out var asset)) throw new KeyNotFoundException($"No stub asset for key '{key}'.");
            return (T)asset;
        }

        public UniTask<T> LoadAsync<T>(string key, IProgress<float> progress = null, CancellationToken cancellationToken = default) where T : Object
        {
            return UniTask.FromResult(this.Load<T>(key));
        }

        public void Unload(string key) { this.UnloadedKeys.Add(key); }

        public IEnumerable<T> LoadAll<T>(string key) where T : Object => throw new NotSupportedException();

        public UniTask<IEnumerable<T>> LoadAllAsync<T>(string key, IProgress<float> progress = null, CancellationToken cancellationToken = default) where T : Object => throw new NotSupportedException();

        public void Download(string key) => throw new NotSupportedException();

        public void DownloadAll() => throw new NotSupportedException();

        public UniTask DownloadAsync(string key, IProgress<float> progress = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public UniTask DownloadAllAsync(IProgress<float> progress = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public void Dispose() { }
    }

    /// <summary>A silent <see cref="IAudioService"/>; the popup presenter plays a click sound.</summary>
    public sealed class StubAudioService : IAudioService
    {
        public readonly List<string> PlayedSounds = new();

        public void PlaySound(string name, AudioSource sender) { this.PlayedSounds.Add(name); }

        public void PlaySound(string name, bool isLoop = false, float volumeScale = 1f, float fadeSeconds = 1f, bool isAverage = false, bool isPausePlaylist = false) { this.PlayedSounds.Add(name); }

        public void PlaySound(AudioClip audioClip, bool isLoop = false, float volumeScale = 1f, float fadeSeconds = 1f, bool isAverage = false, bool isPausePlaylist = false) { }

        public AudioSource GetLoopingSound(string name) => null;

        public void StopLoopingSound(string name) { }

        public void StopAllSound() { }

        public void StopAll() { }

        public void PlayPlayList(string musicName, bool random = false, float volumeScale = 1f, float fadeSeconds = 1f, float fadeProgressThreshold = 0f, bool persist = false) { }

        public void PlayPlayList(AudioClip audioClip, bool random = false, float volumeScale = 1f, float fadeSeconds = 1f, float fadeProgressThreshold = 0f, bool persist = false) { }

        public void StopPlayList(float fadeSeconds = 1f) { }

        public void SetPlayListTime(float time) { }

        public float GetPlayListTime() => 0f;

        public void SetPlayListPitch(float pitch) { }

        public void SetPlayListLoop(bool isLoop) { }

        public void PausePlayList(float fadeSeconds = 1f) { }

        public void ResumePlayList(float fadeSeconds = 1f) { }

        public bool IsPlayingPlayList() => false;

        public void StopAllPlayList() { }

        public void PauseEverything() { }

        public void ResumeEverything() { }
    }
}
#endif

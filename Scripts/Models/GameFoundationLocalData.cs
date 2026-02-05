namespace GameFoundation.Scripts.Models
{
    using GameFoundation.Scripts.UserData;
    using UniT.Data;
    using R3;
    using UnityEngine.Scripting;

    [Preserve]
    [LocalDataOnly]
    public sealed class SoundSetting : IWritableData
    {
        public ReactiveProperty<bool>  MasterVolume { get; set; } = new(true);
        public ReactiveProperty<bool>  MuteMusic    { get; set; } = new(false);
        public ReactiveProperty<bool>  MuteSound    { get; set; } = new(false);
        public ReactiveProperty<float> MusicValue   { get; set; } = new(1);
        public ReactiveProperty<float> SoundValue   { get; set; } = new(1);
    }
}
using Android.Media;
using DrawSound.Core.Audio;
using Microsoft.Extensions.Options;

namespace DrawSound.Services;

public class TonePlayer : ITonePlayer, IDisposable
{
    private class Voice
    {
        public double Frequency { get; set; }
        public float[] WaveTable { get; set; } = Array.Empty<float>();
        public int Position { get; set; }
        public bool Releasing { get; set; }
        public int ReleaseSamplesRemaining { get; set; }
    }

    private AudioTrack? _audioTrack;
    private CancellationTokenSource? _cts;
    private Task? _playTask;
    
    private const int SampleRate = 44100;
    private readonly WaveTableGenerator _waveTableGenerator;
    private readonly VoiceMixer _mixer;

    public TonePlayer(IOptions<AudioSettings> audioOptions)
    {
        _waveTableGenerator = new WaveTableGenerator(SampleRate);
        var releaseMs = audioOptions.Value.ReleaseMs;
        var releaseSamples = Math.Max(1, (int)Math.Round(SampleRate * (releaseMs / 1000d)));
        _mixer = new VoiceMixer(SampleRate, releaseSamples, audioOptions.Value.MaxPolyphony);
    }

    public void StartTone(double frequency)
    {
        var waveTable = _waveTableGenerator.GenerateSineWave(frequency);
        StartTone(frequency, waveTable);
    }

    public void StartTone(double frequency, float[] waveTable)
    {
        EnsurePlaybackThread();
        _mixer.AddVoice(frequency, waveTable);
    }

    public void UpdateWaveTable(double frequency, float[] waveTable)
    {
        _mixer.UpdateVoice(frequency, waveTable);
    }

    public void StopTone(double frequency)
    {
        _mixer.ReleaseVoice(frequency);
    }

    private void EnsurePlaybackThread()
    {
        if (_playTask != null && !_playTask.IsCompleted)
            return;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        var minBufferSize = AudioTrack.GetMinBufferSize(
            SampleRate,
            ChannelOut.Mono,
            Encoding.PcmFloat);

        _audioTrack?.Release();
        _audioTrack = new AudioTrack.Builder()
            .SetAudioAttributes(new AudioAttributes.Builder()
                .SetUsage(AudioUsageKind.Media)!
                .SetContentType(AudioContentType.Music)!
                .Build()!)
            .SetAudioFormat(new AudioFormat.Builder()
                .SetEncoding(Encoding.PcmFloat)!
                .SetSampleRate(SampleRate)!
                .SetChannelMask(ChannelOut.Mono)!
                .Build()!)
            .SetBufferSizeInBytes(minBufferSize)
            .SetTransferMode(AudioTrackMode.Stream)
            .Build();

        _audioTrack.Play();

        _playTask = Task.Run(() => PlayVoices(minBufferSize / sizeof(float), token), token);
    }

    private void PlayVoices(int bufferSamples, CancellationToken token)
    {
        var buffer = new float[bufferSamples];

        while (!token.IsCancellationRequested)
        {
            _mixer.Mix(buffer);

            try
            {
                _audioTrack?.Write(buffer, 0, bufferSamples, WriteMode.Blocking);
            }
            catch
            {
                break;
            }
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();

        try
        {
            _playTask?.Wait(100);
        }
        catch { }

        _audioTrack?.Stop();
        _audioTrack?.Release();
        _audioTrack?.Dispose();
        _audioTrack = null;

        _cts?.Dispose();
        _cts = null;
        _playTask = null;
    }
}

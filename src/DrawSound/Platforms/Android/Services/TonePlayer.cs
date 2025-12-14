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
    private readonly object _lock = new();
    private readonly int _releaseSamples;
    private readonly int _maxPolyphony;
    private readonly List<Voice> _voices = new();

    public TonePlayer(IOptions<AudioSettings> audioOptions)
    {
        _waveTableGenerator = new WaveTableGenerator(SampleRate);
        var releaseMs = audioOptions.Value.ReleaseMs;
        _releaseSamples = Math.Max(1, (int)Math.Round(SampleRate * (releaseMs / 1000d)));
        _maxPolyphony = Math.Max(1, audioOptions.Value.MaxPolyphony);
    }

    public void StartTone(double frequency)
    {
        var waveTable = _waveTableGenerator.GenerateSineWave(frequency);
        StartTone(frequency, waveTable);
    }

    public void StartTone(double frequency, float[] waveTable)
    {
        var cloned = (float[])waveTable.Clone();
        EnsurePlaybackThread();

        lock (_lock)
        {
            if (_voices.Count >= _maxPolyphony)
            {
                _voices.RemoveAt(0);
            }

            _voices.Add(new Voice
            {
                Frequency = frequency,
                WaveTable = cloned,
                Position = 0,
                Releasing = false,
                ReleaseSamplesRemaining = _releaseSamples
            });
        }
    }

    public void UpdateWaveTable(float[] waveTable)
    {
        // Kept for interface compatibility; not used in polyphonic path
    }

    public void UpdateWaveTable(double frequency, float[] waveTable)
    {
        var cloned = (float[])waveTable.Clone();
        lock (_lock)
        {
            foreach (var voice in _voices)
            {
                if (Math.Abs(voice.Frequency - frequency) < 0.0001)
                {
                    voice.WaveTable = cloned;
                    if (voice.Position >= cloned.Length)
                        voice.Position %= cloned.Length;
                }
            }
        }
    }

    public void StopTone(double frequency)
    {
        lock (_lock)
        {
            var voice = _voices.FirstOrDefault(v => Math.Abs(v.Frequency - frequency) < 0.0001);
            if (voice != null)
            {
                voice.Releasing = true;
                voice.ReleaseSamplesRemaining = _releaseSamples;
            }
        }
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
            Voice[] snapshot;
            lock (_lock)
            {
                snapshot = _voices.ToArray();
            }

            if (snapshot.Length == 0)
            {
                Array.Clear(buffer, 0, bufferSamples);
                _audioTrack?.Write(buffer, 0, bufferSamples, WriteMode.Blocking);
                Thread.Sleep(5);
                continue;
            }

            for (int i = 0; i < bufferSamples; i++)
            {
                float sample = 0f;

                foreach (var voice in snapshot)
                {
                    var table = voice.WaveTable;
                    int len = table.Length;
                    if (len == 0) continue;

                    float gain = voice.Releasing
                        ? Math.Max(0f, voice.ReleaseSamplesRemaining / (float)_releaseSamples)
                        : 1f;

                    sample += table[voice.Position % len] * gain;
                    voice.Position = (voice.Position + 1) % len;

                    if (voice.Releasing && voice.ReleaseSamplesRemaining > 0)
                    {
                        voice.ReleaseSamplesRemaining--;
                    }
                }

                buffer[i] = Math.Clamp(sample, -1f, 1f);
            }

            try
            {
                _audioTrack?.Write(buffer, 0, bufferSamples, WriteMode.Blocking);
            }
            catch
            {
                break;
            }

            lock (_lock)
            {
                for (int v = _voices.Count - 1; v >= 0; v--)
                {
                    var voice = _voices[v];
                    if (voice.Releasing && voice.ReleaseSamplesRemaining <= 0)
                    {
                        _voices.RemoveAt(v);
                    }
                }
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

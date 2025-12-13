using Android.Media;
using DrawSound.Core.Audio;

namespace DrawSound.Services;

public class TonePlayer : ITonePlayer, IDisposable
{
    private AudioTrack? _audioTrack;
    private CancellationTokenSource? _cts;
    private Task? _playTask;
    
    private const int SampleRate = 44100;
    private readonly WaveTableGenerator _waveTableGenerator;
    private float[]? _waveTable;
    private readonly object _waveTableLock = new();

    public TonePlayer()
    {
        _waveTableGenerator = new WaveTableGenerator(SampleRate);
    }

    public void StartTone(double frequency)
    {
        var waveTable = _waveTableGenerator.GenerateSineWave(frequency);
        StartTone(frequency, waveTable);
    }

    public void StartTone(double frequency, float[] waveTable)
    {
        StopTone();

        lock (_waveTableLock)
        {
            _waveTable = (float[])waveTable.Clone();
        }

        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        var minBufferSize = AudioTrack.GetMinBufferSize(
            SampleRate,
            ChannelOut.Mono,
            Encoding.PcmFloat);

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

        _playTask = Task.Run(() => PlayWaveTable(minBufferSize / sizeof(float), token), token);
    }

    public void UpdateWaveTable(float[] waveTable)
    {
        lock (_waveTableLock)
        {
            _waveTable = (float[])waveTable.Clone();
        }
    }

    private void PlayWaveTable(int bufferSamples, CancellationToken token)
    {
        var buffer = new float[bufferSamples];
        int waveTableIndex = 0;

        while (!token.IsCancellationRequested)
        {
            float[]? currentWaveTable;
            lock (_waveTableLock)
            {
                currentWaveTable = _waveTable;
            }

            if (currentWaveTable == null || currentWaveTable.Length == 0)
            {
                Thread.Sleep(10);
                continue;
            }

            int waveTableLength = currentWaveTable.Length;

            // Fill buffer by looping through the wavetable
            for (int i = 0; i < bufferSamples; i++)
            {
                buffer[i] = currentWaveTable[waveTableIndex % waveTableLength];
                waveTableIndex = (waveTableIndex + 1) % waveTableLength;
            }

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

    public void StopTone()
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

    public void Dispose()
    {
        StopTone();
    }
}

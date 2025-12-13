using Android.Media;

namespace DrawSound.Services;

public class TonePlayer : ITonePlayer, IDisposable
{
    private AudioTrack? _audioTrack;
    private CancellationTokenSource? _cts;
    private Task? _playTask;
    
    private const int SampleRate = 44100;
    private float[]? _waveTable;
    private double _waveTableFrequency;

    public void StartTone(double frequency)
    {
        StopTone();

        // Generate wavetable for one cycle of the sine wave
        _waveTable = GenerateWaveTable(frequency);
        _waveTableFrequency = frequency;

        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        // Calculate buffer size (use at least minBufferSize)
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

    private float[] GenerateWaveTable(double frequency)
    {
        // Calculate samples per cycle
        // For 261.63 Hz at 44100 Hz: 44100 / 261.63 ≈ 168.6 samples
        int samplesPerCycle = (int)Math.Round(SampleRate / frequency);
        
        // Ensure at least 2 samples
        samplesPerCycle = Math.Max(samplesPerCycle, 2);

        var waveTable = new float[samplesPerCycle];
        
        for (int i = 0; i < samplesPerCycle; i++)
        {
            double phase = 2.0 * Math.PI * i / samplesPerCycle;
            waveTable[i] = (float)(Math.Sin(phase) * 0.5); // 50% amplitude
        }

        return waveTable;
    }

    private void PlayWaveTable(int bufferSamples, CancellationToken token)
    {
        if (_waveTable == null) return;

        var buffer = new float[bufferSamples];
        int waveTableIndex = 0;
        int waveTableLength = _waveTable.Length;

        while (!token.IsCancellationRequested)
        {
            // Fill buffer by looping through the wavetable
            for (int i = 0; i < bufferSamples; i++)
            {
                buffer[i] = _waveTable[waveTableIndex];
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
        _waveTable = null;
    }

    public void Dispose()
    {
        StopTone();
    }
}


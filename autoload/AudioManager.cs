using Godot;
using System.Collections.Generic;

public partial class AudioManager : Node
{
    /// <summary>
    /// Sets the background music volume (0.0 = silent, 1.0 = full volume).
    /// </summary>
    public void SetBackgroundMusicVolume(float volume)
    {
        if (_backgroundMusic != null)
        {
            _backgroundMusic.VolumeDb = Mathf.LinearToDb(Mathf.Clamp(volume, 0f, 1f));
        }
    }
    private AudioStreamPlayer? _backgroundMusic;
    private AudioStreamPlayer? _soundEffectPlayer;
    private readonly Dictionary<int, AudioStream> _levelMusicCache = new();

    public override void _Ready()
    {
        _backgroundMusic = new AudioStreamPlayer();
        _soundEffectPlayer = new AudioStreamPlayer();
        
        AddChild(_backgroundMusic);
        AddChild(_soundEffectPlayer);
    }

    public void PlayLevelBackgroundMusic(int level)
    {
        int normalizedLevel = Mathf.Clamp(level, 1, GameState.MaxLevelCount);
        if (!_levelMusicCache.TryGetValue(normalizedLevel, out AudioStream? music))
        {
            string musicPath = $"res://scenes/bgm/lv{normalizedLevel}.ogg";
            music = GD.Load<AudioStream>(musicPath);
            if (music == null)
            {
                GD.PushWarning($"Missing level background music: {musicPath}");
                return;
            }

            if (music is AudioStreamOggVorbis oggStream)
            {
                oggStream.Loop = true;
            }

            _levelMusicCache[normalizedLevel] = music;
        }

        PlayBackgroundMusic(music);
    }

    public void PlayBackgroundMusic(AudioStream music)
    {
        if (_backgroundMusic == null)
        {
            return;
        }

        if (_backgroundMusic.Stream != music)
        {
            _backgroundMusic.Stream = music;
            _backgroundMusic.Play();
            return;
        }

        if (!_backgroundMusic.Playing)
        {
            _backgroundMusic.Play();
        }
    }

    public void StopBackgroundMusic()
    {
        if (_backgroundMusic == null)
        {
            return;
        }

        _backgroundMusic.Stop();
    }

    public bool IsBackgroundMusicPlaying()
    {
        return _backgroundMusic?.Playing ?? false;
    }

    public void PlaySoundEffect(AudioStream soundEffect)
    {
        if (_soundEffectPlayer == null)
        {
            return;
        }

        _soundEffectPlayer.Stream = soundEffect;
        _soundEffectPlayer.Play();
    }
}
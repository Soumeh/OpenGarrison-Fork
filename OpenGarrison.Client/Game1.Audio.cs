#nullable enable

using Microsoft.Xna.Framework.Audio;
using System;
using System.Collections.Generic;
using System.IO;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private SoundEffect? _menuMusic;
    private SoundEffectInstance? _menuMusicInstance;
    private SoundEffect? _faucetMusic;
    private SoundEffectInstance? _faucetMusicInstance;
    private SoundEffect? _ingameMusic;
    private SoundEffectInstance? _ingameMusicInstance;
    private bool _audioAvailable = true;
    private bool _ingameMusicEnabled = true;
    private readonly HashSet<ulong> _processedNetworkSoundEventIds = new();
    private readonly Queue<ulong> _processedNetworkSoundEventOrder = new();

    private void LoadMenuMusic()
    {
        if (!_audioAvailable)
        {
            return;
        }

        var candidates = new[] { "menumusic1.wav", "menumusic2.wav" };
        var chosen = candidates[_visualRandom.Next(candidates.Length)];
        TryLoadLoopedMusic(Path.Combine("Music", chosen), out _menuMusic, out _menuMusicInstance);
    }

    private void LoadFaucetMusic()
    {
        if (!_audioAvailable)
        {
            return;
        }

        TryLoadLoopedMusic(Path.Combine("Music", "faucetmusic.wav"), out _faucetMusic, out _faucetMusicInstance, 0.8f);
    }

    private void LoadIngameMusic()
    {
        if (!_audioAvailable)
        {
            return;
        }

        TryLoadLoopedMusic(Path.Combine("Music", "ingamemusic.wav"), out _ingameMusic, out _ingameMusicInstance);
    }

    private void TryLoadLoopedMusic(
        string relativePath,
        out SoundEffect? music,
        out SoundEffectInstance? musicInstance,
        float volume = 1f)
    {
        music = null;
        musicInstance = null;

        var musicPath = ProjectSourceLocator.FindFile(relativePath);
        if (musicPath is null || !File.Exists(musicPath))
        {
            return;
        }

        try
        {
            using var stream = File.OpenRead(musicPath);
            music = SoundEffect.FromStream(stream);
            musicInstance = music.CreateInstance();
            musicInstance.IsLooped = true;
            musicInstance.Volume = volume;
        }
        catch (Exception ex)
        {
            DisableAudio("initializing audio", ex);
        }
    }

    private void EnsureMenuMusicPlaying()
    {
        if (IsServerLauncherMode || _menuMusicInstance is null || !_audioAvailable)
        {
            return;
        }

        try
        {
            if (_menuMusicInstance.State != SoundState.Playing)
            {
                _menuMusicInstance.Play();
            }
        }
        catch (Exception ex)
        {
            DisableAudio("starting menu music", ex);
        }
    }

    private void EnsureFaucetMusicPlaying()
    {
        if (_faucetMusicInstance is null || !_audioAvailable)
        {
            return;
        }

        try
        {
            if (_faucetMusicInstance.State != SoundState.Playing)
            {
                _faucetMusicInstance.Play();
            }
        }
        catch (Exception ex)
        {
            DisableAudio("starting faucet music", ex);
        }
    }

    private void StopMenuMusic()
    {
        try
        {
            if (_menuMusicInstance?.State == SoundState.Playing)
            {
                _menuMusicInstance.Stop();
            }
        }
        catch
        {
        }
    }

    private void StopFaucetMusic()
    {
        try
        {
            if (_faucetMusicInstance?.State == SoundState.Playing)
            {
                _faucetMusicInstance.Stop();
            }
        }
        catch
        {
        }
    }

    private void EnsureIngameMusicPlaying()
    {
        if (_ingameMusicInstance is null || !_ingameMusicEnabled || !_audioAvailable)
        {
            return;
        }

        if (_world.MatchState.IsEnded)
        {
            return;
        }

        try
        {
            if (_ingameMusicInstance.State != SoundState.Playing)
            {
                _ingameMusicInstance.Play();
            }
        }
        catch (Exception ex)
        {
            DisableAudio("starting in-game music", ex);
        }
    }

    private void StopIngameMusic()
    {
        try
        {
            if (_ingameMusicInstance?.State == SoundState.Playing)
            {
                _ingameMusicInstance.Stop();
            }
        }
        catch
        {
        }
    }

    private void PlayDeathCamSoundIfNeeded()
    {
        if (!_audioAvailable)
        {
            return;
        }

        var deathCamActive = _killCamEnabled && !_world.LocalPlayer.IsAlive && _world.LocalDeathCam is not null;
        if (!deathCamActive || _wasDeathCamActive)
        {
            return;
        }

        var sound = _runtimeAssets.GetSound("DeathCamSnd");
        TryPlaySound(sound, 0.6f, 0f, 0f);
    }

    private void PlayRoundEndSoundIfNeeded()
    {
        if (!_audioAvailable)
        {
            return;
        }

        if (!_world.MatchState.IsEnded || _wasMatchEnded)
        {
            return;
        }

        var soundName = _world.MatchState.WinnerTeam switch
        {
            PlayerTeam.Red when _world.LocalPlayer.Team == PlayerTeam.Red => "VictorySnd",
            PlayerTeam.Blue when _world.LocalPlayer.Team == PlayerTeam.Blue => "VictorySnd",
            null => "FailureSnd",
            _ => "FailureSnd",
        };

        if (_ingameMusicInstance?.State == SoundState.Playing)
        {
            _ingameMusicInstance.Stop();
        }

        var sound = _runtimeAssets.GetSound(soundName);
        TryPlaySound(sound, 0.8f, 0f, 0f);
    }

    private void PlayPendingSoundEvents()
    {
        if (!_audioAvailable)
        {
            return;
        }

        foreach (var soundEvent in _world.DrainPendingSoundEvents())
        {
            if (!ShouldProcessNetworkEvent(soundEvent.EventId, _processedNetworkSoundEventIds, _processedNetworkSoundEventOrder))
            {
                continue;
            }

            if (string.Equals(soundEvent.SoundName, "ExplosionSnd", StringComparison.OrdinalIgnoreCase))
            {
                _explosions.Add(new ExplosionVisual(soundEvent.X, soundEvent.Y));
            }

            var sound = _runtimeAssets.GetSound(soundEvent.SoundName);
            if (sound is null)
            {
                continue;
            }

            var dx = soundEvent.X - _world.LocalPlayer.X;
            var dy = soundEvent.Y - _world.LocalPlayer.Y;
            var distance = MathF.Sqrt(dx * dx + dy * dy);
            var volume = Math.Clamp(1f - (distance / 1200f), 0f, 1f) * 0.6f;
            if (volume <= 0f)
            {
                continue;
            }

            var pan = Math.Clamp(dx / 600f, -1f, 1f);
            TryPlaySound(sound, volume, 0f, pan);
        }
    }

    private void TryPlaySound(SoundEffect? sound, float volume, float pitch, float pan)
    {
        if (!_audioAvailable || sound is null)
        {
            return;
        }

        try
        {
            sound.Play(volume, pitch, pan);
        }
        catch (Exception ex)
        {
            DisableAudio("playing sound", ex);
        }
    }

    private void DisableAudio(string reason, Exception ex)
    {
        if (!_audioAvailable)
        {
            return;
        }

        _audioAvailable = false;
        StopMenuMusic();
        StopFaucetMusic();
        StopIngameMusic();
        _menuMusicInstance?.Dispose();
        _menuMusicInstance = null;
        _menuMusic?.Dispose();
        _menuMusic = null;
        _faucetMusicInstance?.Dispose();
        _faucetMusicInstance = null;
        _faucetMusic?.Dispose();
        _faucetMusic = null;
        _ingameMusicInstance?.Dispose();
        _ingameMusicInstance = null;
        _ingameMusic?.Dispose();
        _ingameMusic = null;
        AddConsoleLine($"audio disabled: {reason} ({ex.GetType().Name}: {ex.Message})");
    }
}

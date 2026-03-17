#nullable enable

using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const float PlayerWeaponFlashDurationSeconds = 1f / LegacyMovementModel.SourceTicksPerSecond;
    private readonly HashSet<int> _activePlayerRenderStateIds = new();
    private readonly List<int> _stalePlayerRenderStateIds = new();

    private void UpdatePlayerRenderState(PlayerEntity player)
    {
        var playerStateKey = GetPlayerStateKey(player);
        if (!player.IsAlive)
        {
            _playerAnimationImages.Remove(playerStateKey);
            _playerWeaponFlashTimes.Remove(playerStateKey);
            _playerPreviousAmmoCounts.Remove(playerStateKey);
            _playerPreviousCooldownTicks.Remove(playerStateKey);
            _playerPreviousRenderPositions.Remove(playerStateKey);
            _playerPreviousRenderSampleTimes.Remove(playerStateKey);
            return;
        }

        var observedRenderVelocity = SampleObservedRenderVelocity(player);
        var renderHorizontalSpeed = GetPlayerRenderHorizontalSpeed(player, observedRenderVelocity);
        var renderVerticalSpeed = GetPlayerRenderVerticalSpeed(player, observedRenderVelocity);
        var horizontalSourceStepSpeed = GetPlayerAnimationSourceStepSpeed(renderHorizontalSpeed);
        var verticalSourceStepSpeed = GetPlayerAnimationSourceStepSpeed(renderVerticalSpeed);
        var animationElapsedSeconds = GetPlayerAnimationElapsedSeconds();
        var isRemoteNetworkPlayer = _networkClient.IsConnected && !ReferenceEquals(player, _world.LocalPlayer);
        var animationImage = _playerAnimationImages.GetValueOrDefault(playerStateKey, 0f);
        if (horizontalSourceStepSpeed < 0.2f)
        {
            animationImage = 0f;
        }

        var appearsAirborne = !GetPlayerRenderIsGrounded(player);
        if (_networkClient.IsConnected
            && ReferenceEquals(player, _world.LocalPlayer)
            && MathF.Abs(renderVerticalSpeed) > 20f)
        {
            appearsAirborne = true;
        }

        if (isRemoteNetworkPlayer && !player.IsGrounded)
        {
            appearsAirborne = verticalSourceStepSpeed > 0.35f;
        }

        if (appearsAirborne)
        {
            animationImage = 1f;
        }

        animationImage = (animationImage + GetPlayerAnimationAdvance(renderHorizontalSpeed, animationElapsedSeconds)) % 2f;
        _playerAnimationImages[playerStateKey] = animationImage;

        var flashTimeRemaining = _playerWeaponFlashTimes.GetValueOrDefault(playerStateKey, 0f);
        if (flashTimeRemaining > 0f)
        {
            flashTimeRemaining = MathF.Max(0f, flashTimeRemaining - animationElapsedSeconds);
        }

        var previousAmmo = _playerPreviousAmmoCounts.GetValueOrDefault(playerStateKey, player.CurrentShells);
        var previousCooldown = _playerPreviousCooldownTicks.GetValueOrDefault(playerStateKey, player.PrimaryCooldownTicks);
        if (player.PrimaryCooldownTicks > 0 && (player.CurrentShells < previousAmmo || previousCooldown <= 0))
        {
            flashTimeRemaining = PlayerWeaponFlashDurationSeconds;
        }

        _playerWeaponFlashTimes[playerStateKey] = flashTimeRemaining;
        _playerPreviousAmmoCounts[playerStateKey] = player.CurrentShells;
        _playerPreviousCooldownTicks[playerStateKey] = player.PrimaryCooldownTicks;
    }

    private void RemoveStalePlayerRenderState()
    {
        _activePlayerRenderStateIds.Clear();
        if (_world.LocalPlayer.IsAlive)
        {
            _activePlayerRenderStateIds.Add(GetPlayerStateKey(_world.LocalPlayer));
        }

        foreach (var player in EnumerateRemotePlayersForView())
        {
            if (player.IsAlive)
            {
                _activePlayerRenderStateIds.Add(GetPlayerStateKey(player));
            }
        }

        _stalePlayerRenderStateIds.Clear();
        foreach (var playerId in _playerAnimationImages.Keys)
        {
            if (!_activePlayerRenderStateIds.Contains(playerId))
            {
                _stalePlayerRenderStateIds.Add(playerId);
            }
        }

        foreach (var playerId in _stalePlayerRenderStateIds)
        {
            _playerAnimationImages.Remove(playerId);
            _playerWeaponFlashTimes.Remove(playerId);
            _playerPreviousAmmoCounts.Remove(playerId);
            _playerPreviousCooldownTicks.Remove(playerId);
            _playerPreviousRenderPositions.Remove(playerId);
            _playerPreviousRenderSampleTimes.Remove(playerId);
        }
    }

    private float GetPlayerAnimationElapsedSeconds()
    {
        return MathF.Max(0f, _clientUpdateElapsedSeconds);
    }

    private static float GetPlayerAnimationSourceStepSpeed(float speedPerSecond)
    {
        return MathF.Abs(speedPerSecond) / LegacyMovementModel.SourceTicksPerSecond;
    }

    private static float GetPlayerAnimationAdvance(float speedPerSecond, float elapsedSeconds)
    {
        return elapsedSeconds <= 0f
            ? 0f
            : MathF.Abs(speedPerSecond) * elapsedSeconds / 20f;
    }

    private Vector2 SampleObservedRenderVelocity(PlayerEntity player)
    {
        var playerStateKey = GetPlayerStateKey(player);
        var currentPosition = GetRenderPosition(player, allowInterpolation: !ReferenceEquals(player, _world.LocalPlayer));
        var currentTimeSeconds = _networkInterpolationClockSeconds;
        if (!_playerPreviousRenderPositions.TryGetValue(playerStateKey, out var previousPosition)
            || !_playerPreviousRenderSampleTimes.TryGetValue(playerStateKey, out var previousTimeSeconds))
        {
            _playerPreviousRenderPositions[playerStateKey] = currentPosition;
            _playerPreviousRenderSampleTimes[playerStateKey] = currentTimeSeconds;
            return Vector2.Zero;
        }

        _playerPreviousRenderPositions[playerStateKey] = currentPosition;
        _playerPreviousRenderSampleTimes[playerStateKey] = currentTimeSeconds;

        var elapsedSeconds = currentTimeSeconds - previousTimeSeconds;
        if (elapsedSeconds <= 0.0001d)
        {
            return Vector2.Zero;
        }

        return (currentPosition - previousPosition) / (float)elapsedSeconds;
    }

    private float GetPlayerRenderHorizontalSpeed(PlayerEntity player, Vector2 observedRenderVelocity)
    {
        if (_networkClient.IsConnected && ReferenceEquals(player, _world.LocalPlayer))
        {
            if (_hasPredictedLocalPlayerPosition)
            {
                return MathF.Abs(observedRenderVelocity.X) > MathF.Abs(_predictedLocalPlayerVelocity.X)
                    ? observedRenderVelocity.X
                    : _predictedLocalPlayerVelocity.X;
            }

            return observedRenderVelocity.X;
        }

        return player.HorizontalSpeed;
    }

    private float GetPlayerRenderVerticalSpeed(PlayerEntity player, Vector2 observedRenderVelocity)
    {
        if (_networkClient.IsConnected && ReferenceEquals(player, _world.LocalPlayer))
        {
            if (_hasPredictedLocalPlayerPosition)
            {
                return MathF.Abs(observedRenderVelocity.Y) > MathF.Abs(_predictedLocalPlayerVelocity.Y)
                    ? observedRenderVelocity.Y
                    : _predictedLocalPlayerVelocity.Y;
            }

            return observedRenderVelocity.Y;
        }

        return player.VerticalSpeed;
    }

    private bool GetPlayerRenderIsGrounded(PlayerEntity player)
    {
        return _networkClient.IsConnected
            && ReferenceEquals(player, _world.LocalPlayer)
            && _hasPredictedLocalPlayerPosition
                ? _predictedLocalPlayerGrounded
                : player.IsGrounded;
    }
}

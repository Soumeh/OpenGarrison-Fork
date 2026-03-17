using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private const int GeneratorMaxHealth = 4000;

    public IReadOnlyList<GeneratorState> Generators => _generators;

    public GeneratorState? GetGenerator(PlayerTeam team)
    {
        for (var index = 0; index < _generators.Count; index += 1)
        {
            if (_generators[index].Team == team)
            {
                return _generators[index];
            }
        }

        return null;
    }

    private void ResetGeneratorStateForNewRound()
    {
        _generators.Clear();

        var generatorMarkers = Level.GetRoomObjects(RoomObjectType.Generator);
        for (var index = 0; index < generatorMarkers.Count; index += 1)
        {
            var marker = generatorMarkers[index];
            if (!marker.Team.HasValue)
            {
                continue;
            }

            _generators.Add(new GeneratorState(marker.Team.Value, marker, GeneratorMaxHealth));
        }
    }

    private static void UpdateGeneratorState()
    {
        // Generator objectives are passive. Damage resolution happens in the combat systems.
    }

    private void AdvanceGeneratorMatchState()
    {
        if (MatchState.IsEnded)
        {
            return;
        }

        var capWinner = GetCapLimitWinner();
        if (capWinner.HasValue)
        {
            MatchState = MatchState with { Phase = MatchPhase.Ended, WinnerTeam = capWinner };
            QueuePendingMapChange();
            return;
        }

        if (MatchState.TimeRemainingTicks > 0)
        {
            MatchState = MatchState with { TimeRemainingTicks = MatchState.TimeRemainingTicks - 1 };
            if (MatchState.TimeRemainingTicks > 0)
            {
                return;
            }
        }

        MatchState = MatchState with { Phase = MatchPhase.Ended, WinnerTeam = GetHigherCapWinner() };
        QueuePendingMapChange();
    }

    private bool TryDamageGenerator(PlayerTeam targetTeam, float damage)
    {
        var generator = GetGenerator(targetTeam);
        if (generator is null || generator.IsDestroyed)
        {
            return false;
        }

        var destroyed = generator.ApplyDamage(damage);
        if (!destroyed)
        {
            return false;
        }

        HandleGeneratorDestroyed(generator);
        return true;
    }

    private void HandleGeneratorDestroyed(GeneratorState generator)
    {
        if (MatchState.IsEnded)
        {
            return;
        }

        var winner = GetOpposingTeam(generator.Team);
        if (winner == PlayerTeam.Red)
        {
            RedCaps += 1;
        }
        else
        {
            BlueCaps += 1;
        }

        RegisterWorldSoundEvent("ExplosionSnd", generator.Marker.CenterX, generator.Marker.CenterY);
        RegisterWorldSoundEvent("RevolverSnd", generator.Marker.CenterX, generator.Marker.CenterY);
        RegisterWorldSoundEvent("CPBeginCapSnd", generator.Marker.CenterX, generator.Marker.CenterY);
        RegisterVisualEffect("Explosion", generator.Marker.CenterX, generator.Marker.CenterY, count: 2);

        MatchState = MatchState with { Phase = MatchPhase.Ended, WinnerTeam = winner };
        QueuePendingMapChange();
    }

    private void ApplySnapshotGenerators(SnapshotMessage snapshot)
    {
        if ((GameModeKind)snapshot.GameMode != GameModeKind.Generator)
        {
            _generators.Clear();
            return;
        }

        ResetGeneratorStateForNewRound();
        if (_generators.Count == 0 || snapshot.Generators.Count == 0)
        {
            return;
        }

        for (var index = 0; index < snapshot.Generators.Count; index += 1)
        {
            var generatorState = snapshot.Generators[index];
            var target = GetGenerator((PlayerTeam)generatorState.Team);
            target?.SetHealth(generatorState.Health);
        }
    }

    internal GeneratorState? CombatTestGetGenerator(PlayerTeam team)
        => GetGenerator(team);

    internal void CombatTestSetGeneratorHealth(PlayerTeam team, int health)
    {
        var generator = GetGenerator(team);
        generator?.SetHealth(health);
    }

    internal bool CombatTestDamageGenerator(PlayerTeam team, float damage)
        => TryDamageGenerator(team, damage);
}

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{

    private void AdvanceShots()
    {
        for (var shotIndex = _shots.Count - 1; shotIndex >= 0; shotIndex -= 1)
        {
            var shot = _shots[shotIndex];
            shot.AdvanceOneTick();
            var movementX = shot.X - shot.PreviousX;
            var movementY = shot.Y - shot.PreviousY;
            var movementDistance = MathF.Sqrt((movementX * movementX) + (movementY * movementY));
            if (movementDistance <= 0.0001f)
            {
                if (shot.IsExpired)
                {
                    RemoveShotAt(shotIndex);
                }

                continue;
            }

            var directionX = movementX / movementDistance;
            var directionY = movementY / movementDistance;
            var hit = GetNearestShotHit(shot, directionX, directionY, movementDistance);
            if (hit.HasValue)
            {
                var hitResult = hit.Value;
                shot.MoveTo(hitResult.HitX, hitResult.HitY);
                RegisterCombatTrace(shot.PreviousX, shot.PreviousY, directionX, directionY, hitResult.Distance, hitResult.HitPlayer is not null);
                if (hitResult.HitPlayer is not null)
                {
                    RegisterBloodEffect(hitResult.HitPlayer.X, hitResult.HitPlayer.Y, MathF.Atan2(directionY, directionX) * (180f / MathF.PI) - 180f);
                    if (hitResult.HitPlayer.ApplyDamage(ShotProjectileEntity.DamagePerHit, PlayerEntity.SpyDamageRevealAlpha))
                    {
                        KillPlayer(hitResult.HitPlayer, killer: FindPlayerById(shot.OwnerId), weaponSpriteName: GetKillFeedWeaponSprite(FindPlayerById(shot.OwnerId)));
                    }
                }
                else if (hitResult.HitSentry is not null && hitResult.HitSentry.ApplyDamage(ShotProjectileEntity.DamagePerHit))
                {
                    DestroySentry(hitResult.HitSentry);
                }
                else if (hitResult.HitGenerator is not null)
                {
                    TryDamageGenerator(hitResult.HitGenerator.Team, ShotProjectileEntity.DamagePerHit);
                }

                shot.Destroy();
            }
            else
            {
                RegisterCombatTrace(shot.PreviousX, shot.PreviousY, directionX, directionY, movementDistance, false);
            }

            if (shot.IsExpired)
            {
                RemoveShotAt(shotIndex);
            }
        }
    }

    private void AdvanceBubbles()
    {
        for (var bubbleIndex = _bubbles.Count - 1; bubbleIndex >= 0; bubbleIndex -= 1)
        {
            var bubble = _bubbles[bubbleIndex];
            var owner = FindPlayerById(bubble.OwnerId);
            if (owner is null || !owner.IsAlive)
            {
                RemoveBubbleAt(bubbleIndex);
                continue;
            }

            bubble.AdvanceOneTick(owner.X, owner.Y, owner.HorizontalSpeed * (float)Config.FixedDeltaSeconds, owner.VerticalSpeed * (float)Config.FixedDeltaSeconds, owner.AimDirectionDegrees);
            if (bubble.IsExpired || DistanceBetween(bubble.X, bubble.Y, owner.X, owner.Y) > BubbleProjectileEntity.MaxDistanceFromOwner)
            {
                RemoveBubbleAt(bubbleIndex);
                continue;
            }

            if (IsBubbleTouchingEnvironment(bubble))
            {
                bubble.Bounce();
            }

            var touchedEnemy = false;
            foreach (var player in EnumerateSimulatedPlayers())
            {
                if (!player.IsAlive || player.Team == bubble.Team || player.Id == bubble.OwnerId || !CircleIntersectsPlayer(bubble.X, bubble.Y, BubbleProjectileEntity.Radius, player))
                {
                    continue;
                }

                touchedEnemy = true;
                if (player.ApplyContinuousDamage(BubbleProjectileEntity.DamagePerTouch))
                {
                    KillPlayer(player, killer: owner, weaponSpriteName: "BladeS");
                }
            }

            if (touchedEnemy)
            {
                bubble.OnCharacterHit();
            }

            if (TryDestroyBubbleOnProjectileCollision(bubble) || TryDamageBubbleStructureTarget(bubble, owner))
            {
                RemoveBubbleAt(bubbleIndex);
                continue;
            }

            if (bubble.IsExpired)
            {
                RemoveBubbleAt(bubbleIndex);
            }
        }
    }

    private void AdvanceBlades()
    {
        for (var bladeIndex = _blades.Count - 1; bladeIndex >= 0; bladeIndex -= 1)
        {
            var blade = _blades[bladeIndex];
            blade.AdvanceOneTick();
            var movementX = blade.X - blade.PreviousX;
            var movementY = blade.Y - blade.PreviousY;
            var movementDistance = MathF.Sqrt((movementX * movementX) + (movementY * movementY));
            if (movementDistance > 0.0001f)
            {
                var directionX = movementX / movementDistance;
                var directionY = movementY / movementDistance;
                var hit = GetNearestBladeHit(blade, directionX, directionY, movementDistance);
                if (hit.HasValue)
                {
                    var hitResult = hit.Value;
                    blade.MoveTo(hitResult.HitX, hitResult.HitY);
                    RegisterCombatTrace(blade.PreviousX, blade.PreviousY, directionX, directionY, hitResult.Distance, hitResult.HitPlayer is not null);
                    if (hitResult.HitPlayer is not null)
                    {
                        RegisterBloodEffect(hitResult.HitPlayer.X, hitResult.HitPlayer.Y, MathF.Atan2(directionY, directionX) * (180f / MathF.PI) - 180f, 6);
                        hitResult.HitPlayer.AddImpulse(blade.VelocityX * 0.4f, blade.VelocityY * 0.4f);
                        if (hitResult.HitPlayer.ApplyDamage(blade.HitDamage, PlayerEntity.SpyDamageRevealAlpha))
                        {
                            KillPlayer(hitResult.HitPlayer, killer: FindPlayerById(blade.OwnerId), weaponSpriteName: "BladeS");
                        }
                    }
                    else if (hitResult.HitSentry is not null && hitResult.HitSentry.ApplyDamage(blade.HitDamage))
                    {
                        DestroySentry(hitResult.HitSentry);
                    }
                    else if (hitResult.HitGenerator is not null)
                    {
                        TryDamageGenerator(hitResult.HitGenerator.Team, blade.HitDamage);
                    }

                    blade.Destroy();
                }
            }

            if (TryCutBubbleWithBlade(blade))
            {
                blade.Destroy();
            }

            if (blade.IsExpired)
            {
                RemoveBladeAt(bladeIndex);
            }
        }
    }

    private void AdvanceNeedles()
    {
        for (var needleIndex = _needles.Count - 1; needleIndex >= 0; needleIndex -= 1)
        {
            var needle = _needles[needleIndex];
            needle.AdvanceOneTick();
            var movementX = needle.X - needle.PreviousX;
            var movementY = needle.Y - needle.PreviousY;
            var movementDistance = MathF.Sqrt((movementX * movementX) + (movementY * movementY));
            if (movementDistance <= 0.0001f)
            {
                if (needle.IsExpired)
                {
                    RemoveNeedleAt(needleIndex);
                }

                continue;
            }

            var directionX = movementX / movementDistance;
            var directionY = movementY / movementDistance;
            var hit = GetNearestNeedleHit(needle, directionX, directionY, movementDistance);
            if (hit.HasValue)
            {
                var hitResult = hit.Value;
                needle.MoveTo(hitResult.HitX, hitResult.HitY);
                RegisterCombatTrace(needle.PreviousX, needle.PreviousY, directionX, directionY, hitResult.Distance, hitResult.HitPlayer is not null);
                if (hitResult.HitPlayer is not null)
                {
                    RegisterBloodEffect(hitResult.HitPlayer.X, hitResult.HitPlayer.Y, MathF.Atan2(directionY, directionX) * (180f / MathF.PI) - 180f);
                    if (hitResult.HitPlayer.ApplyDamage(NeedleProjectileEntity.DamagePerHit, PlayerEntity.SpyDamageRevealAlpha))
                    {
                        KillPlayer(hitResult.HitPlayer, killer: FindPlayerById(needle.OwnerId), weaponSpriteName: "MedigunS");
                    }
                }
                else if (hitResult.HitSentry is not null && hitResult.HitSentry.ApplyDamage(NeedleProjectileEntity.DamagePerHit))
                {
                    DestroySentry(hitResult.HitSentry);
                }
                else if (hitResult.HitGenerator is not null)
                {
                    TryDamageGenerator(hitResult.HitGenerator.Team, NeedleProjectileEntity.DamagePerHit);
                }

                needle.Destroy();
            }
            else
            {
                RegisterCombatTrace(needle.PreviousX, needle.PreviousY, directionX, directionY, movementDistance, false);
            }

            if (needle.IsExpired)
            {
                RemoveNeedleAt(needleIndex);
            }
        }
    }

    private void AdvanceRevolverShots()
    {
        for (var shotIndex = _revolverShots.Count - 1; shotIndex >= 0; shotIndex -= 1)
        {
            var shot = _revolverShots[shotIndex];
            shot.AdvanceOneTick();
            var movementX = shot.X - shot.PreviousX;
            var movementY = shot.Y - shot.PreviousY;
            var movementDistance = MathF.Sqrt((movementX * movementX) + (movementY * movementY));
            if (movementDistance <= 0.0001f)
            {
                if (shot.IsExpired)
                {
                    RemoveRevolverShotAt(shotIndex);
                }

                continue;
            }

            var directionX = movementX / movementDistance;
            var directionY = movementY / movementDistance;
            var hit = GetNearestRevolverHit(shot, directionX, directionY, movementDistance);
            if (hit.HasValue)
            {
                var hitResult = hit.Value;
                shot.MoveTo(hitResult.HitX, hitResult.HitY);
                RegisterCombatTrace(shot.PreviousX, shot.PreviousY, directionX, directionY, hitResult.Distance, hitResult.HitPlayer is not null);
                if (hitResult.HitPlayer is not null)
                {
                    RegisterBloodEffect(hitResult.HitPlayer.X, hitResult.HitPlayer.Y, MathF.Atan2(directionY, directionX) * (180f / MathF.PI) - 180f);
                    if (hitResult.HitPlayer.ApplyDamage(RevolverProjectileEntity.DamagePerHit, PlayerEntity.SpyDamageRevealAlpha))
                    {
                        KillPlayer(hitResult.HitPlayer, killer: FindPlayerById(shot.OwnerId), weaponSpriteName: "RevolverS");
                    }
                }
                else if (hitResult.HitSentry is not null && hitResult.HitSentry.ApplyDamage(RevolverProjectileEntity.DamagePerHit))
                {
                    DestroySentry(hitResult.HitSentry);
                }
                else if (hitResult.HitGenerator is not null)
                {
                    TryDamageGenerator(hitResult.HitGenerator.Team, RevolverProjectileEntity.DamagePerHit);
                }

                shot.Destroy();
            }
            else
            {
                RegisterCombatTrace(shot.PreviousX, shot.PreviousY, directionX, directionY, movementDistance, false);
            }

            if (shot.IsExpired)
            {
                RemoveRevolverShotAt(shotIndex);
            }
        }
    }

    private void AdvanceStabAnimations()
    {
        for (var animationIndex = _stabAnimations.Count - 1; animationIndex >= 0; animationIndex -= 1)
        {
            var animation = _stabAnimations[animationIndex];
            var owner = FindPlayerById(animation.OwnerId);
            if (owner is null || !owner.IsAlive || owner.ClassId != PlayerClass.Spy)
            {
                RemoveStabAnimationAt(animationIndex);
                continue;
            }

            animation.AdvanceOneTick(owner.X, owner.Y);
            if (animation.IsExpired)
            {
                RemoveStabAnimationAt(animationIndex);
            }
        }
    }

    private void AdvanceStabMasks()
    {
        for (var maskIndex = _stabMasks.Count - 1; maskIndex >= 0; maskIndex -= 1)
        {
            var mask = _stabMasks[maskIndex];
            var owner = FindPlayerById(mask.OwnerId);
            if (owner is null || !owner.IsAlive || owner.ClassId != PlayerClass.Spy)
            {
                RemoveStabMaskAt(maskIndex);
                continue;
            }

            mask.AdvanceOneTick(owner.X, owner.Y);
            var directionRadians = DegreesToRadians(mask.DirectionDegrees);
            var directionX = MathF.Cos(directionRadians);
            var directionY = MathF.Sin(directionRadians);
            var hit = GetNearestStabHit(mask, directionX, directionY);
            if (hit.HasValue)
            {
                var hitResult = hit.Value;
                RegisterCombatTrace(mask.X, mask.Y, directionX, directionY, hitResult.Distance, hitResult.HitPlayer is not null);
                if (hitResult.HitPlayer is not null)
                {
                    RegisterBloodEffect(hitResult.HitPlayer.X, hitResult.HitPlayer.Y, mask.DirectionDegrees - 180f, 6);
                    if (hitResult.HitPlayer.ApplyDamage(StabMaskEntity.DamagePerHit, PlayerEntity.SpyDamageRevealAlpha))
                    {
                        KillPlayer(hitResult.HitPlayer, killer: owner, weaponSpriteName: "KnifeS");
                    }
                }
                else if (hitResult.HitSentry is not null && hitResult.HitSentry.ApplyDamage(StabMaskEntity.DamagePerHit))
                {
                    DestroySentry(hitResult.HitSentry);
                }

                mask.Destroy();
            }

            if (mask.IsExpired)
            {
                RemoveStabMaskAt(maskIndex);
            }
        }
    }

    private void AdvanceFlames()
    {
        for (var flameIndex = _flames.Count - 1; flameIndex >= 0; flameIndex -= 1)
        {
            var flame = _flames[flameIndex];
            if (flame.IsAttached)
            {
                var attachedPlayer = FindPlayerById(flame.AttachedPlayerId!.Value);
                if (attachedPlayer is null || !attachedPlayer.IsAlive)
                {
                    RemoveFlameAt(flameIndex);
                    continue;
                }

                flame.AdvanceOneTick();
                if (flame.ApplyAttachedBurn(attachedPlayer) && attachedPlayer.IsAlive)
                {
                    KillPlayer(attachedPlayer, killer: FindPlayerById(flame.OwnerId), weaponSpriteName: "FlamethrowerS");
                }

                if (flame.IsExpired)
                {
                    RemoveFlameAt(flameIndex);
                }

                continue;
            }

            flame.AdvanceOneTick();
            var movementX = flame.X - flame.PreviousX;
            var movementY = flame.Y - flame.PreviousY;
            var movementDistance = MathF.Sqrt((movementX * movementX) + (movementY * movementY));
            if (movementDistance <= 0.0001f)
            {
                if (flame.IsExpired)
                {
                    RemoveFlameAt(flameIndex);
                }

                continue;
            }

            var directionX = movementX / movementDistance;
            var directionY = movementY / movementDistance;
            var hit = GetNearestFlameHit(flame, directionX, directionY, movementDistance);
            if (hit.HasValue)
            {
                var hitResult = hit.Value;
                flame.MoveTo(hitResult.HitX, hitResult.HitY);
                if (hitResult.HitPlayer is not null)
                {
                    var hitPlayer = hitResult.HitPlayer;
                    var playerDied = hitPlayer.ApplyDamage(FlameProjectileEntity.DirectHitDamage);
                    if (playerDied)
                    {
                        KillPlayer(hitPlayer, killer: FindPlayerById(flame.OwnerId), weaponSpriteName: "FlamethrowerS");
                    }
                    else if (hitPlayer.ClassId != PlayerClass.Pyro && CountAttachedFlames(hitPlayer.Id) <= 7)
                    {
                        flame.AttachToPlayer(hitPlayer);
                        RegisterCombatTrace(flame.PreviousX, flame.PreviousY, directionX, directionY, hitResult.Distance, true);
                        continue;
                    }
                }
                else if (hitResult.HitSentry is not null && hitResult.HitSentry.ApplyDamage(FlameProjectileEntity.DirectHitDamage))
                {
                    DestroySentry(hitResult.HitSentry);
                }
                else if (hitResult.HitGenerator is not null)
                {
                    TryDamageGenerator(hitResult.HitGenerator.Team, FlameProjectileEntity.DirectHitDamage);
                }

                RegisterCombatTrace(flame.PreviousX, flame.PreviousY, directionX, directionY, hitResult.Distance, hitResult.HitPlayer is not null);
                flame.Destroy();
            }
            else
            {
                RegisterCombatTrace(flame.PreviousX, flame.PreviousY, directionX, directionY, movementDistance, false);
            }

            if (flame.IsExpired)
            {
                RemoveFlameAt(flameIndex);
            }
        }
    }

    private void AdvanceRockets()
    {
        for (var rocketIndex = _rockets.Count - 1; rocketIndex >= 0; rocketIndex -= 1)
        {
            var rocket = _rockets[rocketIndex];
            rocket.AdvanceOneTick();
            var movementX = rocket.X - rocket.PreviousX;
            var movementY = rocket.Y - rocket.PreviousY;
            var movementDistance = MathF.Sqrt((movementX * movementX) + (movementY * movementY));
            if (movementDistance <= 0.0001f)
            {
                if (rocket.IsExpired)
                {
                    RemoveRocketAt(rocketIndex);
                }

                continue;
            }

            var directionX = movementX / movementDistance;
            var directionY = movementY / movementDistance;
            var hit = GetNearestRocketHit(rocket, directionX, directionY, movementDistance);
            if (hit.HasValue)
            {
                var hitResult = hit.Value;
                rocket.MoveTo(hitResult.HitX, hitResult.HitY);
                RegisterCombatTrace(rocket.PreviousX, rocket.PreviousY, directionX, directionY, hitResult.Distance, hitResult.HitPlayer is not null);
                ExplodeRocket(rocket, hitResult.HitPlayer, hitResult.HitSentry, hitResult.HitGenerator);
            }
            else if (rocket.IsExpired)
            {
                RemoveRocketAt(rocketIndex);
            }
        }
    }

    private void AdvanceMines()
    {
        for (var mineIndex = _mines.Count - 1; mineIndex >= 0; mineIndex -= 1)
        {
            var mine = _mines[mineIndex];
            mine.AdvanceOneTick();
            if (mine.IsStickied)
            {
                continue;
            }

            var movementX = mine.X - mine.PreviousX;
            var movementY = mine.Y - mine.PreviousY;
            var movementDistance = MathF.Sqrt((movementX * movementX) + (movementY * movementY));
            if (movementDistance <= 0.0001f)
            {
                continue;
            }

            var directionX = movementX / movementDistance;
            var directionY = movementY / movementDistance;
            var hit = GetNearestMineHit(mine, directionX, directionY, movementDistance);
            if (!hit.HasValue)
            {
                continue;
            }

            var hitResult = hit.Value;
            mine.MoveTo(hitResult.HitX, hitResult.HitY);
            if (hitResult.DestroyOnHit)
            {
                RemoveMineAt(mineIndex);
                continue;
            }

            mine.Stick();
        }
    }

    private void RemoveShotAt(int shotIndex)
    {
        var shot = _shots[shotIndex];
        _entities.Remove(shot.Id);
        _shots.RemoveAt(shotIndex);
    }

    private void RemoveBubbleAt(int bubbleIndex)
    {
        var bubble = _bubbles[bubbleIndex];
        if (FindPlayerById(bubble.OwnerId) is { } owner)
        {
            owner.DecrementQuoteBubbleCount();
        }

        _entities.Remove(bubble.Id);
        _bubbles.RemoveAt(bubbleIndex);
    }

    private void RemoveBladeAt(int bladeIndex)
    {
        var blade = _blades[bladeIndex];
        if (FindPlayerById(blade.OwnerId) is { } owner)
        {
            owner.DecrementQuoteBladeCount();
        }

        _entities.Remove(blade.Id);
        _blades.RemoveAt(bladeIndex);
    }

    private void RemoveNeedleAt(int needleIndex)
    {
        var needle = _needles[needleIndex];
        _entities.Remove(needle.Id);
        _needles.RemoveAt(needleIndex);
    }

    private void RemoveRevolverShotAt(int shotIndex)
    {
        var shot = _revolverShots[shotIndex];
        _entities.Remove(shot.Id);
        _revolverShots.RemoveAt(shotIndex);
    }

    private void RemoveStabAnimationAt(int animationIndex)
    {
        var animation = _stabAnimations[animationIndex];
        _entities.Remove(animation.Id);
        _stabAnimations.RemoveAt(animationIndex);
    }

    private void RemoveStabMaskAt(int maskIndex)
    {
        var mask = _stabMasks[maskIndex];
        _entities.Remove(mask.Id);
        _stabMasks.RemoveAt(maskIndex);
    }

    private void RemoveOwnedSpyArtifacts(int ownerId)
    {
        for (var animationIndex = _stabAnimations.Count - 1; animationIndex >= 0; animationIndex -= 1)
        {
            if (_stabAnimations[animationIndex].OwnerId == ownerId)
            {
                RemoveStabAnimationAt(animationIndex);
            }
        }

        for (var maskIndex = _stabMasks.Count - 1; maskIndex >= 0; maskIndex -= 1)
        {
            if (_stabMasks[maskIndex].OwnerId == ownerId)
            {
                RemoveStabMaskAt(maskIndex);
            }
        }
    }

    private void RemoveFlameAt(int flameIndex)
    {
        var flame = _flames[flameIndex];
        _entities.Remove(flame.Id);
        _flames.RemoveAt(flameIndex);
    }

    private void RemoveRocketAt(int rocketIndex)
    {
        var rocket = _rockets[rocketIndex];
        _entities.Remove(rocket.Id);
        _rockets.RemoveAt(rocketIndex);
    }

    private void RemoveMineAt(int mineIndex)
    {
        var mine = _mines[mineIndex];
        _entities.Remove(mine.Id);
        _mines.RemoveAt(mineIndex);
    }

    private void RemoveOwnedSentries(int ownerId)
    {
        for (var sentryIndex = _sentries.Count - 1; sentryIndex >= 0; sentryIndex -= 1)
        {
            if (_sentries[sentryIndex].OwnerPlayerId == ownerId)
            {
                DestroySentry(_sentries[sentryIndex]);
            }
        }
    }

    private void RemoveOwnedMines(int ownerId)
    {
        for (var mineIndex = _mines.Count - 1; mineIndex >= 0; mineIndex -= 1)
        {
            if (_mines[mineIndex].OwnerId == ownerId)
            {
                RemoveMineAt(mineIndex);
            }
        }
    }

    private void RemoveOwnedProjectiles(int ownerId)
    {
        for (var shotIndex = _shots.Count - 1; shotIndex >= 0; shotIndex -= 1)
        {
            if (_shots[shotIndex].OwnerId == ownerId)
            {
                RemoveShotAt(shotIndex);
            }
        }

        for (var needleIndex = _needles.Count - 1; needleIndex >= 0; needleIndex -= 1)
        {
            if (_needles[needleIndex].OwnerId == ownerId)
            {
                RemoveNeedleAt(needleIndex);
            }
        }

        for (var shotIndex = _revolverShots.Count - 1; shotIndex >= 0; shotIndex -= 1)
        {
            if (_revolverShots[shotIndex].OwnerId == ownerId)
            {
                RemoveRevolverShotAt(shotIndex);
            }
        }

        for (var bubbleIndex = _bubbles.Count - 1; bubbleIndex >= 0; bubbleIndex -= 1)
        {
            if (_bubbles[bubbleIndex].OwnerId == ownerId)
            {
                RemoveBubbleAt(bubbleIndex);
            }
        }

        for (var bladeIndex = _blades.Count - 1; bladeIndex >= 0; bladeIndex -= 1)
        {
            if (_blades[bladeIndex].OwnerId == ownerId)
            {
                RemoveBladeAt(bladeIndex);
            }
        }

        for (var rocketIndex = _rockets.Count - 1; rocketIndex >= 0; rocketIndex -= 1)
        {
            if (_rockets[rocketIndex].OwnerId == ownerId)
            {
                RemoveRocketAt(rocketIndex);
            }
        }

        for (var flameIndex = _flames.Count - 1; flameIndex >= 0; flameIndex -= 1)
        {
            if (_flames[flameIndex].OwnerId == ownerId)
            {
                RemoveFlameAt(flameIndex);
            }
        }
    }

    private int CountAttachedFlames(int playerId)
    {
        var count = 0;
        foreach (var flame in _flames)
        {
            if (flame.AttachedPlayerId == playerId)
            {
                count += 1;
            }
        }

        return count;
    }

    private int CountOwnedMines(int ownerId)
    {
        var count = 0;
        foreach (var mine in _mines)
        {
            if (mine.OwnerId == ownerId)
            {
                count += 1;
            }
        }

        return count;
    }

    private bool IsBubbleTouchingEnvironment(BubbleProjectileEntity bubble)
    {
        foreach (var solid in Level.Solids)
        {
            if (CircleIntersectsRectangle(bubble.X, bubble.Y, BubbleProjectileEntity.Radius, solid.Left, solid.Top, solid.Right, solid.Bottom))
            {
                return true;
            }
        }

        foreach (var roomObject in Level.RoomObjects)
        {
            if (!IsBubbleBlockingRoomObject(roomObject))
            {
                continue;
            }

            if (CircleIntersectsRectangle(bubble.X, bubble.Y, BubbleProjectileEntity.Radius, roomObject.Left, roomObject.Top, roomObject.Right, roomObject.Bottom))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsBubbleBlockingRoomObject(RoomObjectMarker roomObject)
    {
        return roomObject.Type switch
        {
            RoomObjectType.TeamGate => true,
            RoomObjectType.ControlPointSetupGate => Level.ControlPointSetupGatesActive,
            RoomObjectType.BulletWall => true,
            _ => false,
        };
    }

    private bool TryDamageBubbleStructureTarget(BubbleProjectileEntity bubble, PlayerEntity owner)
    {
        foreach (var sentry in _sentries)
        {
            if (sentry.Team == bubble.Team || !CircleIntersectsRectangle(bubble.X, bubble.Y, BubbleProjectileEntity.Radius, sentry.X - 12f, sentry.Y - 12f, sentry.X + 12f, sentry.Y + 12f))
            {
                continue;
            }

            if (sentry.ApplyDamage(1))
            {
                DestroySentry(sentry);
            }

            return true;
        }

        for (var index = 0; index < _generators.Count; index += 1)
        {
            var generator = _generators[index];
            if (generator.Team == bubble.Team
                || generator.IsDestroyed
                || !CircleIntersectsRectangle(bubble.X, bubble.Y, BubbleProjectileEntity.Radius, generator.Marker.Left, generator.Marker.Top, generator.Marker.Right, generator.Marker.Bottom))
            {
                continue;
            }

            TryDamageGenerator(generator.Team, 1f);
            return true;
        }

        return false;
    }

    private bool TryDestroyBubbleOnProjectileCollision(BubbleProjectileEntity bubble)
    {
        for (var rocketIndex = _rockets.Count - 1; rocketIndex >= 0; rocketIndex -= 1)
        {
            var rocket = _rockets[rocketIndex];
            if (rocket.Team != bubble.Team && DistanceBetween(bubble.X, bubble.Y, rocket.X, rocket.Y) <= 10f)
            {
                RemoveRocketAt(rocketIndex);
                return true;
            }
        }

        for (var mineIndex = _mines.Count - 1; mineIndex >= 0; mineIndex -= 1)
        {
            var mine = _mines[mineIndex];
            if (mine.Team != bubble.Team && DistanceBetween(bubble.X, bubble.Y, mine.X, mine.Y) <= 10f)
            {
                RemoveMineAt(mineIndex);
                return true;
            }
        }

        for (var flameIndex = _flames.Count - 1; flameIndex >= 0; flameIndex -= 1)
        {
            var flame = _flames[flameIndex];
            if (flame.Team != bubble.Team && DistanceBetween(bubble.X, bubble.Y, flame.X, flame.Y) <= 8f)
            {
                RemoveFlameAt(flameIndex);
                return true;
            }
        }

        for (var shotIndex = _shots.Count - 1; shotIndex >= 0; shotIndex -= 1)
        {
            if (_shots[shotIndex].Team != bubble.Team && DistanceBetween(bubble.X, bubble.Y, _shots[shotIndex].X, _shots[shotIndex].Y) <= 8f)
            {
                RemoveShotAt(shotIndex);
                return true;
            }
        }

        for (var needleIndex = _needles.Count - 1; needleIndex >= 0; needleIndex -= 1)
        {
            if (_needles[needleIndex].Team != bubble.Team && DistanceBetween(bubble.X, bubble.Y, _needles[needleIndex].X, _needles[needleIndex].Y) <= 8f)
            {
                RemoveNeedleAt(needleIndex);
                return true;
            }
        }

        for (var revolverIndex = _revolverShots.Count - 1; revolverIndex >= 0; revolverIndex -= 1)
        {
            if (_revolverShots[revolverIndex].Team != bubble.Team && DistanceBetween(bubble.X, bubble.Y, _revolverShots[revolverIndex].X, _revolverShots[revolverIndex].Y) <= 8f)
            {
                RemoveRevolverShotAt(revolverIndex);
                return true;
            }
        }

        for (var bladeIndex = _blades.Count - 1; bladeIndex >= 0; bladeIndex -= 1)
        {
            if (_blades[bladeIndex].Team != bubble.Team && DistanceBetween(bubble.X, bubble.Y, _blades[bladeIndex].X, _blades[bladeIndex].Y) <= 10f)
            {
                RemoveBladeAt(bladeIndex);
                return true;
            }
        }

        return false;
    }

    private bool TryCutBubbleWithBlade(BladeProjectileEntity blade)
    {
        for (var bubbleIndex = _bubbles.Count - 1; bubbleIndex >= 0; bubbleIndex -= 1)
        {
            if (_bubbles[bubbleIndex].Team == blade.Team)
            {
                continue;
            }

            if (DistanceBetween(blade.X, blade.Y, _bubbles[bubbleIndex].X, _bubbles[bubbleIndex].Y) > 10f)
            {
                continue;
            }

            RemoveBubbleAt(bubbleIndex);
            return true;
        }

        return false;
    }

    private static bool CircleIntersectsPlayer(float circleX, float circleY, float radius, PlayerEntity player)
    {
        return CircleIntersectsRectangle(
            circleX,
            circleY,
            radius,
            player.X - (player.Width / 2f),
            player.Y - (player.Height / 2f),
            player.X + (player.Width / 2f),
            player.Y + (player.Height / 2f));
    }

    private static bool CircleIntersectsRectangle(float circleX, float circleY, float radius, float left, float top, float right, float bottom)
    {
        var closestX = float.Clamp(circleX, left, right);
        var closestY = float.Clamp(circleY, top, bottom);
        var deltaX = circleX - closestX;
        var deltaY = circleY - closestY;
        return (deltaX * deltaX) + (deltaY * deltaY) <= radius * radius;
    }
}


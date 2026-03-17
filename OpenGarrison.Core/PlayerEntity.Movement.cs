namespace OpenGarrison.Core;

public sealed partial class PlayerEntity
{
    private const int MaxCollisionResolutionIterations = 10;
    private const float CollisionMoveStep = 1f;

    public void TeleportTo(float x, float y)
    {
        X = x;
        Y = y;
        HorizontalSpeed = 0f;
        VerticalSpeed = 0f;
        IsGrounded = false;
        LegacyStateTickAccumulator = 0f;
        MovementState = LegacyMovementState.None;
    }

    public bool Advance(PlayerInputSnapshot input, bool jumpPressed, SimpleLevel level, PlayerTeam team, double deltaSeconds)
    {
        var dt = (float)deltaSeconds;
        UpdateAimDirection(input);

        if (!IsAlive)
        {
            return false;
        }

        var legacyStateTicks = ConsumeLegacyStateTicks(dt);
        for (var tick = 0; tick < legacyStateTicks; tick += 1)
        {
            if (IntelPickupCooldownTicks > 0)
            {
                IntelPickupCooldownTicks -= 1;
            }

            AdvanceEngineerResources();
            AdvanceWeaponState();
            AdvanceHeavyState();
            AdvanceTauntState();
            AdvanceSniperState();
            AdvanceUberState();
            AdvanceMedicState();
            AdvanceSpyState();
        }

        var canMove = !IsHeavyEating && !IsTaunting;

        var horizontalDirection = 0f;
        if (canMove && input.Left)
        {
            horizontalDirection -= 1f;
        }
        if (canMove && input.Right)
        {
            horizontalDirection += 1f;
        }

        if (horizontalDirection != 0f)
        {
            FacingDirectionX = horizontalDirection;
        }

        HorizontalSpeed = LegacyMovementModel.AdvanceHorizontalSpeed(
            HorizontalSpeed,
            RunPower,
            GetMovementScale(input),
            horizontalDirection,
            MovementState,
            IsCarryingIntel,
            dt);

        var jumped = false;
        if (canMove && jumpPressed)
        {
            jumped = TryJump();
        }

        var movementState = MovementState;
        VerticalSpeed = LegacyMovementModel.AdvanceVerticalSpeed(
            VerticalSpeed,
            CanOccupy(level, team, X, Y + 1f),
            dt,
            ref movementState);
        MovementState = movementState;

        MoveWithCollisions(level, team, HorizontalSpeed * dt, VerticalSpeed * dt);
        ClampTo(level.Bounds);
        return jumped;
    }

    public void ClampTo(WorldBounds bounds)
    {
        X = bounds.ClampX(X, Width);
        if (X <= Width / 2f || X >= bounds.Width - (Width / 2f))
        {
            HorizontalSpeed = 0f;
        }

        var clampedY = bounds.ClampY(Y, Height);
        if (clampedY != Y)
        {
            if (VerticalSpeed > 0f)
            {
                IsGrounded = true;
            }

            Y = clampedY;
            VerticalSpeed = 0f;
            MovementState = LegacyMovementState.None;
        }
    }

    private void MoveWithCollisions(SimpleLevel level, PlayerTeam team, float moveX, float moveY)
    {
        if (!float.IsFinite(moveX) || !float.IsFinite(moveY))
        {
            HorizontalSpeed = 0f;
            VerticalSpeed = 0f;
            return;
        }

        NudgeOutsideBlockingGeometry(level, team);

        var remainingX = moveX;
        var remainingY = moveY;
        IsGrounded = false;

        for (var iteration = 0; iteration < MaxCollisionResolutionIterations && (MathF.Abs(remainingX) >= 1f || MathF.Abs(remainingY) >= 1f); iteration += 1)
        {
            var previousX = X;
            var previousY = Y;
            MoveContact(level, team, remainingX, remainingY);
            remainingX -= X - previousX;
            remainingY -= Y - previousY;

            var collisionRectified = false;
            if (remainingY != 0f && !CanOccupy(level, team, X, Y + MathF.Sign(remainingY)))
            {
                if (remainingY > 0f)
                {
                    IsGrounded = true;
                    RemainingAirJumps = MaxAirJumps;
                }

                VerticalSpeed = 0f;
                MovementState = LegacyMovementState.None;
                remainingY = 0f;
                collisionRectified = true;
            }

            if (remainingX != 0f && !CanOccupy(level, team, X + MathF.Sign(remainingX), Y))
            {
                if (TryStepUpForObstacle(level, team, MathF.Sign(remainingX)))
                {
                    collisionRectified = true;
                }
                else
                {
                    HorizontalSpeed = 0f;
                    remainingX = 0f;
                    collisionRectified = true;
                }
            }

            if (!collisionRectified && (MathF.Abs(remainingX) >= 1f || MathF.Abs(remainingY) >= 1f))
            {
                VerticalSpeed = 0f;
                remainingY = 0f;
            }
        }

        TryApplyResidualMovement(level, team, remainingX, remainingY);
        RefreshGroundSupport(level, team);
    }

    private float GetMovementScale(PlayerInputSnapshot input)
    {
        if (IsHeavyEating || IsTaunting)
        {
            return 0f;
        }

        if (ClassId == PlayerClass.Spy && SpyBackstabVisualTicksRemaining > 0)
        {
            return 0f;
        }

        if (ClassId == PlayerClass.Sniper && IsSniperScoped)
        {
            return SniperScopedMoveScale;
        }

        if (ClassId == PlayerClass.Heavy && input.FirePrimary)
        {
            return HeavyPrimaryMoveScale;
        }

        return 1f;
    }

    private float GetJumpScale()
    {
        if (ClassId == PlayerClass.Sniper && IsSniperScoped)
        {
            return SniperScopedJumpScale;
        }

        if (ClassId == PlayerClass.Spy && SpyBackstabVisualTicksRemaining > 0)
        {
            return 0f;
        }

        return 1f;
    }

    public bool IntersectsMarker(float markerX, float markerY, float markerWidth, float markerHeight)
    {
        var left = X - (Width / 2f);
        var right = X + (Width / 2f);
        var top = Y - (Height / 2f);
        var bottom = Y + (Height / 2f);
        var markerLeft = markerX - (markerWidth / 2f);
        var markerRight = markerX + (markerWidth / 2f);
        var markerTop = markerY - (markerHeight / 2f);
        var markerBottom = markerY + (markerHeight / 2f);

        return left < markerRight
            && right > markerLeft
            && top < markerBottom
            && bottom > markerTop;
    }

    public bool IsInsideBlockingTeamGate(SimpleLevel level, PlayerTeam team)
    {
        foreach (var gate in level.GetBlockingTeamGates(team, IsCarryingIntel))
        {
            if (Intersects(gate))
            {
                return true;
            }
        }

        return false;
    }

    public void AddImpulse(float velocityX, float velocityY)
    {
        HorizontalSpeed += velocityX;
        VerticalSpeed += velocityY;
        if (velocityY < 0f)
        {
            IsGrounded = false;
        }
    }

    private void UpdateAimDirection(PlayerInputSnapshot input)
    {
        var aimDeltaX = input.AimWorldX - X;
        var aimDeltaY = input.AimWorldY - Y;
        if (MathF.Abs(aimDeltaX) <= 0.0001f && MathF.Abs(aimDeltaY) <= 0.0001f)
        {
            AimDirectionDegrees = FacingDirectionX < 0f ? 180f : 0f;
            return;
        }

        AimDirectionDegrees = NormalizeDegrees(MathF.Atan2(aimDeltaY, aimDeltaX) * (180f / MathF.PI));
    }

    private static float NormalizeDegrees(float degrees)
    {
        if (degrees < 0f)
        {
            degrees += 360f;
        }

        return degrees;
    }

    private void MoveContact(SimpleLevel level, PlayerTeam team, float deltaX, float deltaY)
    {
        if (!float.IsFinite(deltaX) || !float.IsFinite(deltaY))
        {
            return;
        }

        var distance = MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
        if (distance <= 0f)
        {
            return;
        }

        var steps = Math.Max(1, (int)MathF.Ceiling(distance / CollisionMoveStep));
        var stepX = deltaX / steps;
        var stepY = deltaY / steps;
        for (var step = 0; step < steps; step += 1)
        {
            var nextX = X + stepX;
            var nextY = Y + stepY;
            if (!CanOccupy(level, team, nextX, nextY))
            {
                break;
            }

            X = nextX;
            Y = nextY;
        }
    }

    private void NudgeOutsideBlockingGeometry(SimpleLevel level, PlayerTeam team)
    {
        if (CanOccupy(level, team, X, Y))
        {
            return;
        }

        for (var offset = 1; offset <= 8; offset += 1)
        {
            if (CanOccupy(level, team, X + offset, Y))
            {
                X += offset;
                return;
            }
        }

        for (var offset = 1; offset <= 16; offset += 1)
        {
            if (CanOccupy(level, team, X - offset, Y))
            {
                X -= offset;
                return;
            }
        }
    }

    private bool Intersects(LevelSolid solid)
    {
        var left = X - (Width / 2f);
        var right = X + (Width / 2f);
        var top = Y - (Height / 2f);
        var bottom = Y + (Height / 2f);

        return left < solid.Right
            && right > solid.Left
            && top < solid.Bottom
            && bottom > solid.Top;
    }

    private bool Intersects(RoomObjectMarker roomObject)
    {
        var left = X - (Width / 2f);
        var right = X + (Width / 2f);
        var top = Y - (Height / 2f);
        var bottom = Y + (Height / 2f);
        var gateLeft = roomObject.Left;
        var gateRight = roomObject.Right;
        var gateTop = roomObject.Top;
        var gateBottom = roomObject.Bottom;

        return left < gateRight
            && right > gateLeft
            && top < gateBottom
            && bottom > gateTop;
    }

    private void TryApplyResidualMovement(SimpleLevel level, PlayerTeam team, float remainingX, float remainingY)
    {
        if (MathF.Abs(remainingX) <= 0f && MathF.Abs(remainingY) <= 0f)
        {
            return;
        }

        if (CanOccupy(level, team, X + remainingX, Y + remainingY))
        {
            X += remainingX;
            Y += remainingY;
            return;
        }

        if (MathF.Abs(remainingX) > 0f && MathF.Abs(remainingY) <= 0f && CanOccupy(level, team, X + remainingX, Y))
        {
            X += remainingX;
            return;
        }

        if (MathF.Abs(remainingY) > 0f && MathF.Abs(remainingX) <= 0f && CanOccupy(level, team, X, Y + remainingY))
        {
            Y += remainingY;
        }
    }

    private void RefreshGroundSupport(SimpleLevel level, PlayerTeam team)
    {
        if (VerticalSpeed < 0f || !CanOccupy(level, team, X, Y))
        {
            return;
        }

        if (CanOccupy(level, team, X, Y + StepSupportEpsilon))
        {
            return;
        }

        IsGrounded = true;
        RemainingAirJumps = MaxAirJumps;
        VerticalSpeed = 0f;
    }

    private bool TryStepUpForObstacle(SimpleLevel level, PlayerTeam team, float horizontalDirection)
    {
        if (horizontalDirection == 0f || HorizontalSpeed == 0f)
        {
            return false;
        }

        var obstacleTop = FindBlockingObstacleTop(level, team, X + horizontalDirection, Y);
        if (!obstacleTop.HasValue)
        {
            return false;
        }

        var bottom = Y + (Height / 2f);
        var stepDelta = bottom - obstacleTop.Value;
        if (stepDelta < 0f || stepDelta > StepUpHeight)
        {
            return false;
        }

        var targetY = Y - stepDelta;
        if (!CanOccupy(level, team, X, targetY))
        {
            return false;
        }

        Y = targetY;
        return true;
    }

    private float? FindBlockingObstacleTop(SimpleLevel level, PlayerTeam team, float x, float y)
    {
        var left = x - (Width / 2f);
        var right = x + (Width / 2f);
        var top = y - (Height / 2f);
        var bottom = y + (Height / 2f);
        float? obstacleTop = null;

        foreach (var solid in level.Solids)
        {
            if (left < solid.Right && right > solid.Left && top < solid.Bottom && bottom > solid.Top)
            {
                obstacleTop = obstacleTop.HasValue ? MathF.Min(obstacleTop.Value, solid.Top) : solid.Top;
            }
        }

        foreach (var wall in level.GetRoomObjects(RoomObjectType.PlayerWall))
        {
            if (left < wall.Right && right > wall.Left && top < wall.Bottom && bottom > wall.Top)
            {
                obstacleTop = obstacleTop.HasValue ? MathF.Min(obstacleTop.Value, wall.Top) : wall.Top;
            }
        }

        return obstacleTop;
    }

    private bool TryJump()
    {
        var jumpSpeed = JumpSpeed * GetJumpScale();
        if (jumpSpeed <= 0f)
        {
            return false;
        }

        if (IsGrounded)
        {
            VerticalSpeed = -jumpSpeed;
            IsGrounded = false;
            return true;
        }

        if (RemainingAirJumps <= 0)
        {
            return false;
        }

        VerticalSpeed = -jumpSpeed;
        RemainingAirJumps -= 1;
        MovementState = LegacyMovementState.None;
        return true;
    }
}

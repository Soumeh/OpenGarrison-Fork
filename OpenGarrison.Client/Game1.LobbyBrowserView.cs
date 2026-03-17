#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void OpenLobbyBrowser()
    {
        _lobbyBrowserOpen = true;
        _manualConnectOpen = false;
        _optionsMenuOpen = false;
        _creditsOpen = false;
        _editingPlayerName = false;
        _editingConnectHost = false;
        _editingConnectPort = false;
        _lobbyBrowserSelectedIndex = -1;
        _lobbyBrowserHoverIndex = -1;
        RefreshLobbyBrowser();
    }

    private void CloseLobbyBrowser(bool clearStatus)
    {
        _lobbyBrowserOpen = false;
        _lobbyBrowserHoverIndex = -1;
        CloseLobbyBrowserLobbyClient();
        if (clearStatus)
        {
            _menuStatusMessage = string.Empty;
        }
    }

    private void RefreshLobbyBrowser()
    {
        EnsureLobbyBrowserClient();
        _lobbyBrowserEntries.Clear();
        StartLobbyBrowserLobbyRequest();

        foreach (var target in BuildLobbyBrowserTargets())
        {
            AddLobbyBrowserEntry(target.DisplayName, target.Host, target.Port, isPrivate: false, isLobbyEntry: false);
        }

        _lobbyBrowserSelectedIndex = _lobbyBrowserEntries.Count > 0 ? 0 : -1;
        _menuStatusMessage = _lobbyBrowserEntries.Count > 0
            ? "Refreshing server list..."
            : _lobbyBrowserLobbyClient is not null
                ? "Contacting lobby server..."
                : "No browser targets yet. Use Join (manual) once to seed one.";
    }

    private void UpdateLobbyBrowserState(KeyboardState keyboard, MouseState mouse)
    {
        UpdateLobbyBrowserResponses();

        if (keyboard.IsKeyDown(Keys.Escape) && !_previousKeyboard.IsKeyDown(Keys.Escape))
        {
            CloseLobbyBrowser(clearStatus: false);
            return;
        }

        var rowBounds = GetLobbyBrowserRowBounds();
        _lobbyBrowserHoverIndex = -1;
        for (var index = 0; index < rowBounds.Length; index += 1)
        {
            if (index >= _lobbyBrowserEntries.Count)
            {
                break;
            }

            if (rowBounds[index].Contains(mouse.Position))
            {
                _lobbyBrowserHoverIndex = index;
                break;
            }
        }

        if (keyboard.IsKeyDown(Keys.Enter) && !_previousKeyboard.IsKeyDown(Keys.Enter))
        {
            JoinSelectedLobbyEntry();
        }

        var clickPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed;
        if (!clickPressed)
        {
            return;
        }

        if (_lobbyBrowserHoverIndex >= 0)
        {
            _lobbyBrowserSelectedIndex = _lobbyBrowserHoverIndex;
            return;
        }

        var refreshBounds = new Rectangle(390, 574, 160, 42);
        var joinBounds = new Rectangle(570, 574, 160, 42);
        var manualBounds = new Rectangle(750, 574, 160, 42);
        var backBounds = new Rectangle(930, 574, 160, 42);
        var point = mouse.Position;
        if (refreshBounds.Contains(point))
        {
            RefreshLobbyBrowser();
        }
        else if (joinBounds.Contains(point))
        {
            JoinSelectedLobbyEntry();
        }
        else if (manualBounds.Contains(point))
        {
            CloseLobbyBrowser(clearStatus: false);
            _manualConnectOpen = true;
            _editingConnectHost = true;
            _editingConnectPort = false;
            _menuStatusMessage = string.Empty;
        }
        else if (backBounds.Contains(point))
        {
            CloseLobbyBrowser(clearStatus: false);
        }
    }

    private void DrawLobbyBrowserMenu()
    {
        var viewportWidth = _graphics.PreferredBackBufferWidth;
        var viewportHeight = _graphics.PreferredBackBufferHeight;
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * 0.78f);

        var panel = new Rectangle(160, 110, 960, 530);
        _spriteBatch.Draw(_pixel, panel, new Color(34, 35, 39, 235));
        _spriteBatch.Draw(_pixel, new Rectangle(panel.X, panel.Y, panel.Width, 3), new Color(210, 210, 210));
        _spriteBatch.Draw(_pixel, new Rectangle(panel.X, panel.Bottom - 3, panel.Width, 3), new Color(76, 76, 76));

        DrawBitmapFontText("Join (browser)", new Vector2(panel.X + 28f, panel.Y + 24f), Color.White, 1f);
        DrawBitmapFontText("Known servers with live status", new Vector2(panel.X + 28f, panel.Y + 56f), new Color(210, 210, 210), 1f);

        var headerY = panel.Y + 104f;
        DrawBitmapFontText("NAME", new Vector2(panel.X + 24f, headerY), Color.White, 1f);
        DrawBitmapFontText("ADDRESS", new Vector2(panel.X + 250f, headerY), Color.White, 1f);
        DrawBitmapFontText("PLAYERS", new Vector2(panel.X + 470f, headerY), Color.White, 1f);
        DrawBitmapFontText("MAP", new Vector2(panel.X + 610f, headerY), Color.White, 1f);
        DrawBitmapFontText("MODE", new Vector2(panel.X + 760f, headerY), Color.White, 1f);
        DrawBitmapFontText("PING", new Vector2(panel.X + 860f, headerY), Color.White, 1f);

        _spriteBatch.Draw(_pixel, new Rectangle(panel.X + 20, panel.Y + 132, panel.Width - 40, 2), new Color(120, 120, 120));
        var rows = GetLobbyBrowserRowBounds();
        for (var index = 0; index < rows.Length && index < _lobbyBrowserEntries.Count; index += 1)
        {
            var entry = _lobbyBrowserEntries[index];
            var bounds = rows[index];
            var highlighted = index == _lobbyBrowserSelectedIndex;
            var hovered = index == _lobbyBrowserHoverIndex;
            var background = highlighted
                ? new Color(110, 53, 53)
                : hovered
                    ? new Color(64, 66, 72)
                    : new Color(44, 46, 52);
            _spriteBatch.Draw(_pixel, bounds, background);

            var statusColor = entry.HasResponse
                ? Color.White
                : entry.HasTimedOut
                    ? new Color(220, 160, 120)
                    : new Color(190, 190, 140);
            var playerText = entry.HasResponse
                ? $"{entry.PlayerCount}/{entry.MaxPlayerCount} (+{entry.SpectatorCount})"
                : entry.StatusText;
            DrawBitmapFontText(entry.DisplayName, new Vector2(bounds.X + 12f, bounds.Y + 10f), Color.White, 1f);
            DrawBitmapFontText(entry.AddressLabel, new Vector2(bounds.X + 238f, bounds.Y + 10f), new Color(210, 210, 210), 1f);
            DrawBitmapFontText(playerText, new Vector2(bounds.X + 458f, bounds.Y + 10f), statusColor, 1f);
            DrawBitmapFontText(entry.LevelName, new Vector2(bounds.X + 598f, bounds.Y + 10f), statusColor, 1f);
            DrawBitmapFontText(entry.ModeLabel, new Vector2(bounds.X + 748f, bounds.Y + 10f), statusColor, 1f);
            DrawBitmapFontText(entry.PingLabel, new Vector2(bounds.X + 848f, bounds.Y + 10f), statusColor, 1f);
        }

        DrawMenuButton(new Rectangle(panel.X + 230, panel.Bottom - 66, 160, 42), "Refresh", false);
        DrawMenuButton(new Rectangle(panel.X + 410, panel.Bottom - 66, 160, 42), "Join", CanJoinSelectedLobbyEntry());
        DrawMenuButton(new Rectangle(panel.X + 590, panel.Bottom - 66, 160, 42), "Manual", false);
        DrawMenuButton(new Rectangle(panel.X + 770, panel.Bottom - 66, 160, 42), "Back", false);

        if (!string.IsNullOrWhiteSpace(_menuStatusMessage))
        {
            DrawBitmapFontText(_menuStatusMessage, new Vector2(panel.X + 28f, panel.Bottom - 104f), new Color(230, 220, 180), 1f);
        }
    }

    private static Rectangle[] GetLobbyBrowserRowBounds()
    {
        var rows = new Rectangle[8];
        for (var index = 0; index < rows.Length; index += 1)
        {
            rows[index] = new Rectangle(180, 250 + (index * 34), 920, 28);
        }

        return rows;
    }

    private void JoinSelectedLobbyEntry()
    {
        if (!CanJoinSelectedLobbyEntry())
        {
            _menuStatusMessage = "Select an online server first.";
            return;
        }

        var entry = _lobbyBrowserEntries[_lobbyBrowserSelectedIndex];
        TryConnectToServer(entry.Host, entry.Port, addConsoleFeedback: false);
    }

    private bool CanJoinSelectedLobbyEntry()
    {
        return _lobbyBrowserSelectedIndex >= 0
            && _lobbyBrowserSelectedIndex < _lobbyBrowserEntries.Count
            && _lobbyBrowserEntries[_lobbyBrowserSelectedIndex].HasResponse;
    }

    private IEnumerable<LobbyBrowserTarget> BuildLobbyBrowserTargets()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var target in new[]
                 {
                     new LobbyBrowserTarget("Localhost", "127.0.0.1", 8190),
                     new LobbyBrowserTarget("Manual target", _connectHostBuffer.Trim(), TryParseBrowserPort(_connectPortBuffer)),
                     new LobbyBrowserTarget("Recent", _recentConnectHost ?? string.Empty, _recentConnectPort),
                 })
        {
            if (string.IsNullOrWhiteSpace(target.Host) || target.Port <= 0)
            {
                continue;
            }

            var key = $"{target.Host}:{target.Port}";
            if (seen.Add(key))
            {
                yield return target;
            }
        }
    }

    private static int TryParseBrowserPort(string text)
    {
        return int.TryParse(text.Trim(), out var port) && port is > 0 and <= 65535 ? port : 0;
    }
}

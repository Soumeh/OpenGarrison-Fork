#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void OpenHostSetupMenu()
    {
        _hostSetupOpen = true;
        _hostSetupHoverIndex = -1;
        _hostSetupTab = HostSetupTab.Settings;
        _hostSetupEditField = HostSetupEditField.ServerName;
        _menuStatusMessage = string.Empty;
        _manualConnectOpen = false;
        CloseLobbyBrowser(clearStatus: false);
        _optionsMenuOpen = false;
        _creditsOpen = false;
        _editingPlayerName = false;

        if (string.IsNullOrWhiteSpace(_hostServerNameBuffer))
        {
            _hostServerNameBuffer = "My Server";
        }

        if (string.IsNullOrWhiteSpace(_hostPortBuffer))
        {
            _hostPortBuffer = "8190";
        }

        if (string.IsNullOrWhiteSpace(_hostSlotsBuffer))
        {
            _hostSlotsBuffer = "10";
        }

        if (string.IsNullOrWhiteSpace(_hostTimeLimitBuffer))
        {
            _hostTimeLimitBuffer = "15";
        }

        if (string.IsNullOrWhiteSpace(_hostCapLimitBuffer))
        {
            _hostCapLimitBuffer = "5";
        }

        if (string.IsNullOrWhiteSpace(_hostRespawnSecondsBuffer))
        {
            _hostRespawnSecondsBuffer = "5";
        }

        _hostMapEntries = BuildHostSetupMapEntries();
        if (_hostMapEntries.Count == 0)
        {
            _hostMapIndex = 0;
            return;
        }

        var configuredStartMapName = _clientSettings.HostDefaults.GetFirstIncludedMapLevelName();
        if (!SelectHostMapEntry(configuredStartMapName))
        {
            _hostMapIndex = FindDefaultHostMapIndex();
        }
    }

    private void UpdateHostSetupMenu(MouseState mouse)
    {
        GetHostSetupLayout(
            out var panel,
            out var listBounds,
            out var toggleBounds,
            out var moveUpBounds,
            out var moveDownBounds,
            out var serverNameBounds,
            out var portBounds,
            out var slotsBounds,
            out var passwordBounds,
            out var rotationFileBounds,
            out var timeLimitBounds,
            out var capLimitBounds,
            out var respawnBounds,
            out var lobbyBounds,
            out var autoBalanceBounds,
            out var hostBounds,
            out var backBounds);
        var clickPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed;

        if (IsServerLauncherMode)
        {
            GetServerLauncherTabBounds(panel, out var settingsTabBounds, out var consoleTabBounds);
            if (clickPressed)
            {
                if (settingsTabBounds.Contains(mouse.Position))
                {
                    _hostSetupTab = HostSetupTab.Settings;
                    _hostSetupEditField = HostSetupEditField.ServerName;
                    return;
                }

                if (consoleTabBounds.Contains(mouse.Position))
                {
                    _hostSetupTab = HostSetupTab.ServerConsole;
                    _hostSetupEditField = HostSetupEditField.ServerConsoleCommand;
                    return;
                }
            }
        }

        if (IsServerLauncherMode && _hostSetupTab == HostSetupTab.ServerConsole)
        {
            GetHostedServerConsoleLayout(
                panel,
                out _,
                out _,
                out var commandBounds,
                out var sendBounds,
                out var clearBounds,
                out var statusCommandBounds,
                out var playersCommandBounds,
                out var rotationCommandBounds,
                out var helpCommandBounds,
                out hostBounds,
                out backBounds);
            var terminalBounds = GetHostSetupTerminalButtonBounds(panel);

            if (!clickPressed)
            {
                return;
            }

            if (commandBounds.Contains(mouse.Position))
            {
                _hostSetupEditField = HostSetupEditField.ServerConsoleCommand;
                return;
            }

            if (sendBounds.Contains(mouse.Position))
            {
                ExecuteHostedServerCommandFromUi(_hostedServerCommandInput);
                return;
            }

            if (clearBounds.Contains(mouse.Position))
            {
                ClearHostedServerConsoleView();
                _menuStatusMessage = "Console view cleared.";
                return;
            }

            if (statusCommandBounds.Contains(mouse.Position))
            {
                ExecuteHostedServerCommandFromUi("status");
                return;
            }

            if (playersCommandBounds.Contains(mouse.Position))
            {
                ExecuteHostedServerCommandFromUi("players");
                return;
            }

            if (rotationCommandBounds.Contains(mouse.Position))
            {
                ExecuteHostedServerCommandFromUi("rotation");
                return;
            }

            if (helpCommandBounds.Contains(mouse.Position))
            {
                ExecuteHostedServerCommandFromUi("help");
                return;
            }

            if (!IsHostedServerRunning && hostBounds.Contains(mouse.Position))
            {
                TryHostFromSetup();
            }
            else if (!IsHostedServerRunning && terminalBounds.Contains(mouse.Position))
            {
                TryHostFromSetup(runInTerminal: true);
            }
            else if (backBounds.Contains(mouse.Position))
            {
                if (!TryHandleServerLauncherBackAction())
                {
                    _hostSetupOpen = false;
                    _hostSetupEditField = HostSetupEditField.None;
                }
            }

            return;
        }

        const int listHeaderHeight = 20;
        const int rowHeight = 28;
        var launchTerminalBounds = GetHostSetupTerminalButtonBounds(panel);

        _hostSetupHoverIndex = -1;
        var listRowsBounds = new Rectangle(listBounds.X, listBounds.Y + listHeaderHeight, listBounds.Width, listBounds.Height - listHeaderHeight);
        if (listRowsBounds.Contains(mouse.Position))
        {
            var row = (mouse.Y - listRowsBounds.Y) / rowHeight;
            if (row >= 0 && row < _hostMapEntries.Count)
            {
                _hostSetupHoverIndex = row;
            }
        }

        if (!clickPressed)
        {
            return;
        }

        if (serverNameBounds.Contains(mouse.Position))
        {
            _hostSetupEditField = HostSetupEditField.ServerName;
            return;
        }

        if (portBounds.Contains(mouse.Position))
        {
            _hostSetupEditField = HostSetupEditField.Port;
            return;
        }

        if (slotsBounds.Contains(mouse.Position))
        {
            _hostSetupEditField = HostSetupEditField.Slots;
            return;
        }

        if (passwordBounds.Contains(mouse.Position))
        {
            _hostSetupEditField = HostSetupEditField.Password;
            return;
        }

        if (rotationFileBounds.Contains(mouse.Position))
        {
            _hostSetupEditField = HostSetupEditField.MapRotationFile;
            return;
        }

        if (timeLimitBounds.Contains(mouse.Position))
        {
            _hostSetupEditField = HostSetupEditField.TimeLimit;
            return;
        }

        if (capLimitBounds.Contains(mouse.Position))
        {
            _hostSetupEditField = HostSetupEditField.CapLimit;
            return;
        }

        if (respawnBounds.Contains(mouse.Position))
        {
            _hostSetupEditField = HostSetupEditField.RespawnSeconds;
            return;
        }

        if (_hostSetupHoverIndex >= 0 && listRowsBounds.Contains(mouse.Position))
        {
            _hostMapIndex = _hostSetupHoverIndex;
            _hostSetupEditField = HostSetupEditField.None;
            return;
        }

        if (toggleBounds.Contains(mouse.Position))
        {
            ToggleSelectedHostMap();
            return;
        }

        if (moveUpBounds.Contains(mouse.Position))
        {
            MoveSelectedHostMap(-1);
            return;
        }

        if (moveDownBounds.Contains(mouse.Position))
        {
            MoveSelectedHostMap(1);
            return;
        }

        if (lobbyBounds.Contains(mouse.Position))
        {
            _hostLobbyAnnounceEnabled = !_hostLobbyAnnounceEnabled;
            return;
        }

        if (autoBalanceBounds.Contains(mouse.Position))
        {
            _hostAutoBalanceEnabled = !_hostAutoBalanceEnabled;
            return;
        }

        if (!IsHostedServerRunning && hostBounds.Contains(mouse.Position))
        {
            TryHostFromSetup();
        }
        else if (IsServerLauncherMode && !IsHostedServerRunning && launchTerminalBounds.Contains(mouse.Position))
        {
            TryHostFromSetup(runInTerminal: true);
        }
        else if (backBounds.Contains(mouse.Position))
        {
            if (!TryHandleServerLauncherBackAction())
            {
                _hostSetupOpen = false;
                _hostSetupEditField = HostSetupEditField.None;
            }
        }
    }

    private void DrawHostSetupMenu()
    {
        var viewportWidth = _graphics.PreferredBackBufferWidth;
        var viewportHeight = _graphics.PreferredBackBufferHeight;
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * 0.78f);

        GetHostSetupLayout(
            out var panel,
            out var listBounds,
            out var toggleBounds,
            out var moveUpBounds,
            out var moveDownBounds,
            out var serverNameBounds,
            out var portBounds,
            out var slotsBounds,
            out var passwordBounds,
            out var rotationFileBounds,
            out var timeLimitBounds,
            out var capLimitBounds,
            out var respawnBounds,
            out var lobbyBounds,
            out var autoBalanceBounds,
            out var hostBounds,
            out var backBounds);
        _spriteBatch.Draw(_pixel, panel, new Color(34, 35, 39, 235));
        _spriteBatch.Draw(_pixel, new Rectangle(panel.X, panel.Y, panel.Width, 3), new Color(210, 210, 210));
        _spriteBatch.Draw(_pixel, new Rectangle(panel.X, panel.Bottom - 3, panel.Width, 3), new Color(76, 76, 76));

        DrawBitmapFontText(GetHostSetupTitle(), new Vector2(panel.X + 28f, panel.Y + 22f), Color.White, 1f);
        DrawBitmapFontText(GetHostSetupSubtitle(), new Vector2(panel.X + 28f, panel.Y + 50f), new Color(200, 200, 200), 0.9f);

        if (IsServerLauncherMode)
        {
            GetServerLauncherTabBounds(panel, out var settingsTabBounds, out var consoleTabBounds);
            DrawMenuButton(settingsTabBounds, "Settings", _hostSetupTab == HostSetupTab.Settings);
            DrawMenuButton(consoleTabBounds, "Server Console", _hostSetupTab == HostSetupTab.ServerConsole);
        }

        if (IsServerLauncherMode && _hostSetupTab == HostSetupTab.ServerConsole)
        {
            DrawHostedServerConsoleTab(panel);
            return;
        }

        DrawBitmapFontText("Stock Map Rotation", new Vector2(listBounds.X, listBounds.Y - 24f), Color.White, 0.95f);
        DrawBitmapFontText("ORDER", new Vector2(listBounds.X + 10f, listBounds.Y - 2f), new Color(210, 210, 210), 0.8f);
        DrawBitmapFontText("MAP", new Vector2(listBounds.X + 78f, listBounds.Y - 2f), new Color(210, 210, 210), 0.8f);
        DrawBitmapFontText("MODE", new Vector2(listBounds.Right - 112f, listBounds.Y - 2f), new Color(210, 210, 210), 0.8f);
        DrawBitmapFontText("ON", new Vector2(listBounds.Right - 48f, listBounds.Y - 2f), new Color(210, 210, 210), 0.8f);

        const int listHeaderHeight = 20;
        const int rowHeight = 28;
        for (var index = 0; index < _hostMapEntries.Count; index += 1)
        {
            var entry = _hostMapEntries[index];
            var rowBounds = new Rectangle(listBounds.X - 6, listBounds.Y + listHeaderHeight + (index * rowHeight), listBounds.Width + 12, rowHeight - 2);
            if (index == _hostMapIndex)
            {
                _spriteBatch.Draw(_pixel, rowBounds, new Color(90, 64, 64));
            }
            else if (index == _hostSetupHoverIndex)
            {
                _spriteBatch.Draw(_pixel, rowBounds, new Color(60, 60, 70));
            }
            else
            {
                _spriteBatch.Draw(_pixel, rowBounds, new Color(44, 46, 52, 170));
            }

            var modeLabel = entry.Mode switch
            {
                GameModeKind.Arena => "Arena",
                GameModeKind.ControlPoint => "CP",
                GameModeKind.Generator => "Gen",
                _ => "CTF",
            };
            var orderLabel = entry.Order > 0 ? $"#{entry.Order}" : "--";
            var enabledLabel = entry.Order > 0 ? "ON" : "OFF";
            var enabledColor = entry.Order > 0 ? new Color(178, 228, 155) : new Color(140, 140, 140);

            DrawBitmapFontText(orderLabel, new Vector2(listBounds.X + 10f, rowBounds.Y + 6f), Color.White, 0.9f);
            DrawBitmapFontText(entry.DisplayName, new Vector2(listBounds.X + 78f, rowBounds.Y + 6f), Color.White, 0.9f);
            DrawBitmapFontText(modeLabel, new Vector2(listBounds.Right - 112f, rowBounds.Y + 6f), new Color(210, 210, 210), 0.9f);
            DrawBitmapFontText(enabledLabel, new Vector2(listBounds.Right - 50f, rowBounds.Y + 6f), enabledColor, 0.9f);
        }

        var selectedMap = GetSelectedHostMapEntry();
        var selectedIncluded = selectedMap is not null && selectedMap.Order > 0;
        DrawMenuButton(toggleBounds, selectedIncluded ? "Exclude" : "Include", selectedIncluded);
        DrawMenuButton(moveUpBounds, "Move Up", false);
        DrawMenuButton(moveDownBounds, "Move Down", false);

        var stockRotationLabel = GetHostStockRotationSummary();
        DrawBitmapFontText(stockRotationLabel, new Vector2(listBounds.X, toggleBounds.Bottom + 18f), new Color(220, 220, 220), 0.85f);
        if (!string.IsNullOrWhiteSpace(_hostMapRotationFileBuffer))
        {
            DrawBitmapFontText("Custom rotation file overrides the stock list below.", new Vector2(listBounds.X, toggleBounds.Bottom + 40f), new Color(226, 204, 164), 0.82f);
        }

        DrawBitmapFontText("Server Name", new Vector2(serverNameBounds.X, serverNameBounds.Y - 18f), new Color(210, 210, 210), 0.9f);
        DrawMenuInputBox(serverNameBounds, _hostServerNameBuffer, _hostSetupEditField == HostSetupEditField.ServerName);

        DrawBitmapFontText("Port", new Vector2(portBounds.X, portBounds.Y - 18f), new Color(210, 210, 210), 0.9f);
        DrawMenuInputBox(portBounds, _hostPortBuffer, _hostSetupEditField == HostSetupEditField.Port);

        DrawBitmapFontText("Slots", new Vector2(slotsBounds.X, slotsBounds.Y - 18f), new Color(210, 210, 210), 0.9f);
        DrawMenuInputBox(slotsBounds, _hostSlotsBuffer, _hostSetupEditField == HostSetupEditField.Slots);

        DrawBitmapFontText("Password", new Vector2(passwordBounds.X, passwordBounds.Y - 18f), new Color(210, 210, 210), 0.9f);
        var maskedPassword = string.IsNullOrEmpty(_hostPasswordBuffer) ? string.Empty : new string('*', _hostPasswordBuffer.Length);
        DrawMenuInputBox(passwordBounds, maskedPassword, _hostSetupEditField == HostSetupEditField.Password);

        DrawBitmapFontText("Custom Rotation File", new Vector2(rotationFileBounds.X, rotationFileBounds.Y - 18f), new Color(210, 210, 210), 0.9f);
        DrawMenuInputBox(rotationFileBounds, _hostMapRotationFileBuffer, _hostSetupEditField == HostSetupEditField.MapRotationFile);

        DrawBitmapFontText("Time Limit (mins)", new Vector2(timeLimitBounds.X, timeLimitBounds.Y - 18f), new Color(210, 210, 210), 0.9f);
        DrawMenuInputBox(timeLimitBounds, _hostTimeLimitBuffer, _hostSetupEditField == HostSetupEditField.TimeLimit);

        DrawBitmapFontText("Cap Limit", new Vector2(capLimitBounds.X, capLimitBounds.Y - 18f), new Color(210, 210, 210), 0.9f);
        DrawMenuInputBox(capLimitBounds, _hostCapLimitBuffer, _hostSetupEditField == HostSetupEditField.CapLimit);

        DrawBitmapFontText("Respawn Time (secs)", new Vector2(respawnBounds.X, respawnBounds.Y - 18f), new Color(210, 210, 210), 0.9f);
        DrawMenuInputBox(respawnBounds, _hostRespawnSecondsBuffer, _hostSetupEditField == HostSetupEditField.RespawnSeconds);

        DrawMenuButton(lobbyBounds, _hostLobbyAnnounceEnabled ? "Lobby Announce: On" : "Lobby Announce: Off", _hostLobbyAnnounceEnabled);
        DrawMenuButton(autoBalanceBounds, _hostAutoBalanceEnabled ? "Auto-balance: On" : "Auto-balance: Off", _hostAutoBalanceEnabled);

        DrawMenuButton(hostBounds, GetHostSetupPrimaryButtonLabel(), false);
        DrawMenuButton(backBounds, GetHostSetupSecondaryButtonLabel(), IsServerLauncherMode && IsHostedServerRunning);
        if (IsServerLauncherMode && !IsHostedServerRunning)
        {
            DrawMenuButton(GetHostSetupTerminalButtonBounds(panel), "Run In Terminal", false);
        }

        if (IsServerLauncherMode && IsHostedServerRunning)
        {
            DrawBitmapFontText(
                "Use Stop Server to end the active dedicated server session.",
                new Vector2(panel.X + 28f, panel.Bottom - 62f),
                new Color(210, 210, 210),
                0.85f);
        }

        if (!string.IsNullOrWhiteSpace(_menuStatusMessage))
        {
            DrawBitmapFontText(_menuStatusMessage, new Vector2(panel.X + 28f, panel.Bottom - 38f), new Color(230, 220, 180), 1f);
        }
    }

    private void DrawHostedServerConsoleTab(Rectangle panel)
    {
        GetHostedServerConsoleLayout(
            panel,
            out var logBounds,
            out var summaryBounds,
            out var commandBounds,
            out var sendBounds,
            out var clearBounds,
            out var statusCommandBounds,
            out var playersCommandBounds,
            out var rotationCommandBounds,
            out var helpCommandBounds,
            out var hostBounds,
            out var backBounds);

        _spriteBatch.Draw(_pixel, logBounds, new Color(24, 25, 30, 230));
        _spriteBatch.Draw(_pixel, summaryBounds, new Color(28, 30, 34, 230));
        DrawBitmapFontText("Recent Output", new Vector2(logBounds.X + 10f, logBounds.Y + 8f), Color.White, 0.95f);
        DrawBitmapFontText("Live Status", new Vector2(summaryBounds.X + 10f, summaryBounds.Y + 8f), Color.White, 0.95f);

        var consoleLines = GetHostedServerConsoleLinesSnapshot();
        var availableLineCount = Math.Max(1, (logBounds.Height - 38) / 18);
        var firstLineIndex = Math.Max(0, consoleLines.Count - availableLineCount);
        var drawY = logBounds.Y + 30f;
        if (consoleLines.Count == 0)
        {
            _spriteBatch.DrawString(_consoleFont, "No server output yet.", new Vector2(logBounds.X + 12f, drawY), new Color(200, 200, 200));
        }
        else
        {
            for (var index = firstLineIndex; index < consoleLines.Count; index += 1)
            {
                var line = TrimConsoleText(consoleLines[index], logBounds.Width - 24f);
                _spriteBatch.DrawString(_consoleFont, line, new Vector2(logBounds.X + 12f, drawY), new Color(230, 232, 235));
                drawY += 18f;
            }
        }

        var summaryRows = new (string Label, string Value)[]
        {
            ("Server", _hostedServerStatusName),
            ("Port", _hostedServerStatusPort),
            ("Players", _hostedServerStatusPlayers),
            ("Lobby", _hostedServerStatusLobby),
            ("Map", _hostedServerStatusMap),
            ("Rules", _hostedServerStatusRules),
            ("Runtime", _hostedServerStatusRuntime),
            ("World", _hostedServerStatusWorld),
        };

        for (var index = 0; index < summaryRows.Length; index += 1)
        {
            var rowBounds = new Rectangle(summaryBounds.X + 10, summaryBounds.Y + 32 + (index * 45), summaryBounds.Width - 20, 40);
            DrawHostedServerSummaryRow(rowBounds, summaryRows[index].Label, summaryRows[index].Value);
        }

        DrawBitmapFontText("Console Command", new Vector2(commandBounds.X, commandBounds.Y - 20f), new Color(210, 210, 210), 0.9f);
        DrawMenuInputBox(commandBounds, _hostedServerCommandInput, _hostSetupEditField == HostSetupEditField.ServerConsoleCommand);
        DrawMenuButton(sendBounds, "Send", false);
        DrawMenuButton(clearBounds, "Clear", false);
        DrawMenuButton(statusCommandBounds, "Status", false);
        DrawMenuButton(playersCommandBounds, "Players", false);
        DrawMenuButton(rotationCommandBounds, "Rotation", false);
        DrawMenuButton(helpCommandBounds, "Help", false);
        DrawMenuButton(hostBounds, GetHostSetupPrimaryButtonLabel(), false);
        DrawMenuButton(backBounds, GetHostSetupSecondaryButtonLabel(), IsServerLauncherMode && IsHostedServerRunning);
        if (!IsHostedServerRunning)
        {
            DrawMenuButton(GetHostSetupTerminalButtonBounds(panel), "Run In Terminal", false);
        }

        DrawBitmapFontText(
            "Use Enter or Send to dispatch a server command to the dedicated process.",
            new Vector2(panel.X + 28f, panel.Bottom - 90f),
            new Color(210, 210, 210),
            0.82f);

        if (!string.IsNullOrWhiteSpace(_menuStatusMessage))
        {
            DrawBitmapFontText(_menuStatusMessage, new Vector2(panel.X + 28f, panel.Bottom - 38f), new Color(230, 220, 180), 1f);
        }
    }

    private void DrawHostedServerSummaryRow(Rectangle bounds, string label, string value)
    {
        _spriteBatch.Draw(_pixel, bounds, new Color(44, 46, 52, 180));
        DrawBitmapFontText(label.ToUpperInvariant(), new Vector2(bounds.X + 8f, bounds.Y + 6f), new Color(210, 210, 210), 0.82f);
        _spriteBatch.DrawString(_consoleFont, TrimConsoleText(value, bounds.Width - 16f), new Vector2(bounds.X + 10f, bounds.Y + 20f), Color.White);
    }

    private void ExecuteHostedServerCommandFromUi(string command)
    {
        if (TrySendHostedServerCommand(command, out var error))
        {
            _menuStatusMessage = "Command sent.";
        }
        else
        {
            _menuStatusMessage = error;
        }
    }

    private string TrimConsoleText(string text, float maxWidth)
    {
        if (string.IsNullOrEmpty(text) || _consoleFont.MeasureString(text).X <= maxWidth)
        {
            return text;
        }

        const string ellipsis = "...";
        var trimmed = text;
        while (trimmed.Length > 0 && _consoleFont.MeasureString(trimmed + ellipsis).X > maxWidth)
        {
            trimmed = trimmed[..^1];
        }

        return trimmed.Length == 0 ? ellipsis : trimmed + ellipsis;
    }

    private static void GetServerLauncherTabBounds(Rectangle panel, out Rectangle settingsTabBounds, out Rectangle consoleTabBounds)
    {
        settingsTabBounds = new Rectangle(panel.Right - 332, panel.Y + 18, 146, 32);
        consoleTabBounds = new Rectangle(panel.Right - 176, panel.Y + 18, 146, 32);
    }

    private static void GetHostedServerConsoleLayout(
        Rectangle panel,
        out Rectangle logBounds,
        out Rectangle summaryBounds,
        out Rectangle commandBounds,
        out Rectangle sendBounds,
        out Rectangle clearBounds,
        out Rectangle statusCommandBounds,
        out Rectangle playersCommandBounds,
        out Rectangle rotationCommandBounds,
        out Rectangle helpCommandBounds,
        out Rectangle hostBounds,
        out Rectangle backBounds)
    {
        logBounds = new Rectangle(panel.X + 28, panel.Y + 96, 574, 410);
        summaryBounds = new Rectangle(logBounds.Right + 18, logBounds.Y, panel.Right - logBounds.Right - 46, 410);
        commandBounds = new Rectangle(logBounds.X, logBounds.Bottom + 18, 390, 34);
        sendBounds = new Rectangle(commandBounds.Right + 12, commandBounds.Y, 78, 34);
        clearBounds = new Rectangle(sendBounds.Right + 10, commandBounds.Y, 78, 34);
        statusCommandBounds = new Rectangle(summaryBounds.X, summaryBounds.Bottom + 18, 64, 34);
        playersCommandBounds = new Rectangle(statusCommandBounds.Right + 8, statusCommandBounds.Y, 72, 34);
        rotationCommandBounds = new Rectangle(playersCommandBounds.Right + 8, statusCommandBounds.Y, 78, 34);
        helpCommandBounds = new Rectangle(rotationCommandBounds.Right + 8, statusCommandBounds.Y, 60, 34);
        hostBounds = new Rectangle(panel.Right - 330, panel.Bottom - 62, 140, 42);
        backBounds = new Rectangle(panel.Right - 170, panel.Bottom - 62, 140, 42);
    }

    private static Rectangle GetHostSetupTerminalButtonBounds(Rectangle panel)
    {
        return new Rectangle(panel.Right - 500, panel.Bottom - 62, 150, 42);
    }

    private static void GetHostSetupLayout(
        out Rectangle panel,
        out Rectangle listBounds,
        out Rectangle toggleBounds,
        out Rectangle moveUpBounds,
        out Rectangle moveDownBounds,
        out Rectangle serverNameBounds,
        out Rectangle portBounds,
        out Rectangle slotsBounds,
        out Rectangle passwordBounds,
        out Rectangle rotationFileBounds,
        out Rectangle timeLimitBounds,
        out Rectangle capLimitBounds,
        out Rectangle respawnBounds,
        out Rectangle lobbyBounds,
        out Rectangle autoBalanceBounds,
        out Rectangle hostBounds,
        out Rectangle backBounds)
    {
        panel = new Rectangle(160, 50, 960, 620);
        listBounds = new Rectangle(panel.X + 36, panel.Y + 96, 392, 328);

        toggleBounds = new Rectangle(listBounds.X, listBounds.Bottom + 14, 116, 34);
        moveUpBounds = new Rectangle(toggleBounds.Right + 12, listBounds.Bottom + 14, 116, 34);
        moveDownBounds = new Rectangle(moveUpBounds.Right + 12, listBounds.Bottom + 14, 116, 34);

        var fieldX = panel.X + 474;
        var fieldWidth = panel.Right - fieldX - 36;
        var fieldBoxY = panel.Y + 100;
        const int fieldHeight = 32;
        const int fieldSpacing = 50;

        serverNameBounds = new Rectangle(fieldX, fieldBoxY, fieldWidth, fieldHeight);
        portBounds = new Rectangle(fieldX, fieldBoxY + fieldSpacing, fieldWidth, fieldHeight);
        slotsBounds = new Rectangle(fieldX, fieldBoxY + (fieldSpacing * 2), fieldWidth, fieldHeight);
        passwordBounds = new Rectangle(fieldX, fieldBoxY + (fieldSpacing * 3), fieldWidth, fieldHeight);
        rotationFileBounds = new Rectangle(fieldX, fieldBoxY + (fieldSpacing * 4), fieldWidth, fieldHeight);
        timeLimitBounds = new Rectangle(fieldX, fieldBoxY + (fieldSpacing * 5), fieldWidth, fieldHeight);
        capLimitBounds = new Rectangle(fieldX, fieldBoxY + (fieldSpacing * 6), fieldWidth, fieldHeight);
        respawnBounds = new Rectangle(fieldX, fieldBoxY + (fieldSpacing * 7), fieldWidth, fieldHeight);

        lobbyBounds = new Rectangle(fieldX, panel.Bottom - 150, fieldWidth, 34);
        autoBalanceBounds = new Rectangle(fieldX, panel.Bottom - 110, fieldWidth, 34);

        hostBounds = new Rectangle(panel.Right - 330, panel.Bottom - 62, 140, 42);
        backBounds = new Rectangle(panel.Right - 170, panel.Bottom - 62, 140, 42);
    }

    private void TryHostFromSetup(bool runInTerminal = false)
    {
        var trimmedRotationFile = _hostMapRotationFileBuffer.Trim();
        if (_hostMapEntries.Count == 0)
        {
            _menuStatusMessage = "No stock maps are available.";
            return;
        }

        if (string.IsNullOrWhiteSpace(trimmedRotationFile)
            && !_hostMapEntries.Any(entry => entry.Order > 0))
        {
            _menuStatusMessage = "Include at least one stock map or set a custom rotation file.";
            return;
        }

        var serverName = _hostServerNameBuffer.Trim();
        if (string.IsNullOrWhiteSpace(serverName))
        {
            _menuStatusMessage = "Server name is required.";
            return;
        }

        if (!int.TryParse(_hostPortBuffer.Trim(), out var port) || port is <= 0 or > 65535)
        {
            _menuStatusMessage = "Port must be 1-65535.";
            return;
        }

        if (!int.TryParse(_hostSlotsBuffer.Trim(), out var maxPlayers)
            || maxPlayers < 1
            || maxPlayers > SimulationWorld.MaxPlayableNetworkPlayers)
        {
            _menuStatusMessage = $"Slots must be 1-{SimulationWorld.MaxPlayableNetworkPlayers}.";
            return;
        }

        if (!int.TryParse(_hostTimeLimitBuffer.Trim(), out var timeLimitMinutes)
            || timeLimitMinutes < 1
            || timeLimitMinutes > 255)
        {
            _menuStatusMessage = "Time limit must be 1-255 minutes.";
            return;
        }

        if (!int.TryParse(_hostCapLimitBuffer.Trim(), out var capLimit)
            || capLimit < 1
            || capLimit > 255)
        {
            _menuStatusMessage = "Cap limit must be 1-255.";
            return;
        }

        if (!int.TryParse(_hostRespawnSecondsBuffer.Trim(), out var respawnSeconds)
            || respawnSeconds < 0
            || respawnSeconds > 255)
        {
            _menuStatusMessage = "Respawn time must be 0-255 seconds.";
            return;
        }

        PersistClientSettings();
        if (IsServerLauncherMode)
        {
            if (runInTerminal)
            {
                BeginDedicatedServerTerminalLaunch(
                    serverName,
                    port,
                    maxPlayers,
                    _hostPasswordBuffer.Trim(),
                    timeLimitMinutes,
                    capLimit,
                    respawnSeconds,
                    _hostLobbyAnnounceEnabled,
                    _hostAutoBalanceEnabled);
            }
            else
            {
                BeginDedicatedServerLaunch(
                    serverName,
                    port,
                    maxPlayers,
                    _hostPasswordBuffer.Trim(),
                    timeLimitMinutes,
                    capLimit,
                    respawnSeconds,
                    _hostLobbyAnnounceEnabled,
                    _hostAutoBalanceEnabled);
            }
            return;
        }

        BeginHostedGame(
            serverName,
            port,
            maxPlayers,
            _hostPasswordBuffer.Trim(),
            timeLimitMinutes,
            capLimit,
            respawnSeconds,
            _hostLobbyAnnounceEnabled,
            _hostAutoBalanceEnabled);
    }

    private List<OpenGarrisonMapRotationEntry> BuildHostSetupMapEntries()
    {
        var configuredEntries = _clientSettings.HostDefaults.StockMapRotation
            .ToDictionary(entry => entry.IniKey, entry => entry, StringComparer.OrdinalIgnoreCase);
        var mergedEntries = new List<OpenGarrisonMapRotationEntry>(OpenGarrisonStockMapCatalog.Definitions.Count);
        foreach (var definition in OpenGarrisonStockMapCatalog.Definitions)
        {
            if (configuredEntries.TryGetValue(definition.IniKey, out var existing))
            {
                mergedEntries.Add(existing.Clone());
            }
            else
            {
                mergedEntries.Add(new OpenGarrisonMapRotationEntry
                {
                    IniKey = definition.IniKey,
                    LevelName = definition.LevelName,
                    DisplayName = definition.DisplayName,
                    Mode = definition.Mode,
                    DefaultOrder = definition.DefaultOrder,
                    Order = definition.DefaultOrder,
                });
            }
        }

        return OpenGarrisonStockMapCatalog.GetOrderedEntries(mergedEntries)
            .Select(entry => entry.Clone())
            .ToList();
    }

    private void ToggleSelectedHostMap()
    {
        var selected = GetSelectedHostMapEntry();
        if (selected is null)
        {
            return;
        }

        if (selected.Order > 0)
        {
            selected.Order = 0;
        }
        else
        {
            selected.Order = _hostMapEntries.Where(entry => entry.Order > 0).Select(entry => entry.Order).DefaultIfEmpty().Max() + 1;
        }

        SortHostMapEntries(selected.LevelName);
        _menuStatusMessage = string.Empty;
    }

    private void MoveSelectedHostMap(int direction)
    {
        var selected = GetSelectedHostMapEntry();
        if (selected is null || selected.Order <= 0)
        {
            return;
        }

        var includedEntries = _hostMapEntries
            .Where(entry => entry.Order > 0)
            .OrderBy(entry => entry.Order)
            .ToList();
        var currentIndex = includedEntries.FindIndex(entry => string.Equals(entry.LevelName, selected.LevelName, StringComparison.OrdinalIgnoreCase));
        var targetIndex = currentIndex + direction;
        if (currentIndex < 0 || targetIndex < 0 || targetIndex >= includedEntries.Count)
        {
            return;
        }

        var swapTarget = includedEntries[targetIndex];
        (selected.Order, swapTarget.Order) = (swapTarget.Order, selected.Order);
        SortHostMapEntries(selected.LevelName);
        _menuStatusMessage = string.Empty;
    }

    private void SortHostMapEntries(string? selectedLevelName = null)
    {
        var desiredSelection = selectedLevelName ?? GetSelectedHostMapEntry()?.LevelName;
        _hostMapEntries = OpenGarrisonStockMapCatalog.GetOrderedEntries(_hostMapEntries)
            .Select(entry => entry.Clone())
            .ToList();
        if (!SelectHostMapEntry(desiredSelection))
        {
            _hostMapIndex = Math.Clamp(_hostMapIndex, 0, Math.Max(0, _hostMapEntries.Count - 1));
        }
    }

    private bool SelectHostMapEntry(string? levelName)
    {
        if (string.IsNullOrWhiteSpace(levelName))
        {
            return false;
        }

        var index = _hostMapEntries.FindIndex(entry => entry.LevelName.Equals(levelName, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return false;
        }

        _hostMapIndex = index;
        return true;
    }

    private int FindDefaultHostMapIndex()
    {
        var truefortIndex = _hostMapEntries.FindIndex(entry => entry.LevelName.Equals("Truefort", StringComparison.OrdinalIgnoreCase));
        return truefortIndex >= 0 ? truefortIndex : 0;
    }

    private OpenGarrisonMapRotationEntry? GetSelectedHostMapEntry()
    {
        return _hostMapIndex >= 0 && _hostMapIndex < _hostMapEntries.Count
            ? _hostMapEntries[_hostMapIndex]
            : null;
    }

    private string GetHostStockRotationSummary()
    {
        var orderedNames = OpenGarrisonStockMapCatalog.GetOrderedIncludedMapLevelNames(_hostMapEntries);
        if (orderedNames.Count == 0)
        {
            return "Stock rotation: no maps selected.";
        }

        var preview = string.Join(" -> ", orderedNames.Take(4));
        if (orderedNames.Count > 4)
        {
            preview += " ...";
        }

        return $"Stock rotation: {preview}";
    }
}

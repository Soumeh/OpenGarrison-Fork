#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.IO;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void UpdateMainMenu(MouseState mouse)
    {
        const float xbegin = 40f;
        const float ybegin = 300f;
        const float spacing = 30f;
        const float width = 200f;
        const int items = 6;

        if (mouse.X > xbegin && mouse.X < xbegin + width)
        {
            _mainMenuHoverIndex = (int)MathF.Round((mouse.Y - ybegin) / spacing);
            if (_mainMenuHoverIndex < 0 || _mainMenuHoverIndex >= items)
            {
                _mainMenuHoverIndex = -1;
            }
        }
        else
        {
            _mainMenuHoverIndex = -1;
        }

        var clickPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed;
        if (!clickPressed || _mainMenuHoverIndex < 0)
        {
            return;
        }

        switch (_mainMenuHoverIndex)
        {
            case 0:
                OpenHostSetupMenu();
                break;
            case 1:
                OpenLobbyBrowser();
                break;
            case 2:
                _manualConnectOpen = true;
                _editingConnectHost = true;
                _editingConnectPort = false;
                _optionsMenuOpen = false;
                _controlsMenuOpen = false;
                CloseLobbyBrowser(clearStatus: false);
                _creditsOpen = false;
                _editingPlayerName = false;
                _menuStatusMessage = string.Empty;
                break;
            case 3:
                _manualConnectOpen = false;
                CloseLobbyBrowser(clearStatus: false);
                _creditsOpen = false;
                OpenOptionsMenu(fromGameplay: false);
                break;
            case 4:
                _creditsOpen = true;
                _manualConnectOpen = false;
                CloseLobbyBrowser(clearStatus: false);
                _optionsMenuOpen = false;
                _controlsMenuOpen = false;
                _editingPlayerName = false;
                _menuStatusMessage = string.Empty;
                break;
            case 5:
                Exit();
                break;
        }
    }

    private void DrawMainMenu()
    {
        var viewportWidth = _graphics.PreferredBackBufferWidth;
        var viewportHeight = _graphics.PreferredBackBufferHeight;

        if (_menuBackgroundTexture is null)
        {
            var path = ProjectSourceLocator.FindFile(Path.Combine("OpenGarrison.Core", "Content", "Sprites", "Menu", "Title", "background.jpg"));
            if (path is not null && File.Exists(path))
            {
                using var stream = File.OpenRead(path);
                _menuBackgroundTexture = Texture2D.FromStream(GraphicsDevice, stream);
            }
        }

        if (_menuBackgroundTexture is not null)
        {
            _spriteBatch.Draw(_menuBackgroundTexture, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.White);
        }
        else if (!TryDrawScreenSprite("MenuBackgroundS", _menuImageFrame, new Vector2(viewportWidth / 2f, viewportHeight / 2f), Color.White, Vector2.One))
        {
            _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), new Color(26, 24, 20));
        }

        if (_optionsMenuOpen)
        {
            DrawOptionsMenu();
            DrawDevMessagePopup();
            return;
        }

        if (_controlsMenuOpen)
        {
            DrawControlsMenu();
            DrawDevMessagePopup();
            return;
        }

        if (_hostSetupOpen)
        {
            DrawHostSetupMenu();
            DrawDevMessagePopup();
            return;
        }

        if (_creditsOpen)
        {
            DrawCreditsMenu();
            DrawDevMessagePopup();
            return;
        }

        if (_lobbyBrowserOpen)
        {
            DrawLobbyBrowserMenu();
            DrawDevMessagePopup();
            return;
        }

        if (_manualConnectOpen)
        {
            DrawManualConnectMenu();
            DrawDevMessagePopup();
            return;
        }

        string[] items = ["Host Game", "Join (lobby)", "Join (manual)", "Options", "Credits", "Quit"];
        var position = new Vector2(40f, 300f);
        for (var index = 0; index < items.Length; index += 1)
        {
            var color = index == _mainMenuHoverIndex ? Color.Red : Color.White;
            DrawBitmapFontText(items[index], position, color, 1f);
            position.Y += 30f;
        }

        DrawMenuStatusText();
        DrawDevMessagePopup();
    }

    private void UpdateCreditsMenu(KeyboardState keyboard, MouseState mouse)
    {
        if (keyboard.IsKeyDown(Keys.Escape) && !_previousKeyboard.IsKeyDown(Keys.Escape))
        {
            _creditsOpen = false;
            return;
        }

        var panel = GetCreditsPanelBounds();
        var backBounds = new Rectangle(panel.X + 30, panel.Bottom - 62, 180, 42);
        var clickPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed;
        if (clickPressed && backBounds.Contains(mouse.Position))
        {
            _creditsOpen = false;
        }
    }

    private void DrawCreditsMenu()
    {
        var viewportWidth = _graphics.PreferredBackBufferWidth;
        var viewportHeight = _graphics.PreferredBackBufferHeight;
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * 0.82f);

        var panel = GetCreditsPanelBounds();
        _spriteBatch.Draw(_pixel, panel, new Color(31, 33, 38, 238));
        _spriteBatch.Draw(_pixel, new Rectangle(panel.X, panel.Y, panel.Width, 3), new Color(210, 210, 210));
        _spriteBatch.Draw(_pixel, new Rectangle(panel.X, panel.Bottom - 3, panel.Width, 3), new Color(76, 76, 76));

        DrawBitmapFontText("Credits", new Vector2(panel.X + 30f, panel.Y + 26f), Color.White, 1.35f);

        string[] lines =
        [
            "Original Gang Garrison 2 Credit to Faucet Software",
            string.Empty,
            "Port (Alpha) by SenatorGraves"
        ];

        var drawY = panel.Y + 82f;
        foreach (var line in lines)
        {
            if (string.IsNullOrEmpty(line))
            {
                drawY += 16f;
                continue;
            }

            var scale = 0.98f;
            var color = new Color(240, 228, 196);
            DrawBitmapFontText(line, new Vector2(panel.X + 30f, drawY), color, scale);
            drawY += 22f;
        }

        DrawMenuButton(new Rectangle(panel.X + 30, panel.Bottom - 62, 180, 42), "Back", false);
    }

    private void DrawMenuStatusText()
    {
        if (string.IsNullOrWhiteSpace(_menuStatusMessage))
        {
            return;
        }

        DrawBitmapFontText(_menuStatusMessage, new Vector2(40f, 520f), new Color(235, 225, 180), 1f);
    }

    private Rectangle GetCreditsPanelBounds()
    {
        var viewportWidth = _graphics.PreferredBackBufferWidth;
        var viewportHeight = _graphics.PreferredBackBufferHeight;
        var width = Math.Min(680, viewportWidth - 120);
        var height = Math.Min(470, viewportHeight - 120);
        return new Rectangle((viewportWidth - width) / 2, (viewportHeight - height) / 2, width, height);
    }
}

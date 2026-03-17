#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void UpdateManualConnectMenu(KeyboardState keyboard, MouseState mouse)
    {
        if (keyboard.IsKeyDown(Keys.Escape) && !_previousKeyboard.IsKeyDown(Keys.Escape))
        {
            CloseManualConnectMenu(clearStatus: false);
            return;
        }

        if (keyboard.IsKeyDown(Keys.Tab) && !_previousKeyboard.IsKeyDown(Keys.Tab))
        {
            var editHost = !_editingConnectHost;
            _editingConnectHost = editHost;
            _editingConnectPort = !editHost;
        }

        var hostBounds = new Rectangle(420, 266, 440, 36);
        var portBounds = new Rectangle(420, 346, 180, 36);
        var connectBounds = new Rectangle(420, 426, 180, 42);
        var backBounds = new Rectangle(620, 426, 180, 42);

        var clickPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed;
        if (!clickPressed)
        {
            return;
        }

        var point = new Point(mouse.X, mouse.Y);
        if (hostBounds.Contains(point))
        {
            _editingConnectHost = true;
            _editingConnectPort = false;
        }
        else if (portBounds.Contains(point))
        {
            _editingConnectHost = false;
            _editingConnectPort = true;
        }
        else if (connectBounds.Contains(point))
        {
            TryConnectFromMenu();
        }
        else if (backBounds.Contains(point))
        {
            CloseManualConnectMenu(clearStatus: false);
        }
    }

    private void DrawManualConnectMenu()
    {
        var viewportWidth = _graphics.PreferredBackBufferWidth;
        var viewportHeight = _graphics.PreferredBackBufferHeight;
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * 0.78f);

        var panel = new Rectangle(360, 150, 560, 340);
        _spriteBatch.Draw(_pixel, panel, new Color(34, 35, 39, 235));
        _spriteBatch.Draw(_pixel, new Rectangle(panel.X, panel.Y, panel.Width, 3), new Color(210, 210, 210));
        _spriteBatch.Draw(_pixel, new Rectangle(panel.X, panel.Bottom - 3, panel.Width, 3), new Color(76, 76, 76));

        DrawBitmapFontText("Join (manual)", new Vector2(panel.X + 28f, panel.Y + 24f), Color.White, 1f);
        DrawBitmapFontText("Host", new Vector2(panel.X + 28f, panel.Y + 82f), Color.White, 1f);
        DrawBitmapFontText("Port", new Vector2(panel.X + 28f, panel.Y + 162f), Color.White, 1f);

        DrawMenuInputBox(new Rectangle(panel.X + 60, panel.Y + 116, 440, 36), _connectHostBuffer, _editingConnectHost);
        DrawMenuInputBox(new Rectangle(panel.X + 60, panel.Y + 196, 180, 36), _connectPortBuffer, _editingConnectPort);
        DrawMenuButton(new Rectangle(panel.X + 60, panel.Y + 276, 180, 42), "Connect", false);
        DrawMenuButton(new Rectangle(panel.X + 260, panel.Y + 276, 180, 42), "Back", false);

        if (!string.IsNullOrWhiteSpace(_menuStatusMessage))
        {
            DrawBitmapFontText(_menuStatusMessage, new Vector2(panel.X + 28f, panel.Bottom - 38f), new Color(230, 220, 180), 1f);
        }
    }

    private void DrawPasswordPrompt()
    {
        var viewportWidth = _graphics.PreferredBackBufferWidth;
        var viewportHeight = _graphics.PreferredBackBufferHeight;
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * 0.7f);

        var panelWidth = 520;
        var panelHeight = 220;
        var panel = new Rectangle(
            (viewportWidth - panelWidth) / 2,
            (viewportHeight - panelHeight) / 2,
            panelWidth,
            panelHeight);
        _spriteBatch.Draw(_pixel, panel, new Color(34, 35, 39, 240));
        _spriteBatch.Draw(_pixel, new Rectangle(panel.X, panel.Y, panel.Width, 3), new Color(210, 210, 210));
        _spriteBatch.Draw(_pixel, new Rectangle(panel.X, panel.Bottom - 3, panel.Width, 3), new Color(76, 76, 76));

        DrawBitmapFontText("Server Password", new Vector2(panel.X + 28f, panel.Y + 24f), Color.White, 1f);
        DrawBitmapFontText("Enter password to continue.", new Vector2(panel.X + 28f, panel.Y + 54f), new Color(200, 200, 200), 0.9f);

        var masked = new string('*', _passwordEditBuffer.Length);
        DrawMenuInputBox(new Rectangle(panel.X + 28, panel.Y + 92, panel.Width - 56, 36), masked, active: true);
        DrawBitmapFontText("Press Enter to submit, Esc to cancel.", new Vector2(panel.X + 28f, panel.Y + 142f), new Color(200, 200, 200), 0.85f);

        if (!string.IsNullOrWhiteSpace(_passwordPromptMessage))
        {
            DrawBitmapFontText(_passwordPromptMessage, new Vector2(panel.X + 28f, panel.Bottom - 36f), new Color(230, 220, 180), 0.9f);
        }
    }
}

#nullable enable

using Microsoft.Xna.Framework;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void DrawMenuInputBox(Rectangle bounds, string text, bool active)
    {
        _spriteBatch.Draw(_pixel, bounds, active ? new Color(64, 68, 74) : new Color(44, 46, 52));
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 2), active ? new Color(255, 116, 116) : new Color(125, 125, 125));
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Bottom - 2, bounds.Width, 2), new Color(20, 20, 20));
        var display = active ? text + "_" : text;
        DrawBitmapFontText(display, new Vector2(bounds.X + 8f, bounds.Y + 9f), Color.White, 1f);
    }

    private void DrawMenuButton(Rectangle bounds, string label, bool highlighted)
    {
        _spriteBatch.Draw(_pixel, bounds, highlighted ? new Color(120, 50, 50) : new Color(56, 58, 64));
        DrawBitmapFontText(label, new Vector2(bounds.X + 14f, bounds.Y + 12f), Color.White, 1f);
    }
}

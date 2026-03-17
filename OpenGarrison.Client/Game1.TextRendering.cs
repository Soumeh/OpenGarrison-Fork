#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void DrawBitmapFontText(string text, Vector2 position, Color color, float scale = 1f)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        LoadedGameMakerSprite? fontSprite;
        try
        {
            fontSprite = _runtimeAssets.GetSprite("OpenGarrisonFontS");
        }
        catch
        {
            DrawHudTextLeftAligned(text, position, color, scale);
            return;
        }

        if (fontSprite is null)
        {
            DrawHudTextLeftAligned(text, position, color, scale);
            return;
        }

        var cursor = position;
        foreach (var character in text)
        {
            if (character == ' ')
            {
                cursor.X += 4f * scale;
                continue;
            }

            var frameIndex = character - 33;
            if (frameIndex < 0 || frameIndex >= fontSprite.Frames.Count)
            {
                cursor.X += 4f * scale;
                continue;
            }

            var frame = fontSprite.Frames[frameIndex];
            _spriteBatch.Draw(frame, cursor, null, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            cursor.X += MathF.Max(1f, frame.Width - 1f) * scale;
        }
    }
}

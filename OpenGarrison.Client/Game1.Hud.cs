#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void DrawBitmapFontTextCentered(string text, Vector2 position, Color color, float scale)
    {
        var width = MeasureBitmapFontWidth(text, scale);
        DrawBitmapFontText(text, new Vector2(position.X - (width / 2f), position.Y), color, scale);
    }

    private void DrawBitmapFontTextRightAligned(string text, Vector2 position, Color color, float scale)
    {
        var width = MeasureBitmapFontWidth(text, scale);
        DrawBitmapFontText(text, new Vector2(position.X - width, position.Y), color, scale);
    }

    private float MeasureBitmapFontWidth(string text, float scale)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0f;
        }

        LoadedGameMakerSprite? fontSprite;
        try
        {
            fontSprite = _runtimeAssets.GetSprite("gg2FontS");
        }
        catch
        {
            return _consoleFont.MeasureString(text).X * scale;
        }

        if (fontSprite is null)
        {
            return _consoleFont.MeasureString(text).X * scale;
        }

        var width = 0f;
        foreach (var character in text)
        {
            if (character == ' ')
            {
                width += 4f * scale;
                continue;
            }

            var frameIndex = character - 33;
            if (frameIndex < 0 || frameIndex >= fontSprite.Frames.Count)
            {
                width += 4f * scale;
                continue;
            }

            width += MathF.Max(1f, fontSprite.Frames[frameIndex].Width - 1f) * scale;
        }

        return width;
    }

    private bool IsKeyPressed(KeyboardState keyboard, Keys key)
    {
        return keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);
    }

    private void UpdateScoreboardState(KeyboardState keyboard)
    {
        _scoreboardOpen = !_mainMenuOpen
            && !_inGameMenuOpen
            && !_optionsMenuOpen
            && !_controlsMenuOpen
            && !_consoleOpen
            && !_teamSelectOpen
            && !_classSelectOpen
            && keyboard.IsKeyDown(_inputBindings.ShowScoreboard);

        if (_scoreboardOpen)
        {
            if (_scoreboardAlpha < 0.99f)
            {
                _scoreboardAlpha = AdvanceOpeningAlpha(_scoreboardAlpha, 0.02f, 0.99f);
            }

            return;
        }

        if (_scoreboardAlpha > 0.02f)
        {
            _scoreboardAlpha = AdvanceClosingAlpha(_scoreboardAlpha, 0.02f);
        }
    }
}

#nullable enable

using Microsoft.Xna.Framework;
using System;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void SubmitChatMessage()
    {
        var text = _chatInput.Trim();
        if (!string.IsNullOrWhiteSpace(text))
        {
            if (_networkClient.IsConnected)
            {
                _networkClient.SendChat(text);
            }
            else
            {
                AppendChatLine(_world.LocalPlayer.DisplayName, text, (byte)_world.LocalPlayer.Team);
            }
        }

        _chatOpen = false;
        _chatSubmitAwaitingOpenKeyRelease = true;
        _chatInput = string.Empty;
    }

    private void AppendChatLine(string playerName, string text, byte team)
    {
        var line = string.IsNullOrWhiteSpace(playerName)
            ? text
            : $"{playerName}: {text}";
        var color = team switch
        {
            (byte)OpenGarrison.Core.PlayerTeam.Blue => new Color(150, 200, 255),
            (byte)OpenGarrison.Core.PlayerTeam.Red => new Color(255, 180, 170),
            _ => new Color(235, 235, 235),
        };
        _chatLines.Add(new ChatLine(line, color));
        while (_chatLines.Count > 6)
        {
            _chatLines.RemoveAt(0);
        }

        AddConsoleLine($"[chat] {line}");
    }

    private void AdvanceChatHud()
    {
        for (var index = _chatLines.Count - 1; index >= 0; index -= 1)
        {
            _chatLines[index].TicksRemaining -= 1;
            if (_chatLines[index].TicksRemaining <= 0)
            {
                _chatLines.RemoveAt(index);
            }
        }
    }

    private void DrawChatHud()
    {
        var baseX = 18f;
        var promptRectangle = new Rectangle(
            12,
            _graphics.PreferredBackBufferHeight - 118,
            Math.Max(280, _graphics.PreferredBackBufferWidth / 3),
            24);
        var baseY = promptRectangle.Y - 14f - Math.Max(0, (_chatLines.Count - 1) * 18f);
        for (var index = 0; index < _chatLines.Count; index += 1)
        {
            var line = _chatLines[index];
            var alpha = _chatOpen ? 1f : MathF.Min(1f, line.TicksRemaining / 120f);
            _spriteBatch.DrawString(_consoleFont, line.Text, new Vector2(baseX, baseY + index * 18f), line.Color * alpha);
        }

        if (!_chatOpen)
        {
            return;
        }

        _spriteBatch.Draw(_pixel, promptRectangle, new Color(8, 10, 12, 200));
        _spriteBatch.DrawString(_consoleFont, $"> {_chatInput}_", new Vector2(promptRectangle.X + 8, promptRectangle.Y + 3), new Color(255, 245, 210));
    }
}

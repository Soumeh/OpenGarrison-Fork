#nullable enable

using System.Collections.Generic;

namespace OpenGarrison.Client;

public partial class Game1
{
    private static bool ShouldProcessNetworkEvent(ulong eventId, HashSet<ulong> processedIds, Queue<ulong> processedOrder)
    {
        if (eventId == 0)
        {
            return true;
        }

        if (!processedIds.Add(eventId))
        {
            return false;
        }

        processedOrder.Enqueue(eventId);
        while (processedOrder.Count > ProcessedNetworkEventHistoryLimit)
        {
            processedIds.Remove(processedOrder.Dequeue());
        }

        return true;
    }
}

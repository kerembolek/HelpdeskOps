using System.Threading.Channels;

namespace HelpdeskOps.Services.Teams;

public record TeamsMessage(int TicketId, string Json);

// Saves only put cards on this queue; TeamsDeliveryService posts them in the background, so a slow or failing Teams
// never slows down or breaks a page
public class TeamsQueue
{
    // Bounded so an unreachable Teams cannot make the queue grow without limit; the oldest waiting card is dropped
    private readonly Channel<TeamsMessage> _channel = Channel.CreateBounded<TeamsMessage>(
        new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropOldest });

    public void Enqueue(TeamsMessage message) => _channel.Writer.TryWrite(message);

    public IAsyncEnumerable<TeamsMessage> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}

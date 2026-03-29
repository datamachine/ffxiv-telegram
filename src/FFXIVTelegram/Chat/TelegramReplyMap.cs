namespace FFXIVTelegram.Chat;

using System.Collections.Generic;

public sealed class TelegramReplyMap
{
    private readonly object gate = new();
    private readonly LinkedList<long> order = new();
    private readonly Dictionary<long, ReplyEntry> entries = new();
    private readonly int capacity;
    private readonly TimeSpan maxAge;
    private readonly Func<DateTimeOffset> utcNow;

    public TelegramReplyMap(int capacity, TimeSpan maxAge)
        : this(capacity, maxAge, static () => DateTimeOffset.UtcNow)
    {
    }

    internal TelegramReplyMap(int capacity, TimeSpan maxAge, Func<DateTimeOffset> utcNow)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        if (maxAge < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAge));
        }

        this.capacity = capacity;
        this.maxAge = maxAge;
        this.utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
    }

    public void Store(long messageId, ChatRoute route)
    {
        ArgumentNullException.ThrowIfNull(route);

        lock (this.gate)
        {
            this.PruneExpired(this.utcNow());

            if (this.entries.TryGetValue(messageId, out var existing))
            {
                this.order.Remove(existing.Node);
            }

            var node = this.order.AddLast(messageId);
            this.entries[messageId] = new ReplyEntry(route, this.utcNow(), node);
            this.PruneCapacity();
        }
    }

    public bool TryGetRoute(long messageId, out ChatRoute route)
    {
        lock (this.gate)
        {
            this.PruneExpired(this.utcNow());

            if (this.entries.TryGetValue(messageId, out var entry))
            {
                route = entry.Route;
                return true;
            }
        }

        route = null!;
        return false;
    }

    private void PruneCapacity()
    {
        while (this.entries.Count > this.capacity && this.order.First is not null)
        {
            var oldestId = this.order.First.Value;
            this.order.RemoveFirst();
            this.entries.Remove(oldestId);
        }
    }

    private void PruneExpired(DateTimeOffset now)
    {
        var expiredBefore = now - this.maxAge;
        var node = this.order.First;
        while (node is not null)
        {
            var next = node.Next;
            if (this.entries.TryGetValue(node.Value, out var entry) && entry.StoredAt <= expiredBefore)
            {
                this.entries.Remove(node.Value);
                this.order.Remove(node);
            }

            node = next;
        }
    }

    private sealed record ReplyEntry(ChatRoute Route, DateTimeOffset StoredAt, LinkedListNode<long> Node);
}

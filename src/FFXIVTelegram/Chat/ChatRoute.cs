namespace FFXIVTelegram.Chat;

public abstract record ChatRoute
{
    public static ChatRoute FreeCompany() => new FreeCompanyRoute();

    public static ChatRoute Party() => new PartyRoute();

    public static ChatRoute Tell(string target) => new TellRoute(target);

    public sealed record FreeCompanyRoute : ChatRoute;

    public sealed record PartyRoute : ChatRoute;

    public sealed record TellRoute : ChatRoute
    {
        public TellRoute(string target)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(target);
            this.Target = target;
        }

        public string Target { get; }
    }
}

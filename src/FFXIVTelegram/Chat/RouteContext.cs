namespace FFXIVTelegram.Chat;

public sealed record RouteContext(ChatRoute? LastActiveRoute, ChatRoute.TellRoute? LastTellRoute);

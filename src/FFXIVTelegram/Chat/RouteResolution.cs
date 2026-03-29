namespace FFXIVTelegram.Chat;

public sealed record RouteResolution(ChatRoute? Route, string MessageText, string? ErrorMessage)
{
    public bool IsSuccess => this.Route is not null;

    public static RouteResolution Success(ChatRoute route, string messageText) => new(route, messageText, null);

    public static RouteResolution Failure(string errorMessage) => new(null, string.Empty, errorMessage);
}

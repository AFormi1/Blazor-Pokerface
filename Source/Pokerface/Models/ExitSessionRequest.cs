namespace Pokerface.Models
{
    public record ExitSessionRequest(
          int SessionId,
          int PlayerId
      );
}

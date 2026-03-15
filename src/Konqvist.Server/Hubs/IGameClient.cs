namespace Konqvist.Server.Hubs;

public interface IGameClient
{
    Task GameStarted(GameStartedMessage message);

    Task DistrictClaimed(DistrictClaimedMessage message);

    Task DistrictOwnershipChanged(DistrictOwnershipChangedMessage message);

    Task PhaseChanged(PhaseChangedMessage message);

    Task VoteStarted(VoteStartedMessage message);

    Task VoteCast(VoteCastMessage message);

    Task VoteEnded(VoteEndedMessage message);

    Task ScoreUpdated(ScoreUpdatedMessage message);

    Task GameStateChanged(GameStateChangedMessage message);

    Task RoundEnded(RoundEndedMessage message);

    Task RunnerLoggedOut(RunnerLoggedOutMessage message);

    Task LocationUpdated(LocationUpdatedMessage message);

    Task RunnerStateChanged(RunnerStateChangedMessage message);
}

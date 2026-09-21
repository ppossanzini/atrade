using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Handlers.Evidence;

namespace AutoTrade.Trading.Handlers.Tests.Evidence
{
    /// <summary>
    /// Captures what the application tried to remember, without a store or a model. A test must not need a
    /// checkpoint on disk to check whether an episode was written.
    /// </summary>
    internal sealed class FakeOperationalEpisodeWriter : IOperationalEpisodeWriter
    {
        public List<OperationalEpisode> Episodes { get; } = new List<OperationalEpisode>();

        public Task RecordAsync(OperationalEpisode episode, CancellationToken cancellationToken)
        {
            Episodes.Add(episode);

            return Task.CompletedTask;
        }
    }
}

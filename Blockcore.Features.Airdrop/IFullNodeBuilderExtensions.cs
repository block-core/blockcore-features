using Blockcore.Builder;
using Blockcore.Configuration.Logging;
using Blockcore.Features.Consensus;
using Microsoft.Extensions.DependencyInjection;

namespace Blockcore.Features.Airdrop
{
    /// <summary>
    /// Extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static class IFullNodeBuilderExtensions
    {
        /// <summary>
        /// Configures the Airdrop feature.
        /// </summary>
        public static IFullNodeBuilder Airdrop(this IFullNodeBuilder fullNodeBuilder)
        {
            LoggingConfiguration.RegisterFeatureNamespace<AirdropFeature>("airdrop");

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                .AddFeature<AirdropFeature>()
                .DependOn<ConsensusFeature>()
                .FeatureServices(services =>
                {
                    services.AddSingleton(fullNodeBuilder);
                    services.AddSingleton<AirdropSettings>();
                    services.AddSingleton<Distribute>();
                    services.AddSingleton<Snapshot>();
                });
            });

            return fullNodeBuilder;
        }
    }
}
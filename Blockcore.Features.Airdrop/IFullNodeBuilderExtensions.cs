using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Features.Consensus;

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
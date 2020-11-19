using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Blockcore.Builder.Feature;
using Blockcore.Utilities;
using Blockcore.Signals;
using Blockcore.Configuration;
using Blockcore.AsyncWork;
using Blockcore.Features.Consensus.CoinViews;
using Blockcore.EventBus;
using Blockcore.EventBus.CoreEvents;
using Blockcore.Consensus.Chain;
using Blockcore.Networks;

namespace Blockcore.Features.Airdrop
{
    /// <summary>
    /// A feature that will take a snapshot of the UTXO set and create a json file with the results.
    /// </summary>
    public partial class AirdropFeature : FullNodeFeature
    {
        private readonly Network network;
        private readonly INodeLifetime nodeLifetime;
        private readonly ISignals signals;
        private readonly ChainIndexer chainIndexer;
        private readonly AirdropSettings airdropSettings;
        private readonly NodeSettings nodeSettings;
        private readonly DataStoreSerializer dBreezeSerializer;
        private readonly IAsyncProvider asyncProvider;
        private readonly CachedCoinView cachedCoinView;
        private SubscriptionToken blockConnectedSubscription;
        private readonly ILogger logger;
        private readonly Distribute distribute;
        private readonly Snapshot snapshot;

        public AirdropFeature(Network network, INodeLifetime nodeLifetime, ILoggerFactory loggerFactory, Distribute distribute, Snapshot snapshot , ISignals signals, ChainIndexer chainIndexer, AirdropSettings airdropSettings, NodeSettings nodeSettings, ICoinView cachedCoinView, DataStoreSerializer dBreezeSerializer, IAsyncProvider asyncProvider)
        {
            this.network = network;
            this.nodeLifetime = nodeLifetime;
            this.distribute = distribute;
            this.snapshot = snapshot;
            this.signals = signals;
            this.chainIndexer = chainIndexer;
            this.airdropSettings = airdropSettings;
            this.nodeSettings = nodeSettings;
            this.dBreezeSerializer = dBreezeSerializer;
            this.asyncProvider = asyncProvider;
            this.cachedCoinView = (CachedCoinView)cachedCoinView;
            logger = loggerFactory.CreateLogger(GetType().FullName);
        }

        public override Task InitializeAsync()
        {
            if (airdropSettings.SnapshotMode && airdropSettings.SnapshotHeight > 0)
            {
                blockConnectedSubscription = signals.Subscribe<BlockConnected>(BlockConnected);
            }

            if (airdropSettings.DistributeMode)
            {
                distribute.Initialize();

                asyncProvider.CreateAndRunAsyncLoop("airdrop-distribute", DistributeCoins, nodeLifetime.ApplicationStopping, TimeSpans.Minute, TimeSpans.FiveSeconds);
            }

            return Task.CompletedTask;
        }

        private Task DistributeCoins(CancellationToken cancellationToken)
        {
            return distribute.DistributeCoins(cancellationToken);
        }

        private void BlockConnected(BlockConnected blockConnected)
        {
            if (blockConnected.ConnectedBlock.ChainedHeader.Height == airdropSettings.SnapshotHeight)
            {
                snapshot.SnapshotCoins(blockConnected.ConnectedBlock.ChainedHeader);
            }
        }
    }
}

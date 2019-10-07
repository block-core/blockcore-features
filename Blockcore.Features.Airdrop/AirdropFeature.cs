using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.EventBus;
using Stratis.Bitcoin.EventBus.CoreEvents;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;

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
        private readonly DBreezeSerializer dBreezeSerializer;
        private readonly IAsyncProvider asyncProvider;
        private readonly CachedCoinView cachedCoinView;
        private SubscriptionToken blockConnectedSubscription;
        private readonly ILogger logger;
        private readonly Distribute distribute;
        private readonly Snapshot snapshot;

        public AirdropFeature(Network network, INodeLifetime nodeLifetime, ILoggerFactory loggerFactory, Distribute distribute, Snapshot snapshot , ISignals signals, ChainIndexer chainIndexer, AirdropSettings airdropSettings, NodeSettings nodeSettings, ICoinView cachedCoinView, DBreezeSerializer dBreezeSerializer, IAsyncProvider asyncProvider)
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
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public override Task InitializeAsync()
        {
            if (this.airdropSettings.SnapshotMode && this.airdropSettings.SnapshotHeight > 0)
            {
                this.blockConnectedSubscription = this.signals.Subscribe<BlockConnected>(this.BlockConnected);
            }

            if (this.airdropSettings.DistributeMode)
            {
                this.distribute.Initialize();

                this.asyncProvider.CreateAndRunAsyncLoop("airdrop-distribute", this.DistributeCoins, this.nodeLifetime.ApplicationStopping, TimeSpans.Minute, TimeSpans.FiveSeconds);
            }

            return Task.CompletedTask;
        }

        private Task DistributeCoins(CancellationToken cancellationToken)
        {
            return this.distribute.DistributeCoins(cancellationToken);
        }

        private void BlockConnected(BlockConnected blockConnected)
        {
            if (blockConnected.ConnectedBlock.ChainedHeader.Height == this.airdropSettings.SnapshotHeight)
            {
                this.snapshot.SnapshotCoins(blockConnected.ConnectedBlock.ChainedHeader);
            }
        }
    }
}

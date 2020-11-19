using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Blockcore.Base;
using Blockcore.Builder;
using Blockcore.Builder.Feature;
using Blockcore.Configuration;
using Blockcore.Configuration.Logging;
using Blockcore.Connection;
using Blockcore.Consensus;
using Blockcore.Interfaces;
using Blockcore.P2P.Protocol.Payloads;
using Blockcore.Utilities;
using Blockcore.Features.BlockStore;
using Blockcore.Signals;
using Blockcore.Features.BlockExplorer.Controllers;
using Blockcore.Consensus.Chain;

namespace Blockcore.Features.BlockExplorer
{
   public class BlockExplorerFeature : FullNodeFeature
   {
      private readonly ChainIndexer chain;

      //private readonly Signals signals;

      private readonly BlockStoreSignaled blockStoreSignaled;

      private readonly IConnectionManager connectionManager;

      private readonly StoreSettings storeSettings;

      private readonly IChainState chainState;

      /// <summary>Instance logger.</summary>
      private readonly ILogger logger;

      /// <summary>Factory for creating loggers.</summary>
      private readonly ILoggerFactory loggerFactory;

      private readonly IBlockStoreQueue blockStoreQueue;

      private readonly IConsensusManager consensusManager;

      public BlockExplorerFeature(
       ChainIndexer chain,
          IConnectionManager connectionManager,
          //Signals signals,
          BlockStoreSignaled blockStoreSignaled,
          ILoggerFactory loggerFactory,
          StoreSettings storeSettings,
          IChainState chainState,
          IBlockStoreQueue blockStoreQueue,
          INodeStats nodeStats,
          IConsensusManager consensusManager)
      {
         this.chain = chain;
         this.blockStoreQueue = blockStoreQueue;
         //this.signals = signals;
         this.blockStoreSignaled = blockStoreSignaled;
         this.connectionManager = connectionManager;
         logger = loggerFactory.CreateLogger(GetType().FullName);
         this.loggerFactory = loggerFactory;
         this.storeSettings = storeSettings;
         this.chainState = chainState;
         this.consensusManager = consensusManager;

         //nodeStats.RegisterStats(this.AddInlineStats, StatsType.Inline, 900);
      }

      //private void AddInlineStats(StringBuilder log)
      //{
      //    ChainedHeader highestBlock = this.chainState.BlockStoreTip;

      //    if (highestBlock != null)
      //    {
      //        string logString = $"BlockExplorer.Height: ".PadRight(LoggingConfiguration.ColumnLength + 1) + highestBlock.Height.ToString().PadRight(8) +
      //                     $" BlockExplorer.Hash: ".PadRight(LoggingConfiguration.ColumnLength - 1) + highestBlock.HashBlock;

      //        log.AppendLine(logString);
      //    }
      //}

      public override Task InitializeAsync()
      {
         //this.connectionManager.Parameters.TemplateBehaviors.Add(new BlockStoreBehavior(this.chain, this.chainState, this.loggerFactory, this.consensusManager));

         //// Signal to peers that this node can serve blocks.
         //this.connectionManager.Parameters.Services = (this.storeSettings.Prune ? NetworkPeerServices.Nothing : NetworkPeerServices.Network) | NetworkPeerServices.NODE_WITNESS;

         //this.signals.SubscribeForBlocksConnected(this.blockStoreSignaled);

         return Task.CompletedTask;
      }

      /// <inheritdoc />
      public override void Dispose()
      {
         //this.logger.LogInformation("Stopping BlockExplorer.");
         //this.blockStoreSignaled.Dispose();
      }
   }

   /// <summary>
   /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
   /// </summary>
   public static class FullNodeBuilderBlockStoreExtension
   {
      public static IFullNodeBuilder UseBlockExplorer(this IFullNodeBuilder fullNodeBuilder)
      {
         LoggingConfiguration.RegisterFeatureNamespace<BlockExplorerFeature>("db");

         fullNodeBuilder.ConfigureFeature(features =>
         {
            features
            .AddFeature<BlockExplorerFeature>()
            .DependOn<BlockStoreFeature>()
            .FeatureServices(services =>
            {
               services.AddSingleton<BlockExplorerController>();
               services.AddSingleton<TransactionStoreController>();
            });
         });

         return fullNodeBuilder;
      }
   }
}

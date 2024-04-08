using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Blockcore.Builder.Feature;
using Blockcore.Utilities;
using Blockcore.Signals;
using Blockcore.Configuration;
using Blockcore.AsyncWork;
using Blockcore.Features.Consensus.CoinViews;
using Blockcore.EventBus;
using Blockcore.EventBus.CoreEvents;
using Blockcore.Features.Wallet.Interfaces;
using Blockcore.Interfaces;
using Blockcore.Features.Wallet;
using Blockcore.Features.Wallet.Types;
using Blockcore.Networks;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.NBitcoin;

namespace Blockcore.Features.Airdrop
{
   public class Distribute
   {
      private readonly Network network;
      private readonly INodeLifetime nodeLifetime;
      private readonly AirdropSettings airdropSettings;
      private readonly NodeSettings nodeSettings;
      private readonly IWalletManager walletManager;
      private readonly IWalletTransactionHandler walletTransactionHandler;
      private readonly IBroadcasterManager broadcasterManager;
      private readonly IBlockStore blockStore;
      private readonly ILogger logger;

      private UtxoContext utxoContext;

      public Distribute(Network network, INodeLifetime nodeLifetime, ILoggerFactory loggerFactory, AirdropSettings airdropSettings, NodeSettings nodeSettings, IWalletManager walletManager, IWalletTransactionHandler walletTransactionHandler, IBroadcasterManager broadcasterManager, IBlockStore blockStore)
      {
         this.network = network;
         this.nodeLifetime = nodeLifetime;
         this.airdropSettings = airdropSettings;
         this.nodeSettings = nodeSettings;
         this.walletManager = walletManager;
         this.walletTransactionHandler = walletTransactionHandler;
         this.broadcasterManager = broadcasterManager;
         this.blockStore = blockStore;

         logger = loggerFactory.CreateLogger(GetType().FullName);
      }

      public void Initialize()
      {
         utxoContext = new UtxoContext(nodeSettings.DataDir, airdropSettings.SnapshotHeight.Value);
         utxoContext.Database.EnsureCreated();
      }

      public Task DistributeCoins(CancellationToken arg)
      {
         // Check for invalid db states
         if (utxoContext.DistributeOutputs.Any(d =>
             d.Status == DistributeStatus.Started ||
             d.Status == DistributeStatus.Failed))
         {
            logger.LogError("database is in an invalid state");
            return Task.CompletedTask;
         }

         // Check for distributed trx still in progress (unconfirmed yet)
         IQueryable<UTXODistribute> inProgress = utxoContext.DistributeOutputs.Where(d => d.Status == DistributeStatus.InProgress);

         if (inProgress.Any())
         {
            bool foundTrxNotInBlock = false;
            foreach (UTXODistribute utxoDistribute in inProgress)
            {
               Transaction trx = blockStore?.GetTransactionById(new uint256(utxoDistribute.Trxid));

               if (trx == null)
               {
                  foundTrxNotInBlock = true;
               }
               else
               {
                  utxoDistribute.Status = DistributeStatus.Complete;
               }
            }

            utxoContext.SaveChanges(true);

            if (foundTrxNotInBlock)
               return Task.CompletedTask;
         }

         // MANUAL: this part must be manually changed
         string walletName = "wal";
         string accountName = "account 0";
         string password = "123456";

         var accountReference = new WalletAccountReference(walletName, accountName);

         bool progress = utxoContext.DistributeOutputs.Any(d =>
             d.Status == DistributeStatus.Started ||
             d.Status == DistributeStatus.InProgress ||
             d.Status == DistributeStatus.Failed);

         if (progress)
         {
            logger.LogError("database is in an invalid state");
            return Task.CompletedTask;
         }

         IQueryable<UTXODistribute> outputs = utxoContext.DistributeOutputs.Where(d => d.Status == null || d.Status == DistributeStatus.NoStarted).Take(100);

         if (!outputs.Any())
            return Task.CompletedTask;

         List<UTXODistribute> dbOutputs = new List<UTXODistribute>();

         // mark the database items as started
         foreach (UTXODistribute utxoDistribute in outputs)
         {
            utxoDistribute.Status = DistributeStatus.Started;
            dbOutputs.Add(utxoDistribute);
         }

         utxoContext.SaveChanges(true);

         try
         {
            var recipients = new List<Recipient>();
            foreach (UTXODistribute utxoDistribute in dbOutputs)
            {
               try
               {
                  // MANUAL: this part must be manually changed

                  if (string.IsNullOrEmpty(utxoDistribute.Address))
                     continue;

                  // convert the script to the target address
                  Script script = BitcoinAddress.Create(utxoDistribute.Address, network).ScriptPubKey;
                  Script target = null;

                  TxDestination destination = script.GetDestination(network);
                  if (destination != null)
                  {
                     var wit = new BitcoinWitPubKeyAddress(new WitKeyId(destination.ToBytes()), network);

                     target = PayToWitPubKeyHashTemplate.Instance.GenerateScriptPubKey(wit);
                  }
                  else
                  {
                     PubKey[] pubkeys = script.GetDestinationPublicKeys(network);

                     target = pubkeys[0].GetSegwitAddress(network).ScriptPubKey;
                  }

                  // Apply any ratio to the value.
                  long amount = utxoDistribute.Value;

                  amount = amount / 10;

                  recipients.Add(new Recipient
                  {
                     ScriptPubKey = target,
                     Amount = amount
                  });
               }
               catch (Exception e)
               {
                  utxoDistribute.Error = e.Message;
                  utxoDistribute.Status = DistributeStatus.Failed;
                  utxoContext.SaveChanges(true);
                  return Task.CompletedTask;
               }
            }

            HdAddress change = walletManager.GetUnusedChangeAddress(accountReference);

            var context = new TransactionBuildContext(network)
            {
               AccountReference = accountReference,
               MinConfirmations = 0,
               WalletPassword = password,
               Recipients = recipients,
               ChangeAddress = change,
               FeeType = FeeType.Low
            };

            Transaction transactionResult = walletTransactionHandler.BuildTransaction(context);
            uint256 trxHash = transactionResult.GetHash();

            // broadcast to the network
            broadcasterManager.BroadcastTransactionAsync(transactionResult);

            // mark the database items as in progress
            foreach (UTXODistribute utxoDistribute in dbOutputs)
            {
               utxoDistribute.Status = DistributeStatus.InProgress;
               utxoDistribute.Trxid = trxHash.ToString();
            }

            utxoContext.SaveChanges(true);
         }
         catch (Exception e)
         {
            foreach (UTXODistribute utxoDistribute in dbOutputs)
            {
               utxoDistribute.Error = e.Message;
               utxoDistribute.Status = DistributeStatus.Failed;
            }

            utxoContext.SaveChanges(true);

            return Task.CompletedTask;
         }

         return Task.CompletedTask;
      }
   }
}

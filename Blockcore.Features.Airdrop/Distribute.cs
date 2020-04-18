using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
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
using Blockcore.Features.Wallet.Interfaces;
using Blockcore.Interfaces;
using Blockcore.Features.Wallet;

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

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public void Initialize()
        {
            this.utxoContext = new UtxoContext(this.nodeSettings.DataDir, this.airdropSettings.SnapshotHeight.Value);
            this.utxoContext.Database.EnsureCreated();
        }

        public Task DistributeCoins(CancellationToken arg)
        {
            // Check for invalid db states
            if (this.utxoContext.DistributeOutputs.Any(d =>
                d.Status == DistributeStatus.Started ||
                d.Status == DistributeStatus.Failed))
            {
                this.logger.LogError("database is in an invalid state");
                return Task.CompletedTask;
            }

            // Check for distributed trx still in progress (unconfirmed yet)
            var inProgress = this.utxoContext.DistributeOutputs.Where(d => d.Status == DistributeStatus.InProgress);

            if (inProgress.Any())
            {
                bool foundTrxNotInBlock = false;
                foreach (var utxoDistribute in inProgress)
                {
                    var trx = this.blockStore?.GetTransactionById(new uint256(utxoDistribute.Trxid));

                    if (trx == null)
                    {
                        foundTrxNotInBlock = true;
                    }
                    else
                    {
                        utxoDistribute.Status = DistributeStatus.Complete;
                    }
                }

                this.utxoContext.SaveChanges(true);

                if(foundTrxNotInBlock)
                    return Task.CompletedTask;
            }

            // MANUAL: this part must be manually changed
            var walletName = "wal";
            var accountName = "account 0";
            var password = "123456";

            var accountReference = new WalletAccountReference(walletName, accountName);

            var progress = this.utxoContext.DistributeOutputs.Any(d =>
                d.Status == DistributeStatus.Started ||
                d.Status == DistributeStatus.InProgress ||
                d.Status == DistributeStatus.Failed);
            
            if (progress)
            {
                this.logger.LogError("database is in an invalid state");
                return Task.CompletedTask;
            }

            var outputs = this.utxoContext.DistributeOutputs.Where(d => d.Status == null || d.Status == DistributeStatus.NoStarted).Take(100);

            if(!outputs.Any())
                return Task.CompletedTask;

            List<UTXODistribute> dbOutputs = new List<UTXODistribute>();

            // mark the database items as started
            foreach (UTXODistribute utxoDistribute in outputs)
            {
                utxoDistribute.Status = DistributeStatus.Started;
                dbOutputs.Add(utxoDistribute);
            }

            this.utxoContext.SaveChanges(true);

            try
            {
                var recipients = new List<Recipient>();
                foreach (UTXODistribute utxoDistribute in dbOutputs)
                {
                    try
                    {
                        // MANUAL: this part must be manually changed

                        if(string.IsNullOrEmpty(utxoDistribute.Address))
                            continue;

                        // convert the script to the target address
                        Script script = BitcoinAddress.Create(utxoDistribute.Address, this.network).ScriptPubKey;
                        Script target = null;

                        TxDestination destination = script.GetDestination(this.network);
                        if (destination != null)
                        {
                            var wit = new BitcoinWitPubKeyAddress(new WitKeyId(destination.ToBytes()), this.network);

                            target = PayToWitPubKeyHashTemplate.Instance.GenerateScriptPubKey(wit);
                        }
                        else
                        {
                            var pubkeys = script.GetDestinationPublicKeys(this.network);

                            target = pubkeys[0].GetSegwitAddress(this.network).ScriptPubKey;
                        }

                        // Apply any ratio to the value.
                        var amount = utxoDistribute.Value;

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
                        this.utxoContext.SaveChanges(true);
                        return Task.CompletedTask;
                    }
                }

                HdAddress change = this.walletManager.GetUnusedChangeAddress(accountReference);

                var context = new TransactionBuildContext(this.network)
                {
                    AccountReference = accountReference,
                    MinConfirmations = 0,
                    WalletPassword = password,
                    Recipients = recipients,
                    ChangeAddress = change,
                    UseSegwitChangeAddress = true,
                    FeeType = FeeType.Low
                };

                Transaction transactionResult = this.walletTransactionHandler.BuildTransaction(context);
                uint256 trxHash = transactionResult.GetHash();

                // broadcast to the network
                this.broadcasterManager.BroadcastTransactionAsync(transactionResult);

                // mark the database items as in progress
                foreach (UTXODistribute utxoDistribute in dbOutputs)
                {
                    utxoDistribute.Status = DistributeStatus.InProgress;
                    utxoDistribute.Trxid = trxHash.ToString();
                }

                this.utxoContext.SaveChanges(true);
            }
            catch (Exception e)
            {
                foreach (UTXODistribute utxoDistribute in dbOutputs)
                {
                    utxoDistribute.Error = e.Message;
                    utxoDistribute.Status = DistributeStatus.Failed;
                }
                this.utxoContext.SaveChanges(true);
                return Task.CompletedTask;
            }

            return Task.CompletedTask;
        }
    }
}
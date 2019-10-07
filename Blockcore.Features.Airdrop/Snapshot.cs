using System;
using System.Collections.Generic;
using DBreeze.DataTypes;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.BitcoinCore;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Utilities;

namespace Blockcore.Features.Airdrop
{
    public class Snapshot
    {
        private readonly Network network;
        private readonly INodeLifetime nodeLifetime;
        private readonly CachedCoinView cachedCoinView;
        private readonly DBreezeSerializer dBreezeSerializer;
        private readonly AirdropSettings airdropSettings;
        private readonly NodeSettings nodeSettings;
        private readonly ILogger logger;

        public Snapshot(Network network, INodeLifetime nodeLifetime, ILoggerFactory loggerFactory, ICoinView cachedCoinView, DBreezeSerializer dBreezeSerializer, AirdropSettings airdropSettings, NodeSettings nodeSettings)
        {
            this.network = network;
            this.nodeLifetime = nodeLifetime;
            this.cachedCoinView = (CachedCoinView)cachedCoinView;
            this.dBreezeSerializer = dBreezeSerializer;
            this.airdropSettings = airdropSettings;
            this.nodeSettings = nodeSettings;
            this.cachedCoinView = (CachedCoinView)cachedCoinView;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public void SnapshotCoins(ChainedHeader chainedHeader)
        {
            if (chainedHeader.Height == this.airdropSettings.SnapshotHeight)
            {
                // Take a snapshot of the chain.

                // From here consensus will stop advancing until the snapshot is done.
                this.logger.LogInformation("Starting snapshot at height {0}", chainedHeader.Height);

                // Ensure nothing is in cache.
                this.cachedCoinView.Flush(true);

                DBreezeCoinView dBreezeCoinView = (DBreezeCoinView)this.cachedCoinView.Inner; // ugly hack.

                UtxoContext utxoContext = new UtxoContext(this.nodeSettings.DataDir, this.airdropSettings.SnapshotHeight.Value);

                utxoContext.Database.EnsureCreated();

                Money total = 0;
                int count = 0;
                foreach (var item in this.IterateUtxoSet(dBreezeCoinView))
                {
                    if (item.TxOut.IsEmpty) 
                        continue;

                    if (count % 100 == 0 && this.nodeLifetime.ApplicationStopping.IsCancellationRequested)
                        return;

                    total += item.TxOut.Value;
                    count++;

                    var addressItem = GetAddress(this.network, item.TxOut.ScriptPubKey);

                    utxoContext.UnspentOutputs.Add(new UTXOSnapshot()
                    {
                        Trxid = item.OutPoint.ToString(),
                        Script = item.TxOut.ScriptPubKey.ToString(),
                        Value = item.TxOut.Value,
                        Address = addressItem.address,
                        ScriptType = addressItem.scriptType.ToString(),
                        Height = item.Height
                    });

                    if (count % 10000 == 0)
                    {
                        utxoContext.SaveChanges();
                    }

                    this.logger.LogInformation("OutPoint = {0} - TxOut = {1} total = {2} count = {3}", item.OutPoint, item.TxOut, total, count);
                }

                utxoContext.SaveChanges();

                this.logger.LogInformation("Finished snapshot");
            }
        }

        public static (TxOutType scriptType, string address) GetAddress(Network network, Script script)
        {
            var template = NBitcoin.StandardScripts.GetTemplateFromScriptPubKey(script);

            if (template == null)
                return (TxOutType.TX_NONSTANDARD, string.Empty);

            if (template.Type == TxOutType.TX_NONSTANDARD)
                return (TxOutType.TX_NONSTANDARD, string.Empty);

            if (template.Type == TxOutType.TX_NULL_DATA)
                return (template.Type, string.Empty);

            if (template.Type == TxOutType.TX_PUBKEY)
            {
                var pubkeys = script.GetDestinationPublicKeys(network);
                return (template.Type, pubkeys[0].GetAddress(network).ToString());
            }

            if (template.Type == TxOutType.TX_PUBKEYHASH ||
                template.Type == TxOutType.TX_SCRIPTHASH ||
                template.Type == TxOutType.TX_SEGWIT)
            {
                BitcoinAddress bitcoinAddress = script.GetDestinationAddress(network);
                if (bitcoinAddress != null)
                {
                    return (template.Type, bitcoinAddress.ToString());
                }
            }

            if (template.Type == TxOutType.TX_MULTISIG)
            {
                // TODO;
                return (template.Type, string.Empty);
            }

            if (template.Type == TxOutType.TX_COLDSTAKE)
            {
                //if (ColdStakingScriptTemplate.Instance.ExtractScriptPubKeyParameters(script, out KeyId hotPubKeyHash, out KeyId coldPubKeyHash))
                //{
                //    // We want to index based on both the cold and hot key
                //    return new[]
                //    {
                //        hotPubKeyHash.GetAddress(network).ToString(),
                //        coldPubKeyHash.GetAddress(network).ToString(),
                //    };
                //}

                return (template.Type, string.Empty);
            }

            // Fail the node in such cases (all script types must be covered)
            throw new Exception("Unknown script type");
        }

        public IEnumerable<(OutPoint OutPoint, TxOut TxOut, int Height)> IterateUtxoSet(DBreezeCoinView dBreezeCoinView)
        {
            using (DBreeze.Transactions.Transaction transaction = dBreezeCoinView.CreateTransaction())
            {
                transaction.SynchronizeTables("Coins");
                transaction.ValuesLazyLoadingIsOn = false;

                IEnumerable<Row<byte[], byte[]>> rows = transaction.SelectForward<byte[], byte[]>("Coins");

                foreach (Row<byte[], byte[]> row in rows)
                {
                    Coins coins = this.dBreezeSerializer.Deserialize<Coins>(row.Value);
                    uint256 trxHash = new uint256(row.Key);

                    for (int i = 0; i < coins.Outputs.Count; i++)
                    {
                        if (coins.Outputs[i] != null)
                        {
                            this.logger.LogDebug("UTXO for '{0}' position {1}.", trxHash, i);
                            yield return (new OutPoint(trxHash, i), coins.Outputs[i], (int)coins.Height);
                        }
                    }
                }
            }
        }
    }
}
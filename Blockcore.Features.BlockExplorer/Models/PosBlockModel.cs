using System;
using System.Linq;
using NBitcoin;
using Newtonsoft.Json;
using Blockcore.Controllers.Models;
using Blockcore.Utilities.JsonConverters;
using Blockcore.Consensus.BlockInfo;
using Blockcore.Consensus.Chain;

namespace Blockcore.Features.BlockExplorer.Models
{
    public class PosBlockModel
    {
        public PosBlockModel(Block block, ChainIndexer chain)
        {
            Hash = block.GetHash().ToString();
            Size = block.ToBytes().Length;
            Version = block.Header.Version;
            Bits = block.Header.Bits.ToCompact().ToString("x8");
            Time = block.Header.BlockTime;
            Nonce = block.Header.Nonce;
            PreviousBlockHash = block.Header.HashPrevBlock.ToString();
            MerkleRoot = block.Header.HashMerkleRoot.ToString();
            Difficulty = block.Header.Bits.Difficulty;
            Transactions = block.Transactions.Select(trx => new TransactionVerboseModel(trx, chain.Network)).ToArray();
            Height = chain.GetHeader(block.GetHash()).Height;
        }

        /// <summary>
        /// Creates a block model
        /// Used for deserializing from Json
        /// </summary>
        public PosBlockModel()
        {
        }

        [JsonProperty("hashproof")]
        public uint256 HashProof { get; set; }

        [JsonProperty("stakemodifierv2")]
        public uint256 StakeModifierV2 { get; set; }

        [JsonProperty("staketime")]
        public uint StakeTime { get; set; }

        [JsonProperty("hash")]
        public string Hash { get; private set; }

        [JsonProperty("size")]
        public int Size { get; private set; }

        [JsonProperty("version")]
        public int Version { get; private set; }

        [JsonProperty("bits")]
        public string Bits { get; private set; }

        [JsonProperty("time")]
        [JsonConverter(typeof(DateTimeOffsetConverter))]
        public DateTimeOffset Time { get; private set; }

        [JsonProperty("tx")]
        public TransactionVerboseModel[] Transactions { get; set; }

        [JsonProperty("difficulty")]
        public double Difficulty { get; private set; }

        [JsonProperty("merkleroot")]
        public string MerkleRoot { get; private set; }

        [JsonProperty("previousblockhash")]
        public string PreviousBlockHash { get; private set; }

        [JsonProperty("nonce")]
        public uint Nonce { get; private set; }

        [JsonProperty("height")]
        public int Height { get; private set; }
    }
}

using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Blockcore.Features.Airdrop
{
    public class UtxoContext : DbContext
    {
        private readonly string path;
        private readonly int height;

        public DbSet<UTXOSnapshot> UnspentOutputs { get; set; }

        public DbSet<UTXODistribute> DistributeOutputs { get; set; }

        public UtxoContext(string path, int height)
        {
            this.path = path;
            this.height = height;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite($@"Data Source={this.path}\snapshot-{this.height}.db");
        }
    }

    public class UTXOSnapshot
    {
        [Key]
        public string Trxid { get; set; }

        public string Script { get; set; }
        public string Address { get; set; }
        public string ScriptType { get; set; }
        public long Value { get; set; }
        public int Height { get; set; }
    }

    public class UTXODistribute
    {
        [Key]
        public string Address { get; set; }

        public string Script { get; set; }
        public string ScriptType { get; set; }
        public long Value { get; set; }
        public int Height { get; set; }
        public string Status { get; set; }
        public string Trxid { get; set; }
        public string Error { get; set; }

    }

    public class DistributeStatus
    {
        public const string NoStarted = "";
        public const string Started = "started";
        public const string InProgress = "inprogress";
        public const string Complete = "complete";
        public const string Failed = "failed";
    }
}
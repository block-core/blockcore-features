using System.Text;
using Blockcore.Configuration;
using Blockcore.Utilities;
using Microsoft.Extensions.Logging;
using NBitcoin;

namespace Blockcore.Features.Airdrop
{
    /// <summary>
    /// Configuration related to the the airdrop feature.
    /// </summary>
    public class AirdropSettings
    {
        /// <summary>Defines block height at wich to create the airdrop.</summary>
        public int? SnapshotHeight { get; set; }

        public bool SnapshotMode { get; set; }

        public bool DistributeMode { get; set; }

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>
        /// Initializes an instance of the object from the node configuration.
        /// </summary>
        /// <param name="nodeSettings">The node configuration.</param>
        public AirdropSettings(NodeSettings nodeSettings)
        {
            Guard.NotNull(nodeSettings, nameof(nodeSettings));
            
            this.logger = nodeSettings.LoggerFactory.CreateLogger(typeof(AirdropSettings).FullName);            
            TextFileConfiguration config = nodeSettings.ConfigReader;

            this.SnapshotMode = config.GetOrDefault<bool>("snapshot", false, this.logger);
            this.DistributeMode = config.GetOrDefault<bool>("distribute", false, this.logger);

            if (this.SnapshotMode && this.DistributeMode)
            {
                throw new ConfigurationException("-distribute and -snapshot can not be both enabled");
            }

            this.SnapshotHeight = config.GetOrDefault<int>("snapshotheight", 0, this.logger);

            if (this.SnapshotHeight == 0)
            {
                throw new ConfigurationException("-snapshotheight not found or invalid");

            }
        }

        /// <summary>Prints the help information on how to configure the DNS settings to the logger.</summary>
        /// <param name="network">The network to use.</param>
        public static void PrintHelp(Network network)
        {
            var builder = new StringBuilder();

            builder.AppendLine($"-snapshot                 Put the airdrop tool in snapshot mode");
            builder.AppendLine($"-snapshotheight=<1-max>   The height of the chain to take the snapshot form");
            builder.AppendLine($"-distribute               Put the airdrop tool in distribution mode");

            NodeSettings.Default(network).Logger.LogInformation(builder.ToString());
        }

        /// <summary>
        /// Get the default configuration.
        /// </summary>
        /// <param name="builder">The string builder to add the settings to.</param>
        /// <param name="network">The network to base the defaults off.</param>
        public static void BuildDefaultConfigurationFile(StringBuilder builder, Network network)
        {
            builder.AppendLine("####Airdrop Settings####");
            builder.AppendLine($"#The height of the chain to take the snapshot this will also be part of the database filename.");
            builder.AppendLine($"#-snapshotheight=0");
            builder.AppendLine($"#Snapshot mode, to enable the snapshot feature. the snapshot will be written to a SQLite file snapshot.db");
            builder.AppendLine($"#-snapshot");
            builder.AppendLine($"#Distribute mode, to start distribution. the distribution will read from the db snapshot.db and use the select query in file distribute.sql");
            builder.AppendLine($"#-distribute");
        }
    }
}

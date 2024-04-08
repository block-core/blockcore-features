using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Blockcore.Builder;
using Blockcore.Builder.Feature;
using Blockcore.Configuration;
using Blockcore.Configuration.Logging;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.EventBus;
using Blockcore.Features.Wallet;
using Blockcore.Features.Wallet.Events;
using Blockcore.Signals;
using Blockcore.Utilities;
using Microsoft.Extensions.Logging;

namespace Blockcore.Features.WalletNotify
{
   public class WalletNotifyFeature : FullNodeFeature
   {
      /// <summary>Instance logger.</summary>
      private readonly ILogger logger;

      private readonly ISignals signals;

      /// <summary>The settings for the wallet feature.</summary>
      private readonly WalletSettings walletSettings;

      private readonly NodeSettings nodeSettings;

      private SubscriptionToken transactionFoundSubscription;

      /// <summary>The shell command to execute.</summary>
      private string shellCommand;

      /// <summary>The shell arguments to send to the shell command.</summary>
      private string shellArguments;

      public WalletNotifyFeature(
          NodeSettings nodeSettings,
          WalletSettings walletSettings,
          ISignals signals,
          ILoggerFactory loggerFactory)
      {
         Guard.NotNull(walletSettings, nameof(walletSettings));
         Guard.NotNull(signals, nameof(signals));

         logger = loggerFactory.CreateLogger(GetType().FullName);
         this.nodeSettings = nodeSettings;
         this.walletSettings = walletSettings;
         this.signals = signals;
      }

      public override Task InitializeAsync()
      {
         string walletNotify = nodeSettings.ConfigReader.GetOrDefault<string>("walletnotify", null, logger);

         // Only enable this feature if specified in the settings.
         if (string.IsNullOrWhiteSpace(walletNotify))
         {
            return Task.CompletedTask;
         }

         logger.LogInformation($"-walletnotify was configured with command: {walletNotify}.");

         string[] cmdArray = walletNotify.Split(' ');

         shellCommand = cmdArray.First();
         shellArguments = string.Join(" ", cmdArray.Skip(1));

         transactionFoundSubscription = signals.Subscribe<TransactionFound>(ev => ProcessTransactionAndNotify(ev.FoundTransaction));

         logger.LogInformation($"-walletnotify was parsed as: {shellCommand} {shellArguments}");

         return Task.CompletedTask;
      }

      public void ProcessTransactionAndNotify(Transaction transaction)
      {
         try
         {
            string arguments = shellArguments.Replace("%s", transaction.ToString());
            logger.LogInformation($"-walletnotify running command: {shellCommand} {arguments}");
            RunCommand(shellCommand, arguments);
         }
         catch (Exception ex)
         {
            logger.LogError(ex, "Failed to parse and execute on -walletnotify.");
         }
      }

      public string RunCommand(string command, string args)
      {
         var startInfo = new ProcessStartInfo
         {
            FileName = command,
            Arguments = args,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            UseShellExecute = false,
            CreateNoWindow = true,
         };

         Process.Start(startInfo);
         return string.Empty;
      }
   }

   /// <summary>
   /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
   /// </summary>
   public static class FullNodeBuilderWalletNotifyExtension
   {
      public static IFullNodeBuilder UseWalletNotify(this IFullNodeBuilder fullNodeBuilder)
      {
         LoggingConfiguration.RegisterFeatureNamespace<WalletNotifyFeature>("walletnotify");

         fullNodeBuilder.ConfigureFeature(features =>
         {
            features
               .AddFeature<WalletNotifyFeature>()
               .FeatureServices(services =>
               {
             });
         });

         return fullNodeBuilder;
      }
   }
}

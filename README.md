# Blockcore Features

Here is a collection of extra features that can be used together with Blockcore. Please refer to the individual features for more information.

## Publishing to NuGet

By default any project you add to "blockcore-features" is tagged with IsPackable = true, the opposite of "blockcore" repo. So if you add a third party feature to that repo, they will be published to our NuGet account.

## [Airdrop Tool](/Blockcore.Features.Airdrop)

A helper tool for airdrops it can create UTXO snapshots and distribute coins.

## [Wallet Notify](/Blockcore.Features.WalletNotify)

Adds the ability to perform shell executions when transactions is observed in the wallet.

## [Block Explorer](/Blockcore.Features.BlockExplorer)

Local block explorer that can be used in combination with txindex=1 enabled on the node. This feature has API that allows the wallet UI to show block information locally. This enables full privacy blockchain insight.


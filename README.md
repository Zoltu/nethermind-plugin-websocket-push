# nethermind-plugin-websocket-push
A Nethermind plugin that will create websocket endpoints that will push a stream of data to connected clients.
`/pending` will push all new pending transactions.
`/block` will push all new blocks.

Note: It is up to the receiver to filter client side and deal with reorgs.  This is a firehose plugin.

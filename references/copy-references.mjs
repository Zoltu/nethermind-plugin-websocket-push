import * as path from 'path'
import * as filesystem from 'fs/promises'
import * as url from 'url'

const __dirname = path.dirname(url.fileURLToPath(import.meta.url))
const nethermindSourceDirectory = path.join(__dirname, '..', '..', 'nethermind')
const referenceAssemblyPaths = [
	path.join('src', 'int256', 'src', 'Nethermind.Int256', 'bin', 'Release', 'netstandard2.1', 'ref', 'Nethermind.Int256.dll'),
	path.join('src', 'Nethermind', 'Nethermind.Abi', 'bin', 'Release', 'net5.0', 'ref', 'Nethermind.Abi.dll'),
	path.join('src', 'Nethermind', 'Nethermind.Api', 'bin', 'Release', 'net5.0', 'ref', 'Nethermind.Api.dll'),
	path.join('src', 'Nethermind', 'Nethermind.Blockchain', 'bin', 'Release', 'net5.0', 'ref', 'Nethermind.Blockchain.dll'),
	path.join('src', 'Nethermind', 'Nethermind.Config', 'bin', 'Release', 'net5.0', 'ref', 'Nethermind.Config.dll'),
	path.join('src', 'Nethermind', 'Nethermind.Consensus', 'bin', 'Release', 'net5.0', 'ref', 'Nethermind.Consensus.dll'),
	path.join('src', 'Nethermind', 'Nethermind.Core', 'bin', 'Release', 'net5.0', 'ref', 'Nethermind.Core.dll'),
	path.join('src', 'Nethermind', 'Nethermind.Crypto', 'bin', 'Release', 'net5.0', 'ref', 'Nethermind.Crypto.dll'),
	path.join('src', 'Nethermind', 'Nethermind.Db', 'bin', 'Release', 'net5.0', 'ref', 'Nethermind.Db.dll'),
	path.join('src', 'Nethermind', 'Nethermind.Evm', 'bin', 'Release', 'net5.0', 'ref', 'Nethermind.Evm.dll'),
	path.join('src', 'Nethermind', 'Nethermind.Facade', 'bin', 'Release', 'net5.0', 'ref', 'Nethermind.Facade.dll'),
	path.join('src', 'Nethermind', 'Nethermind.Grpc', 'bin', 'Release', 'net5.0', 'ref', 'Nethermind.Grpc.dll'),
	path.join('src', 'Nethermind', 'Nethermind.JsonRpc', 'bin', 'Release', 'net5.0', 'ref', 'Nethermind.JsonRpc.dll'),
	path.join('src', 'Nethermind', 'Nethermind.KeyStore', 'bin', 'Release', 'net5.0', 'ref', 'Nethermind.KeyStore.dll'),
	path.join('src', 'Nethermind', 'Nethermind.Logging', 'bin', 'Release', 'net5.0', 'ref', 'Nethermind.Logging.dll'),
	path.join('src', 'Nethermind', 'Nethermind.Mev', 'bin', 'Release', 'net5.0', 'ref', 'Nethermind.Mev.dll'),
	path.join('src', 'Nethermind', 'Nethermind.Monitoring', 'bin', 'Release', 'net5.0', 'ref', 'Nethermind.Monitoring.dll'),
	path.join('src', 'Nethermind', 'Nethermind.Network', 'bin', 'Release', 'net5.0', 'ref', 'Nethermind.Network.dll'),
	path.join('src', 'Nethermind', 'Nethermind.Network.Stats', 'bin', 'Release', 'net5.0', 'ref', 'Nethermind.Network.Stats.dll'),
	path.join('src', 'Nethermind', 'Nethermind.Serialization.Json', 'bin', 'Release', 'net5.0', 'ref', 'Nethermind.Serialization.Json.dll'),
	path.join('src', 'Nethermind', 'Nethermind.Sockets', 'bin', 'Release', 'net5.0', 'ref', 'Nethermind.Sockets.dll'),
	path.join('src', 'Nethermind', 'Nethermind.Specs', 'bin', 'Release', 'net5.0', 'ref', 'Nethermind.Specs.dll'),
	path.join('src', 'Nethermind', 'Nethermind.State', 'bin', 'Release', 'net5.0', 'ref', 'Nethermind.State.dll'),
	path.join('src', 'Nethermind', 'Nethermind.Synchronization', 'bin', 'Release', 'net5.0', 'ref', 'Nethermind.Synchronization.dll'),
	path.join('src', 'Nethermind', 'Nethermind.Trie', 'bin', 'Release', 'net5.0', 'ref', 'Nethermind.Trie.dll'),
	path.join('src', 'Nethermind', 'Nethermind.TxPool', 'bin', 'Release', 'net5.0', 'ref', 'Nethermind.TxPool.dll'),
	path.join('src', 'Nethermind', 'Nethermind.Wallet', 'bin', 'Release', 'net5.0', 'ref', 'Nethermind.Wallet.dll'),
]

async function main() {
	for (const referenceAssemblyPath of referenceAssemblyPaths) {
		const sourcePath = path.join(nethermindSourceDirectory, referenceAssemblyPath)
		const destinationPath = path.join(__dirname, '..', 'references', path.basename(sourcePath))
		await filesystem.copyFile(sourcePath, destinationPath)
	}
}

main()
	.then(() => process.exit())
	.catch(error => {
		console.log('An error occurred.');
		console.log(error);
		process.exit();
	});

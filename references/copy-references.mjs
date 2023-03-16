import * as path from 'path'
import * as filesystem from 'fs/promises'
import * as url from 'url'

const __dirname = path.dirname(url.fileURLToPath(import.meta.url))
const nethermindSourceDirectory = path.join(__dirname, '..', '..', 'nethermind')
const referenceAssemblyPaths = [
	path.join('src', 'Nethermind', 'Nethermind.Abi', 'obj', 'Release', 'net7.0', 'ref', 'Nethermind.Abi.dll'),
	path.join('src', 'Nethermind', 'Nethermind.Api', 'obj', 'Release', 'net7.0', 'ref', 'Nethermind.Api.dll'),
	path.join('src', 'Nethermind', 'Nethermind.Blockchain', 'obj', 'Release', 'net7.0', 'ref', 'Nethermind.Blockchain.dll'),
	path.join('src', 'Nethermind', 'Nethermind.Config', 'obj', 'Release', 'net7.0', 'ref', 'Nethermind.Config.dll'),
	path.join('src', 'Nethermind', 'Nethermind.Consensus', 'obj', 'Release', 'net7.0', 'ref', 'Nethermind.Consensus.dll'),
	path.join('src', 'Nethermind', 'Nethermind.Core', 'obj', 'Release', 'net7.0', 'ref', 'Nethermind.Core.dll'),
	path.join('src', 'Nethermind', 'Nethermind.Crypto', 'obj', 'Release', 'net7.0', 'ref', 'Nethermind.Crypto.dll'),
	path.join('src', 'Nethermind', 'Nethermind.Db', 'obj', 'Release', 'net7.0', 'ref', 'Nethermind.Db.dll'),
	path.join('src', 'Nethermind', 'Nethermind.Evm', 'obj', 'Release', 'net7.0', 'ref', 'Nethermind.Evm.dll'),
	path.join('src', 'Nethermind', 'Nethermind.Facade', 'obj', 'Release', 'net7.0', 'ref', 'Nethermind.Facade.dll'),
	path.join('src', 'Nethermind', 'Nethermind.Grpc', 'obj', 'Release', 'net7.0', 'ref', 'Nethermind.Grpc.dll'),
	path.join('src', 'Nethermind', 'Nethermind.JsonRpc', 'obj', 'Release', 'net7.0', 'ref', 'Nethermind.JsonRpc.dll'),
	path.join('src', 'Nethermind', 'Nethermind.KeyStore', 'obj', 'Release', 'net7.0', 'ref', 'Nethermind.KeyStore.dll'),
	path.join('src', 'Nethermind', 'Nethermind.Logging', 'obj', 'Release', 'net7.0', 'ref', 'Nethermind.Logging.dll'),
	path.join('src', 'Nethermind', 'Nethermind.Monitoring', 'obj', 'Release', 'net7.0', 'ref', 'Nethermind.Monitoring.dll'),
	path.join('src', 'Nethermind', 'Nethermind.Mev', 'obj', 'Release', 'net7.0', 'ref', 'Nethermind.Mev.dll'),
	path.join('src', 'Nethermind', 'Nethermind.Network', 'obj', 'Release', 'net7.0', 'ref', 'Nethermind.Network.dll'),
	path.join('src', 'Nethermind', 'Nethermind.Network.Stats', 'obj', 'Release', 'net7.0', 'ref', 'Nethermind.Network.Stats.dll'),
	path.join('src', 'Nethermind', 'Nethermind.Serialization.Json', 'obj', 'Release', 'net7.0', 'ref', 'Nethermind.Serialization.Json.dll'),
	path.join('src', 'Nethermind', 'Nethermind.Sockets', 'obj', 'Release', 'net7.0', 'ref', 'Nethermind.Sockets.dll'),
	path.join('src', 'Nethermind', 'Nethermind.Specs', 'obj', 'Release', 'net7.0', 'ref', 'Nethermind.Specs.dll'),
	path.join('src', 'Nethermind', 'Nethermind.State', 'obj', 'Release', 'net7.0', 'ref', 'Nethermind.State.dll'),
	path.join('src', 'Nethermind', 'Nethermind.Synchronization', 'obj', 'Release', 'net7.0', 'ref', 'Nethermind.Synchronization.dll'),
	path.join('src', 'Nethermind', 'Nethermind.Trie', 'obj', 'Release', 'net7.0', 'ref', 'Nethermind.Trie.dll'),
	path.join('src', 'Nethermind', 'Nethermind.TxPool', 'obj', 'Release', 'net7.0', 'ref', 'Nethermind.TxPool.dll'),
	path.join('src', 'Nethermind', 'Nethermind.Wallet', 'obj', 'Release', 'net7.0', 'ref', 'Nethermind.Wallet.dll'),
	path.join('src', 'Nethermind', 'Nethermind.Sockets', 'obj', 'Release', 'net7.0', 'ref', 'Nethermind.Sockets.dll'),
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

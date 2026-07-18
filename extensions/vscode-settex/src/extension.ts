import * as fs from 'fs';
import * as path from 'path';
import * as vscode from 'vscode';
import {
    LanguageClient,
    LanguageClientOptions,
    ServerOptions,
    TransportKind
} from 'vscode-languageclient/node';

let client: LanguageClient;

/**
 * Resolves the Settex language server DLL.
 * Prefers the copy bundled inside the packaged extension (server/), and falls
 * back to the source-tree build when running from the repository during
 * development.
 */
function resolveServerModule(context: vscode.ExtensionContext): string | undefined {
    const candidates = [
        // Bundled inside the packaged extension
        context.asAbsolutePath(path.join('server', 'Settex.LanguageServer.dll')),
        // Development location (running from the source tree)
        context.asAbsolutePath(
            path.join('..', '..', 'src', 'Settex.LanguageServer', 'bin', 'Debug', 'net10.0', 'Settex.LanguageServer.dll')
        )
    ];

    return candidates.find(candidate => fs.existsSync(candidate));
}

/**
 * Settex VS Code Extension
 * Provides syntax highlighting, snippets, and Language Server support for Settex files
 */
export function activate(context: vscode.ExtensionContext) {
    console.log('Settex extension is now active');

    // Language Server setup
    const serverModule = resolveServerModule(context);

    if (!serverModule) {
        vscode.window.showWarningMessage(
            'Settex language server not found. Syntax highlighting and snippets remain available, ' +
            'but IntelliSense and diagnostics are disabled.'
        );
        return;
    }

    const serverOptions: ServerOptions = {
        run: { 
            command: 'dotnet', 
            args: [serverModule],
            transport: TransportKind.stdio
        },
        debug: { 
            command: 'dotnet', 
            args: [serverModule],
            transport: TransportKind.stdio
        }
    };

    const clientOptions: LanguageClientOptions = {
        documentSelector: [{ scheme: 'file', language: 'settex' }],
        synchronize: {
            fileEvents: vscode.workspace.createFileSystemWatcher('**/*.settex')
        }
    };

    client = new LanguageClient(
        'settex',
        'Settex Language Server',
        serverOptions,
        clientOptions
    );

    client.start();
}

export function deactivate() {
    console.log('Settex extension is now deactivated');
    if (!client) {
        return undefined;
    }
    return client.stop();
}

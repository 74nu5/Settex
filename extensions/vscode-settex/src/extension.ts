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
 * Settex VS Code Extension
 * Provides syntax highlighting, snippets, and Language Server support for Settex files
 */
export function activate(context: vscode.ExtensionContext) {
    console.log('Settex extension is now active');

    // Register commands
    const helloCommand = vscode.commands.registerCommand('settex.helloWorld', () => {
        vscode.window.showInformationMessage('Hello from Settex!');
    });

    context.subscriptions.push(helloCommand);

    // Language Server setup
    const serverModule = context.asAbsolutePath(
        path.join('..', '..', 'src', 'Settex.LanguageServer', 'bin', 'Debug', 'net10.0', 'Settex.LanguageServer.dll')
    );

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

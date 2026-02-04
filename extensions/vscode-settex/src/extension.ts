import * as vscode from 'vscode';

/**
 * Settex VS Code Extension
 * Provides syntax highlighting, snippets, and basic language support for Settex files
 */
export function activate(context: vscode.ExtensionContext) {
    console.log('Settex extension is now active');

    // Register commands
    const helloCommand = vscode.commands.registerCommand('settex.helloWorld', () => {
        vscode.window.showInformationMessage('Hello from Settex!');
    });

    context.subscriptions.push(helloCommand);

    // TODO: Add Language Server client when Phase 3 is implemented
    // import { LanguageClient } from 'vscode-languageclient/node';
}

export function deactivate() {
    console.log('Settex extension is now deactivated');
}

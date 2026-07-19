import { execFile } from 'child_process';
import * as fs from 'fs';
import * as path from 'path';
import { promisify } from 'util';
import * as vscode from 'vscode';
import {
    LanguageClient,
    LanguageClientOptions,
    ServerOptions,
    TransportKind
} from 'vscode-languageclient/node';

const execFileAsync = promisify(execFile);

/** Download page shown when the required .NET runtime is missing. */
const DOTNET_DOWNLOAD_URL = 'https://dotnet.microsoft.com/download/dotnet/10.0';

/**
 * The shared-framework line prefix that indicates a .NET 10 runtime. The trailing
 * dot avoids matching e.g. "Microsoft.NETCore.App 100.x".
 */
const REQUIRED_RUNTIME_PREFIX = 'Microsoft.NETCore.App 10.';

let client: LanguageClient;

type RuntimeCheck = { ok: true } | { ok: false; detail: string };

/**
 * Verifies the .NET 10 runtime (which the language server needs) is installed by
 * running `dotnet --list-runtimes`. Returns a reason when it is unavailable so the
 * caller can show an actionable message instead of an opaque start failure.
 */
async function checkDotnetRuntime(): Promise<RuntimeCheck> {
    let stdout: string;

    try {
        ({ stdout } = await execFileAsync('dotnet', ['--list-runtimes']));
    } catch (err) {
        const reason = err instanceof Error ? err.message : String(err);
        return {
            ok: false,
            detail: `The .NET CLI ('dotnet') was not found on your PATH (${reason}).`
        };
    }

    const hasNet10 = stdout
        .split(/\r?\n/)
        .some(line => line.trim().startsWith(REQUIRED_RUNTIME_PREFIX));

    if (!hasNet10) {
        return {
            ok: false,
            detail: `The .NET 10 runtime was not found ('dotnet --list-runtimes' listed no ${REQUIRED_RUNTIME_PREFIX}x).`
        };
    }

    return { ok: true };
}

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
export async function activate(context: vscode.ExtensionContext) {
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

    // The server is a .NET 10 app. Check the runtime up front so a missing runtime
    // yields an actionable message instead of an opaque start failure.
    const runtimeCheck = await checkDotnetRuntime();

    if (!runtimeCheck.ok) {
        const openDownload = 'Download .NET 10';
        const choice = await vscode.window.showErrorMessage(
            `Settex IntelliSense could not start: the .NET 10 runtime is unavailable. ${runtimeCheck.detail} ` +
            'Syntax highlighting and snippets still work.',
            openDownload
        );

        if (choice === openDownload) {
            vscode.env.openExternal(vscode.Uri.parse(DOTNET_DOWNLOAD_URL));
        }

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

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

/** Extension id used when asking the .NET Install Tool for a runtime. */
const EXTENSION_ID = 'settex.settex';

/** Runtime major.minor the language server needs. */
const REQUIRED_RUNTIME_VERSION = '10.0';

type RuntimeCheck = { ok: true } | { ok: false; detail: string };

interface DotnetAcquireResult {
    dotnetPath: string;
}

/**
 * Asks the ".NET Install Tool for extension authors"
 * (ms-dotnettools.vscode-dotnet-runtime, a declared extensionDependency) for a
 * runtime. It keeps a private, user-local copy — no admin rights, no system-wide
 * install, and nothing extra shipped in this VSIX — and installs it on demand if
 * it isn't there yet. Returns the dotnet executable to launch the server with, or
 * undefined so the caller can fall back to a dotnet already on PATH.
 */
async function acquireDotnetRuntime(): Promise<string | undefined> {
    try {
        const result = await vscode.commands.executeCommand<DotnetAcquireResult>(
            'dotnet.acquire',
            { version: REQUIRED_RUNTIME_VERSION, requestingExtensionId: EXTENSION_ID }
        );

        const dotnetPath = result?.dotnetPath;

        // Verify the path before adopting it. A stale entry — a runtime the install
        // tool once provisioned and that has since been removed — would otherwise go
        // straight into ServerOptions.command and surface as an opaque ENOENT, with
        // none of the actionable messaging the PATH branch below provides. Falling
        // through to that branch is strictly better.
        if (!dotnetPath || !fs.existsSync(dotnetPath)) {
            return undefined;
        }

        return dotnetPath;
    } catch (err) {
        // The install tool is unavailable or the acquisition failed; the caller
        // falls back to whatever dotnet is on PATH.
        console.warn(`Settex: could not acquire a .NET ${REQUIRED_RUNTIME_VERSION} runtime`, err);
        return undefined;
    }
}

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

    // The server is a .NET 10 app. Prefer a runtime obtained through the .NET
    // Install Tool: it installs a private copy on demand, so the user never has to
    // install .NET by hand and nothing extra ships in this VSIX.
    let dotnetCommand = await acquireDotnetRuntime();

    if (!dotnetCommand) {
        // No managed runtime: fall back to a dotnet already on PATH, and only warn
        // if that one can't satisfy the server either.
        const runtimeCheck = await checkDotnetRuntime();

        if (!runtimeCheck.ok) {
            const openDownload = 'Download .NET 10';
            const choice = await vscode.window.showErrorMessage(
                `Settex IntelliSense could not start: the .NET ${REQUIRED_RUNTIME_VERSION} runtime is unavailable. ` +
                `${runtimeCheck.detail} Syntax highlighting and snippets still work.`,
                openDownload
            );

            if (choice === openDownload) {
                vscode.env.openExternal(vscode.Uri.parse(DOTNET_DOWNLOAD_URL));
            }

            return;
        }

        dotnetCommand = 'dotnet';
    }

    const serverOptions: ServerOptions = {
        run: {
            command: dotnetCommand,
            args: [serverModule],
            transport: TransportKind.stdio
        },
        debug: {
            command: dotnetCommand,
            args: [serverModule],
            transport: TransportKind.stdio
        }
    };

    // Registered so VS Code disposes it with the extension; it was previously created
    // and never tracked.
    const fileEvents = vscode.workspace.createFileSystemWatcher('**/*.settex');
    context.subscriptions.push(fileEvents);

    const clientOptions: LanguageClientOptions = {
        documentSelector: [{ scheme: 'file', language: 'settex' }],
        synchronize: { fileEvents }
    };

    client = new LanguageClient(
        'settex',
        'Settex Language Server',
        serverOptions,
        clientOptions
    );

    context.subscriptions.push(client);

    // Awaited rather than left floating: a server that fails to start surfaced as an
    // unhandled rejection in the extension host log, where nobody looks, instead of as
    // a message telling the user that IntelliSense is unavailable and why.
    try {
        await client.start();
    } catch (err) {
        const reason = err instanceof Error ? err.message : String(err);
        vscode.window.showErrorMessage(
            `Settex IntelliSense could not start: ${reason}. ` +
            'Syntax highlighting and snippets still work.'
        );
    }
}

export function deactivate() {
    console.log('Settex extension is now deactivated');
    if (!client) {
        return undefined;
    }
    return client.stop();
}

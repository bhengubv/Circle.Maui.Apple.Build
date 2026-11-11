const vscode = require('vscode');
const path = require('node:path');

function activate(context) {
  const tasks = (name, command, args, cwd) => vscode.tasks.executeTask(new vscode.Task(
    { type: 'circle-maui-apple' }, vscode.TaskScope.Workspace, name, 'Circle MAUI Apple',
    new vscode.ProcessExecution(command, args, { cwd })
  ));
  const select = async (label, extensions, folders = false) => (await vscode.window.showOpenDialog({
    openLabel: label, canSelectFiles: !folders, canSelectFolders: folders,
    filters: folders ? undefined : { Files: extensions }
  }))?.[0]?.fsPath;
  const add = (id, run) => context.subscriptions.push(vscode.commands.registerCommand(id, run));

  add('circleApple.add', async () => {
    const project = await select('Select MAUI project', ['csproj']);
    if (!project) return;
    await tasks('Add build package', 'dotnet', [
      'add', project, 'package', 'Circle.Maui.Apple.Build', '--version', '1.0.0',
      '--no-restore'
    ], path.dirname(project));
    vscode.window.showInformationMessage('Make sure the release package is available from a configured NuGet source before restoring.');
  });
  add('circleApple.build', async () => {
    if (process.platform !== 'darwin') {
      vscode.window.showInformationMessage('Run this command in a VS Code session on a Mac with Xcode and the MAUI workload.');
      return;
    }
    const project = await select('Select MAUI project', ['csproj']);
    if (!project) return;
    const destination = await vscode.window.showSaveDialog({ filters: { IPA: ['ipa'] } });
    if (!destination) return;
    await tasks('Build unsigned IPA', 'dotnet', [
      'publish', project, '-f', 'net10.0-ios', '-r', 'ios-arm64', '-c', 'Release',
      '-p:BuildIpa=true', '-p:ArchiveOnBuild=false', '-p:EnableCodeSigning=false',
      '-p:IpaPackagePath=' + destination.fsPath
    ], path.dirname(project));
  });
  add('circleApple.inspect', async () => {
    const ipa = await select('Select IPA', ['ipa']);
    if (!ipa) return;
    const bundleId = await vscode.window.showInputBox({ prompt: 'Expected Release bundle ID' });
    if (!bundleId?.trim()) return;
    const python = process.platform === 'win32' ? 'py' : 'python3';
    const args = process.platform === 'win32' ? ['-3'] : [];
    await tasks('Inspect IPA', python, [
      ...args, context.asAbsolutePath('tools/xtool-maui-ipa.py'), ipa, '--bundle-id', bundleId.trim()
    ], path.dirname(ipa));
  });
}

module.exports = { activate };

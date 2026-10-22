using System.CommandLine;

namespace Ampheck.Adb.Cli.Commands;

internal static class FileCommands {
    public static void AddTo(
        RootCommand root,
        AdbCliOptions options) {
        root.Add(CreatePush(options));
        root.Add(CreatePull(options));
        root.Add(CreateInstall(options));
        root.Add(CreateUninstall(options));
    }

    private static Command CreatePush(AdbCliOptions options) {
        var local = new Argument<string>("local");
        var remote = new Argument<string>("remote");
        var command = new Command(
            "push",
            "Copy a local file to the device.");
        command.Add(local);
        command.Add(remote);
        command.SetAction(async (parseResult, cancellationToken) =>
            await CommandExecution.RunAsync(async () => {
                var settings = options.Read(parseResult);
                var localPath = parseResult.GetValue(local)!;
                var remotePath = parseResult.GetValue(remote)!;
                if (!File.Exists(localPath)) {
                    throw new FileNotFoundException(
                        $"Local file '{localPath}' was not found.",
                        localPath);
                }

                var client = await AdbClientFactory.CreateAsync(
                        settings,
                        cancellationToken)
                    .ConfigureAwait(false);
                var device = await DeviceSelection.RequireOnlineAsync(
                        client,
                        settings,
                        cancellationToken)
                    .ConfigureAwait(false);
                var destination = remotePath.EndsWith('/')
                    ? remotePath + Path.GetFileName(localPath)
                    : remotePath;
                await device.PushAsync(
                        localPath,
                        destination,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                Console.WriteLine(
                    $"{localPath}: 1 file pushed to {destination}");
            }).ConfigureAwait(false));
        return command;
    }

    private static Command CreatePull(AdbCliOptions options) {
        var remote = new Argument<string>("remote");
        var local = new Argument<string>("local");
        var command = new Command(
            "pull",
            "Copy a device file to the local machine.");
        command.Add(remote);
        command.Add(local);
        command.SetAction(async (parseResult, cancellationToken) =>
            await CommandExecution.RunAsync(async () => {
                var settings = options.Read(parseResult);
                var remotePath = parseResult.GetValue(remote)!;
                var localPath = parseResult.GetValue(local)!;
                var client = await AdbClientFactory.CreateAsync(
                        settings,
                        cancellationToken)
                    .ConfigureAwait(false);
                var device = await DeviceSelection.RequireOnlineAsync(
                        client,
                        settings,
                        cancellationToken)
                    .ConfigureAwait(false);
                var info = await device.StatFileAsync(
                        remotePath,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (info.IsDirectory) {
                    throw new NotSupportedException(
                        "Directory pull is not supported.");
                }

                var destinationIsDirectory =
                    Directory.Exists(localPath) ||
                    Path.EndsInDirectorySeparator(localPath);
                var destination = destinationIsDirectory
                    ? Path.Combine(localPath, info.Name)
                    : localPath;
                if (destinationIsDirectory) {
                    Directory.CreateDirectory(localPath);
                }

                await device.PullAsync(
                        remotePath,
                        destination,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                Console.WriteLine(
                    $"{remotePath}: 1 file pulled to {destination}");
            }).ConfigureAwait(false));
        return command;
    }

    private static Command CreateInstall(AdbCliOptions options) {
        var apk = new Argument<string>("apk");
        var replace = new Option<bool>("--replace", "-r") {
            Description = "Replace an existing installation."
        };
        var downgrade = new Option<bool>("--downgrade", "-d") {
            Description = "Allow version downgrade."
        };
        var grant = new Option<bool>("--grant", "-g") {
            Description = "Grant runtime permissions."
        };
        var user = new Option<int?>("--user") {
            Description = "Install for a specific Android user."
        };
        var command = new Command(
            "install",
            "Install an APK.");
        command.Add(apk);
        command.Add(replace);
        command.Add(downgrade);
        command.Add(grant);
        command.Add(user);
        command.SetAction(async (parseResult, cancellationToken) =>
            await CommandExecution.RunAsync(async () => {
                var settings = options.Read(parseResult);
                var apkPath = parseResult.GetValue(apk)!;
                if (!File.Exists(apkPath)) {
                    throw new FileNotFoundException(
                        $"APK '{apkPath}' was not found.",
                        apkPath);
                }

                var client = await AdbClientFactory.CreateAsync(
                        settings,
                        cancellationToken)
                    .ConfigureAwait(false);
                var device = await DeviceSelection.RequireOnlineAsync(
                        client,
                        settings,
                        cancellationToken)
                    .ConfigureAwait(false);
                await device.InstallAsync(
                        apkPath,
                        new AdbInstallOptions {
                            ReplaceExisting =
                                parseResult.GetValue(replace),
                            AllowDowngrade =
                                parseResult.GetValue(downgrade),
                            GrantPermissions =
                                parseResult.GetValue(grant),
                            UserId = parseResult.GetValue(user)
                        },
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                Console.WriteLine("Success");
            }).ConfigureAwait(false));
        return command;
    }

    private static Command CreateUninstall(AdbCliOptions options) {
        var package = new Argument<string>("package");
        var keepData = new Option<bool>("--keep-data", "-k") {
            Description = "Keep application data and cache."
        };
        var command = new Command(
            "uninstall",
            "Uninstall an application.");
        command.Add(package);
        command.Add(keepData);
        command.SetAction(async (parseResult, cancellationToken) =>
            await CommandExecution.RunAsync(async () => {
                var settings = options.Read(parseResult);
                var packageName = parseResult.GetValue(package)!;
                var client = await AdbClientFactory.CreateAsync(
                        settings,
                        cancellationToken)
                    .ConfigureAwait(false);
                var device = await DeviceSelection.RequireOnlineAsync(
                        client,
                        settings,
                        cancellationToken)
                    .ConfigureAwait(false);
                var keep = parseResult.GetValue(keepData)
                    ? "-k"
                    : string.Empty;
                var output = await device.ExecuteShellAsync(
                        $"pm uninstall {keep} \"{packageName}\"",
                        cancellationToken)
                    .ConfigureAwait(false);
                Console.Out.Write(output);
                if (!output.Contains(
                    "Success",
                    StringComparison.OrdinalIgnoreCase)) {
                    throw new InvalidOperationException(
                        string.IsNullOrWhiteSpace(output)
                            ? "Package uninstall failed."
                            : output.Trim());
                }
            }).ConfigureAwait(false));
        return command;
    }
}

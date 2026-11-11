# Circle MAUI Apple Build

Install the NuGet package in a .NET 10 MAUI app when transitive Whisper desktop files cause Xcode's `install_name_tool` to reject a non-Mach-O library. The package filters desktop Whisper and ggml binaries during iOS and Mac Catalyst packaging and keeps the Apple mobile libraries.

Download the artifacts from [GitHub Releases](https://github.com/bhengubv/Circle.Maui.Apple.Build/releases). Place the NuGet package in a local feed directory and add that directory alongside your existing sources in `NuGet.Config`. Add a package reference to the MAUI app.

```xml
<PackageReference Include="Circle.Maui.Apple.Build" Version="1.0.0" />
```

Remove an equivalent custom filter before switching an existing project to the package. Check that restore and the Apple build succeed with the new package reference.

Install the Visual Studio VSIX with VSIX Installer for Visual Studio 2022 17.14 or Visual Studio 2026 on x64 Windows. It adds **Circle MAUI Apple Build: Add Package** and **Inspect IPA** to the Tools menu. In VS Code use **Extensions → Install from VSIX** and select the VS Code file. Its commands are under **Circle MAUI Apple** in the Command Palette. Both can add the package reference and inspect a built IPA with Python 3. Configure the NuGet source before restoring. VS Code on macOS can also build an unsigned IPA directly. Windows Visual Studio developers can use a paired Mac or build from the command line on a Mac.

## Build a device IPA

On a Mac with a compatible .NET 10 MAUI workload and Xcode, run this from your solution checkout.

```sh
dotnet publish path/to/App.csproj -f net10.0-ios -r ios-arm64 -c Release \
  -p:BuildIpa=true -p:ArchiveOnBuild=false -p:EnableCodeSigning=false \
  -p:IpaPackagePath="$PWD/App.ipa"
python3 tools/xtool-maui-ipa.py App.ipa --bundle-id com.example.app
```

Use the project's evaluated Release bundle ID. The helper checks the archive and declared device platform. For a separately authorized device test, configure xtool and USB access, use `xtool devices` to find the device, and append `--install-on DEVICE_UDID`. xtool uses development signing for that copy. Distribution requires Apple signing and a matching provisioning profile. Mac Catalyst uses its own build target.

Source and release binaries are MIT licensed. [xtool setup](https://xtool.sh/documentation/xtool/)

The MAUI sample in `samples/AppleBuildSample` references the package. Configure the local feed and restore the project before building the sample.

Run `Build-Release.ps1` on Windows with Visual Studio, .NET 10, and Node.js to produce the three release files and their SHA-256 checksums in `artifacts/`.

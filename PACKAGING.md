# Packaging & distribution

GlassSweeper ships as a **signed MSIX** package. The build produces a
**self-contained** `.msix` (the .NET + Windows App SDK runtimes are bundled),
so end users don't need to install any prerequisites — just trust the
certificate once and install.

## Build a signed MSIX

A code-signing certificate whose **subject exactly matches** the
`Publisher` in `GlassSweeper.App/Package.appxmanifest` (currently
`CN=AppPublisher`) is required.

### 1. Create a self-signed dev certificate (one-time)

```powershell
$cert = New-SelfSignedCertificate -Type Custom -Subject "CN=AppPublisher" `
  -KeyUsage DigitalSignature -FriendlyName "GlassSweeper Dev Signing" `
  -CertStoreLocation "Cert:\CurrentUser\My" `
  -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")
$cert.Thumbprint   # note this
```

### 2. Build the package

```powershell
# dotnet must be discoverable by MSBuild's SDK resolver:
$env:Path = "C:\Program Files\dotnet;$env:Path"

& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" `
  GlassSweeper.App\GlassSweeper.App.csproj /restore `
  /p:Configuration=Release /p:Platform=x64 `
  /p:AppxBundle=Never /p:UapAppxPackageBuildMode=SideloadOnly `
  /p:GenerateAppxPackageOnBuild=true `
  /p:AppxPackageDir="AppPackages\" `
  /p:AppxPackageSigningEnabled=true `
  /p:PackageCertificateThumbprint="<thumbprint from step 1>"
```

Output lands in `AppPackages\GlassSweeper.App_<version>_x64_Test\`:

| File | Purpose |
| --- | --- |
| `GlassSweeper.App_<ver>_x64.msix` | The installable, signed package |
| `GlassSweeper.App_<ver>_x64.cer` | Public certificate to trust before install |
| `Install.ps1` / `Add-AppDevPackage.ps1` | Helper scripts that trust the cert and install the package |

> `AppPackages/`, `*.msix`, `*.cer`, and `*.pfx` are git-ignored — build
> artifacts and certificates are never committed.

## Install (end user)

From the package folder, in an **elevated** PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File .\Install.ps1
```

This trusts the bundled certificate (into `LocalMachine\TrustedPeople`) and
installs the app. Alternatively, double-click `GlassSweeper.App_<ver>.msix`
after the certificate is trusted.

## Distributing to other people

A **self-signed** certificate means each recipient must trust it once
(the install script does this). For frictionless public distribution without
SmartScreen prompts, rebuild and sign with a certificate from a trusted CA
(or publish through the Microsoft Store), then hand out just the `.msix`.

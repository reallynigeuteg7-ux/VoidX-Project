param([string]$Toolchain = (Join-Path $PSScriptRoot '..\toolchain'), [string]$Output = (Join-Path $PSScriptRoot '..\..\outputs\VoidX-0.3.0.apk'))
$ErrorActionPreference = 'Stop'
$Toolchain = [IO.Path]::GetFullPath($Toolchain)
$jdk = (Get-ChildItem (Join-Path $Toolchain 'jdk') -Directory | Select-Object -First 1).FullName
$tools = Join-Path $Toolchain 'android-tools\android-15'
$android = Join-Path $Toolchain 'android-platform\android-35\android.jar'
$build = Join-Path $PSScriptRoot 'build'
$main = Join-Path $PSScriptRoot 'app\src\main'
function Run([string]$exe, [string[]]$arguments) { & $exe @arguments; if ($LASTEXITCODE -ne 0) { throw "Tool failed: $exe ($LASTEXITCODE)" } }
New-Item -ItemType Directory -Force -Path $build, "$build\classes", "$build\dex" | Out-Null
Run "$tools\aapt2.exe" @('compile', '--dir', "$main\res", '-o', "$build\resources.zip")
$manifest = (Get-Content "$main\AndroidManifest.xml" -Raw).Replace('<manifest ', '<manifest package="com.voidx.game" ')
[IO.File]::WriteAllText("$build\AndroidManifest.xml", $manifest)
Run "$tools\aapt2.exe" @('link', '-o', "$build\base.apk", '-I', $android, '--manifest', "$build\AndroidManifest.xml", '-A', "$main\assets", '--auto-add-overlay', "$build\resources.zip")
# Windows aapt2 may preserve backslashes in nested asset paths. Android assets use '/'.
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::Open("$build\base.apk", [IO.Compression.ZipArchiveMode]::Update)
try {
    foreach ($entry in @($archive.Entries)) {
        if ($entry.FullName.Contains('\')) {
            $normalized = $entry.FullName.Replace('\', '/')
            $buffer = [IO.MemoryStream]::new()
            $inputStream = $entry.Open()
            try { $inputStream.CopyTo($buffer) } finally { $inputStream.Dispose() }
            $entry.Delete()
            $newEntry = $archive.CreateEntry($normalized, [IO.Compression.CompressionLevel]::Optimal)
            $outputStream = $newEntry.Open()
            try { $buffer.Position = 0; $buffer.CopyTo($outputStream) } finally { $outputStream.Dispose(); $buffer.Dispose() }
        }
    }
} finally { $archive.Dispose() }
Run "$jdk\bin\javac.exe" @('-encoding','UTF-8','-source','8','-target','8','-bootclasspath',$android,'-d',"$build\classes","$main\java\com\voidx\game\MainActivity.java")
Run "$jdk\bin\jar.exe" @('cf', "$build\classes.jar", '-C', "$build\classes", '.')
Run "$jdk\bin\java.exe" @('-cp', "$tools\lib\d8.jar", 'com.android.tools.r8.D8', '--min-api', '26', '--lib', $android, '--output', "$build\dex", "$build\classes.jar")
Copy-Item "$build\base.apk" "$build\unsigned.apk" -Force
Run "$jdk\bin\jar.exe" @('uf', "$build\unsigned.apk", '-C', "$build\dex", 'classes.dex')
Run "$tools\zipalign.exe" @('-f','-p','4',"$build\unsigned.apk","$build\aligned.apk")
$key = Join-Path $Toolchain 'voidx-prototype.jks'
if (!(Test-Path $key)) { Run "$jdk\bin\keytool.exe" @('-genkeypair','-keystore',$key,'-alias','voidx','-storepass','android','-keypass','android','-keyalg','RSA','-keysize','2048','-validity','10000','-dname','CN=VoidX Prototype, O=VoidX, C=KZ') }
Run "$jdk\bin\java.exe" @('-jar', "$tools\lib\apksigner.jar", 'sign', '--v4-signing-enabled', 'false', '--ks', $key, '--ks-key-alias', 'voidx', '--ks-pass', 'pass:android', '--key-pass', 'pass:android', '--out', $Output, "$build\aligned.apk")
Run "$jdk\bin\java.exe" @('-jar', "$tools\lib\apksigner.jar", 'verify', '--verbose', $Output)
Run "$tools\aapt.exe" @('dump','badging',$Output)
Get-FileHash $Output -Algorithm SHA256

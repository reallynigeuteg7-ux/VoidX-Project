param(
    [ValidateSet('Validate','Windows','Android')] [string]$Target = 'Android',
    [string]$Editor = 'D:\Unity\6000.3.25f1\Editor\Unity.exe',
    [string]$AndroidTools = 'D:\Unity\Android',
    [string]$Output = "$PSScriptRoot\Builds\VoidX-0.4.2.apk",
    [string]$Keystore,
    [string]$KeystorePassword = $env:VOIDX_KEYSTORE_PASSWORD,
    [string]$Log = "$PSScriptRoot\Logs\build.log"
)
$ErrorActionPreference = 'Stop'
if ($Target -eq 'Windows' -and !$PSBoundParameters.ContainsKey('Output')) {$Output = "$PSScriptRoot\Builds\Windows\VoidX.exe"}
if (!(Test-Path -LiteralPath $Editor)) {throw "Unity Editor not found: $Editor"}
if ($Target -eq 'Android' -and (!(Test-Path -LiteralPath $Keystore) -or !$KeystorePassword)) {throw 'Supply the existing -Keystore and its password through VOIDX_KEYSTORE_PASSWORD or -KeystorePassword.'}
$env:VOIDX_ANDROID_TOOLS = $AndroidTools
$env:VOIDX_KEYSTORE = $Keystore
$env:VOIDX_KEYSTORE_PASSWORD = $KeystorePassword
$env:VOIDX_APK = $Output
$env:VOIDX_WINDOWS = $Output
$env:TEMP = 'D:\Unity\Temp'
$env:TMP = $env:TEMP
$env:GRADLE_USER_HOME = 'D:\Unity\GradleCache'
$env:UPM_CACHE_ROOT = 'D:\Unity\PackageCache'
New-Item -ItemType Directory -Force $env:TEMP,(Split-Path -Parent $Log) | Out-Null
$buildTarget = if ($Target -eq 'Android') {'Android'} else {'Win64'}
$arguments = @('-batchmode','-quit','-projectPath',('"'+$PSScriptRoot+'"'),'-buildTarget',$buildTarget,'-executeMethod',"VoidX.VoidXBuild.$Target",'-logFile',('"'+$Log+'"'))
$process = Start-Process -FilePath $Editor -ArgumentList $arguments -WindowStyle Hidden -PassThru
$process.WaitForExit()
if ($process.ExitCode -ne 0) {throw "Unity failed with exit code $($process.ExitCode). Read $Log"}
Write-Output "Unity $Target completed. Log: $Log"

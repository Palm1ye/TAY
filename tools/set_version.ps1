param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [switch]$Tag,
    [switch]$Push
)

Set-StrictMode -Version Latest

$root = Split-Path -Parent $PSScriptRoot

$propsPath = Join-Path $root "Directory.Build.props"
if (-not (Test-Path $propsPath)) {
    throw "Directory.Build.props not found at: $propsPath"
}

[xml]$props = Get-Content -Raw -Encoding UTF8 $propsPath
$propertyGroup = $props.SelectSingleNode("/Project/PropertyGroup")
if (-not $propertyGroup) {
    $propertyGroup = $props.CreateElement("PropertyGroup")
    $props.Project.AppendChild($propertyGroup) | Out-Null
}

$versionNode = $props.SelectSingleNode("/Project/PropertyGroup/TayVersion")
if (-not $versionNode) {
    $versionNode = $props.CreateElement("TayVersion")
    $propertyGroup.AppendChild($versionNode) | Out-Null
}

$versionNode.InnerText = $Version
$props.Save($propsPath)

$issPath = Join-Path $root "installer.iss"
if (Test-Path $issPath) {
    $iss = Get-Content -Raw -Encoding UTF8 $issPath
    $iss = $iss -replace '(?m)^; Version: .*', "; Version: $Version"
    $iss = $iss -replace '(?m)^#define MyAppVersion ".*"', "#define MyAppVersion `"$Version`""
    Set-Content -Path $issPath -Value $iss -Encoding UTF8
}

$readmePath = Join-Path $root "README.md"
if (Test-Path $readmePath) {
    $readme = Get-Content -Raw -Encoding UTF8 $readmePath
    $readme = $readme -replace '(?i)(Version:</strong>\s*)v[0-9A-Za-z\.-]+', "`${1}v$Version"
    Set-Content -Path $readmePath -Value $readme -Encoding UTF8
}

if ($Tag) {
    git tag "v$Version"
    if ($Push) {
        git push origin "v$Version"
    }
}

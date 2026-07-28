# Generate FormatFusion.Files.wxs by harvesting all files from the Publish directory
# Then invoke wix build to produce the .msi

$publishDir = Resolve-Path "..\Publish\FormatFusion"
$wxsOut     = ".\FormatFusion.Files.wxs"
$msiOut     = ".\Output\FormatFusion-1.0.0.msi"

Write-Host "Harvesting files from: $publishDir"

# Build WiX fragment XML
$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine('<?xml version="1.0" encoding="utf-8"?>')
[void]$sb.AppendLine('<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">')
[void]$sb.AppendLine('  <Fragment>')
[void]$sb.AppendLine('    <ComponentGroup Id="PublishFiles" Directory="INSTALLFOLDER">')

$files = Get-ChildItem $publishDir -Recurse -File
$i = 1
foreach ($f in $files) {
    $rel = $f.FullName.Substring($publishDir.Path.Length).TrimStart('\')
    $id  = "File_{0:D5}" -f $i
    $cid = "Comp_{0:D5}" -f $i
    # Subdirectory?
    $subDir = Split-Path $rel -Parent
    $src    = $f.FullName.Replace('\','\\')
    [void]$sb.AppendLine("      <Component Id=""$cid"" Guid=""*"">")
    if ($subDir) {
        [void]$sb.AppendLine("        <File Id=""$id"" Source=""$src"" Subdirectory=""$subDir"" />")
    } else {
        [void]$sb.AppendLine("        <File Id=""$id"" Source=""$src"" />")
    }
    [void]$sb.AppendLine("      </Component>")
    $i++
}

[void]$sb.AppendLine('    </ComponentGroup>')
[void]$sb.AppendLine('  </Fragment>')
[void]$sb.AppendLine('</Wix>')

$sb.ToString() | Set-Content $wxsOut -Encoding UTF8
Write-Host "Generated $wxsOut with $($i-1) files."

# Now build the MSI
New-Item -ItemType Directory -Force ".\Output" | Out-Null
Write-Host "Building MSI..."
$extPath = "$env:USERPROFILE\.wix\extensions\WixToolset.UI.wixext\7.0.0\wixext\WixToolset.UI.wixext.dll"
$args = @(
    "build"
    "FormatFusion.wxs"
    "FormatFusion.Files.wxs"
    "-ext", $extPath
    "-d", "PublishDir=$publishDir"
    "-bindpath", "$publishDir"
    "-o", $msiOut
)
Write-Host "Running: wix $($args -join ' ')"
& wix @args
if ($LASTEXITCODE -ne 0) { Write-Error "WiX build failed"; exit 1 }
Write-Host "MSI created: $msiOut"
$size = (Get-Item $msiOut).Length / 1MB
Write-Host ("MSI size: {0:N0} MB" -f $size)

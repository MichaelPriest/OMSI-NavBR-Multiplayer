[CmdletBinding()]
param(
    [string]$Root = "src/NavBR.Client"
)

$ErrorActionPreference = 'Stop'
$invalid = New-Object System.Collections.Generic.List[string]

foreach ($file in Get-ChildItem -LiteralPath $Root -Recurse -Filter '*.xaml' -File) {
    $text = Get-Content -LiteralPath $file.FullName -Raw
    foreach ($match in [regex]::Matches($text, '#[0-9A-Fa-f]+')) {
        $token = $match.Value
        if ($token.Length -notin @(4, 5, 7, 9)) {
            $line = ($text.Substring(0, $match.Index) -split "`n").Count
            $invalid.Add("$($file.FullName):$line invalid color token $token")
        }
    }
}

if ($invalid.Count -gt 0) {
    $invalid | ForEach-Object { Write-Error $_ }
    throw "Invalid WPF color token(s) found: $($invalid.Count)"
}

Write-Host 'WPF XAML color tokens validated.'

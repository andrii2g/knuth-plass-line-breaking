[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $OutputDirectory,

    [string] $ReferenceDirectory,

    [switch] $Flagship
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$expectedNames = @(
    'breakpoint-graph.svg'
    'comparison.html'
    'layout-comparison.svg'
    'summary.json'
    'trace.txt'
) | Sort-Object

function Get-VerifiedSummary {
    param([Parameter(Mandatory)][string] $Directory)

    if (-not (Test-Path -LiteralPath $Directory -PathType Container)) {
        throw "Artifact directory does not exist: $Directory"
    }

    $actualNames = @(
        Get-ChildItem -LiteralPath $Directory -File |
            Select-Object -ExpandProperty Name |
            Sort-Object
    )

    $differences = @(Compare-Object -ReferenceObject $expectedNames -DifferenceObject $actualNames)
    if ($differences.Count -ne 0) {
        throw "Artifact set differs from the required five files: $($differences | Out-String)"
    }

    $summaryPath = Join-Path $Directory 'summary.json'
    $summary = Get-Content -LiteralPath $summaryPath -Raw | ConvertFrom-Json
    if ($summary.schemaVersion -ne 1) {
        throw "Unexpected summary schema version: $($summary.schemaVersion)"
    }

    if (@($summary.algorithms).Count -ne 2) {
        throw 'Summary must contain both algorithms.'
    }

    $algorithmNames = @($summary.algorithms | ForEach-Object algorithm)
    if ($algorithmNames[0] -ne 'Greedy' -or $algorithmNames[1] -ne 'Knuth-Plass') {
        throw "Unexpected algorithm order: $($algorithmNames -join ', ')"
    }

    if (-not $summary.comparison.comparable) {
        throw 'Results must be comparable.'
    }

    foreach ($name in @('comparison.html', 'layout-comparison.svg', 'breakpoint-graph.svg')) {
        $path = Join-Path $Directory $name
        try {
            [void] [xml] (Get-Content -LiteralPath $path -Raw)
        }
        catch {
            throw "Artifact is not well-formed XML: $name. $($_.Exception.Message)"
        }
    }

    $tracePath = Join-Path $Directory 'trace.txt'
    $firstTraceLine = Get-Content -LiteralPath $tracePath -TotalCount 1
    if ($firstTraceLine -notlike 'Options *') {
        throw 'Trace must begin with the normalized options header.'
    }

    return $summary
}

function Assert-Near {
    param(
        [Parameter(Mandatory)][double] $Actual,
        [Parameter(Mandatory)][double] $Expected,
        [Parameter(Mandatory)][string] $Label
    )

    if ([Math]::Abs($Actual - $Expected) -gt 0.00000001) {
        throw "$Label was $Actual; expected $Expected."
    }
}

$summary = Get-VerifiedSummary -Directory $OutputDirectory

if ($Flagship) {
    $greedy = $summary.algorithms[0]
    $optimal = $summary.algorithms[1]
    $greedyPath = @($greedy.breakPath) -join ','
    $optimalPath = @($optimal.breakPath) -join ','

    if ($greedy.status -ne 'success' -or $optimal.status -ne 'success') {
        throw 'Both flagship algorithms must succeed.'
    }

    if ($greedyPath -ne '0,5,10,15,22,24') {
        throw "Unexpected flagship Greedy path: $greedyPath"
    }

    if ($optimalPath -ne '0,5,10,15,21,24') {
        throw "Unexpected flagship Knuth-Plass path: $optimalPath"
    }

    Assert-Near -Actual $greedy.metrics.totalDemerits -Expected 13312.5 -Label 'Greedy total demerits'
    Assert-Near -Actual $optimal.metrics.totalDemerits -Expected 1481.46 -Label 'Knuth-Plass total demerits'
    Assert-Near -Actual $summary.comparison.demeritDifference -Expected 11831.04 -Label 'Demerit difference'
    Assert-Near -Actual $summary.comparison.improvementPercent -Expected 88.87166197183103 -Label 'Improvement percent'
}

if (-not [string]::IsNullOrWhiteSpace($ReferenceDirectory)) {
    [void] (Get-VerifiedSummary -Directory $ReferenceDirectory)

    foreach ($name in $expectedNames) {
        $actualHash = (Get-FileHash -LiteralPath (Join-Path $OutputDirectory $name) -Algorithm SHA256).Hash
        $referenceHash = (Get-FileHash -LiteralPath (Join-Path $ReferenceDirectory $name) -Algorithm SHA256).Hash
        if ($actualHash -ne $referenceHash) {
            throw "Artifact is not byte-stable across runs: $name"
        }
    }

    Write-Host "Verified five byte-stable artifacts in $OutputDirectory and $ReferenceDirectory"
}
else {
    Write-Host "Verified five artifacts in $OutputDirectory"
}

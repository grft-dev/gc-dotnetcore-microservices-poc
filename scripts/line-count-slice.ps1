param(
    [string]$BaseSha = "6500daaf",
    [string]$HeadSha = "83db31210fec5cda5f6e6685148ad6606950836b",
    [switch]$ShowManifest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot

# Counting rule used in README:
# sum(bool(line.strip()) for line in text.splitlines())
function Get-NonEmptyLineCountFromGitFile {
    param(
        [Parameter(Mandatory = $true)][string]$Sha,
        [Parameter(Mandatory = $true)][string]$Path
    )

    $lines = & git -C $RepoRoot show "$Sha`:$Path" 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "Cannot read '$Path' at '$Sha'."
    }

    return @($lines | Where-Object { $_.Trim().Length -gt 0 }).Count
}

function Get-HeadManifest {
    return @(
        "ProductService/Repositories/SqlProductRepository.cs",
        "ProductService/Services/ProductCatalogService.cs",
        "PricingService/Repositories/SqlTariffRepository.cs",
        "PricingService/Services/PriceCalculationService.cs",
        "PricingService/Domain/TariffCalculator.cs",
        "PolicyService/Repositories/SqlPolicyRepository.cs",
        "PolicyService/Services/PolicySalesService.cs",
        "PolicySearchService/Repositories/SqlPolicySearchRepository.cs",
        "PolicySearchService/Services/PolicySearch.cs",
        "PaymentService/Repositories/SqlPaymentRepository.cs",
        "PaymentService/Services/PaymentAccountService.cs",
        "DashboardService/Repositories/SqlDashboardRepository.cs",
        "DashboardService/Services/SalesDashboardService.cs",
        "AuthService/Repositories/SqlAgentRepository.cs",
        "AuthService/Services/AgentAuthService.cs"
    )
}

function Get-BaseManifest {
    $services = @(
        "AuthService",
        "DashboardService",
        "PaymentService",
        "PolicySearchService",
        "PolicyService",
        "PricingService",
        "ProductService"
    )
    $subtrees = @("Domain", "Commands", "Queries", "DataAccess")
    $excluded = @(
        "PaymentService/Domain/BankStatementFile.cs",
        "PaymentService/Domain/InPaymentRegistrationService.cs"
    )

    $paths = & git -C $RepoRoot ls-tree -r --name-only $BaseSha
    if ($LASTEXITCODE -ne 0) {
        throw "Cannot list tree for '$BaseSha'."
    }

    $manifest = foreach ($path in $paths) {
        if (-not $path.EndsWith(".cs")) { continue }
        if ($excluded -contains $path) { continue }

        $parts = $path -split "/"
        if ($parts.Count -lt 3) { continue }
        if (-not ($services -contains $parts[0])) { continue }
        if (-not ($subtrees -contains $parts[1])) { continue }

        $path
    }

    return @($manifest | Sort-Object -Unique)
}

function Get-WholeBaseSevenServiceManifest {
    $services = @(
        "AuthService",
        "DashboardService",
        "PaymentService",
        "PolicySearchService",
        "PolicyService",
        "PricingService",
        "ProductService"
    )

    $paths = & git -C $RepoRoot ls-tree -r --name-only $BaseSha
    if ($LASTEXITCODE -ne 0) {
        throw "Cannot list tree for '$BaseSha'."
    }

    $manifest = foreach ($path in $paths) {
        if (-not $path.EndsWith(".cs")) { continue }
        $parts = $path -split "/"
        if ($parts.Count -lt 2) { continue }
        if (-not ($services -contains $parts[0])) { continue }
        $path
    }

    return @($manifest | Sort-Object -Unique)
}

function Measure-Manifest {
    param(
        [Parameter(Mandatory = $true)][string]$Sha,
        [Parameter(Mandatory = $true)][string[]]$Manifest
    )

    $rows = foreach ($path in $Manifest) {
        [PSCustomObject]@{
            Path  = $path
            Lines = Get-NonEmptyLineCountFromGitFile -Sha $Sha -Path $path
        }
    }

    return [PSCustomObject]@{
        Rows      = $rows
        FileCount = $rows.Count
        Total     = @($rows | Measure-Object -Property Lines -Sum).Sum
    }
}

$headManifest = Get-HeadManifest
$baseManifest = Get-BaseManifest
$wholeBaseManifest = Get-WholeBaseSevenServiceManifest

$headMeasure = Measure-Manifest -Sha $HeadSha -Manifest $headManifest
$baseMeasure = Measure-Manifest -Sha $BaseSha -Manifest $baseManifest
$wholeBaseMeasure = Measure-Manifest -Sha $BaseSha -Manifest $wholeBaseManifest

$repoLines = @($headMeasure.Rows | Where-Object { $_.Path -like "*/Repositories/*" } | Measure-Object -Property Lines -Sum).Sum
$nonRepoLines = $headMeasure.Total - $repoLines

$delta = $baseMeasure.Total - $headMeasure.Total
$reduction = if ($baseMeasure.Total -eq 0) { 0 } else { [math]::Round(($delta / $baseMeasure.Total) * 100, 2) }

Write-Host "Base SHA: $BaseSha"
Write-Host "Head SHA: $HeadSha"
Write-Host ""
Write-Host "Base slice: $($baseMeasure.Total) lines in $($baseMeasure.FileCount) files"
Write-Host "Head slice: $($headMeasure.Total) lines in $($headMeasure.FileCount) files"
Write-Host "Delta: $delta fewer lines"
Write-Host "Reduction: $reduction%"
Write-Host ""
Write-Host "Head slice composition:"
Write-Host "  Repositories: $repoLines"
Write-Host "  Service wrappers + TariffCalculator: $nonRepoLines"
Write-Host ""
Write-Host "Context only (not KPI): whole seven-service base = $($wholeBaseMeasure.Total) lines in $($wholeBaseMeasure.FileCount) files"

if ($ShowManifest) {
    Write-Host ""
    Write-Host "Head manifest:"
    foreach ($row in $headMeasure.Rows) {
        Write-Host ("  {0} [{1}]" -f $row.Path, $row.Lines)
    }

    Write-Host ""
    Write-Host "Base manifest:"
    foreach ($row in $baseMeasure.Rows) {
        Write-Host ("  {0} [{1}]" -f $row.Path, $row.Lines)
    }
}

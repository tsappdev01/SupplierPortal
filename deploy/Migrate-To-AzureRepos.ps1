<#
.SYNOPSIS
    One-time move of the whole Git repository (all branches, tags and history) from GitHub to
    Azure DevOps Repos, making Azure DevOps the main repository.

.DESCRIPTION
    Does a MIRROR clone of the GitHub repo and a MIRROR push to Azure Repos, so every branch, tag
    and commit is copied exactly - nothing is lost. Run it on a machine that can authenticate to BOTH
    remotes (e.g. your workstation or UATWEB01). Azure DevOps auth is easiest with a PAT:
        Azure DevOps > User settings > Personal access tokens > New token > scope "Code (Read & Write)".
    When Git prompts for a password on the Azure push, paste the PAT.

.PARAMETER GitHubUrl
    The source GitHub repo (defaults to this project's repo).

.PARAMETER AzureRepoUrl
    The destination Azure Repos clone URL. Get it from Azure DevOps > Repos > Clone. It looks like:
        https://<org>@dev.azure.com/<org>/<project>/_git/<repo>
    e.g. https://TechSourcedxb@dev.azure.com/TechSourcedxb/Supplier_Portal/_git/Supplier_Portal

.PARAMETER WorkDir
    Scratch folder for the temporary mirror clone (removed and recreated each run).

.EXAMPLE
    ./Migrate-To-AzureRepos.ps1 -AzureRepoUrl "https://TechSourcedxb@dev.azure.com/TechSourcedxb/Supplier_Portal/_git/Supplier_Portal"
#>
[CmdletBinding()]
param(
    [string] $GitHubUrl   = "https://github.com/tsappdev01/SupplierPortal.git",
    [Parameter(Mandatory = $true)] [string] $AzureRepoUrl,
    [string] $WorkDir     = (Join-Path $env:TEMP "sp-azure-migrate")
)

$ErrorActionPreference = "Stop"
Write-Host "== Migrate '$GitHubUrl' -> '$AzureRepoUrl' ==" -ForegroundColor Cyan

# Fresh scratch folder for the bare mirror clone.
if (Test-Path $WorkDir) { Remove-Item -Recurse -Force $WorkDir }
New-Item -ItemType Directory -Force -Path $WorkDir | Out-Null
$mirror = Join-Path $WorkDir "repo.git"

# 1. Mirror-clone GitHub (bare: every ref, no working tree).
Write-Host "Cloning (mirror) from GitHub..." -ForegroundColor Yellow
git clone --mirror $GitHubUrl $mirror
if ($LASTEXITCODE -ne 0) { throw "git clone --mirror failed." }

# 2. Push ONLY branches + tags to Azure Repos - never GitHub's refs/pull/*, which Azure DevOps rejects.
#    --prune keeps Azure's branch/tag set identical to the source; --force overwrites a stale earlier import.
Write-Host "Pushing branches + tags to Azure DevOps... (paste your PAT if prompted for a password)" -ForegroundColor Yellow
git --git-dir $mirror push --force --prune $AzureRepoUrl "refs/heads/*:refs/heads/*" "refs/tags/*:refs/tags/*"
if ($LASTEXITCODE -ne 0) { throw "git push to Azure failed (check the URL and your PAT scope: Code Read & Write)." }

Write-Host "== Done. Azure Repos now mirrors GitHub (all branches, tags, history). ==" -ForegroundColor Green
Write-Host "Next: point the pipeline at Azure Repos (Repos Git > this repo > /azure-pipelines.yml) and set your UATWEB01 agent pool." -ForegroundColor Green

# Azure DevOps — main repository + UAT pipeline

Azure DevOps is the **main repository** for this project. Code lives in **Azure Repos**; the **UAT
pipeline** (`/azure-pipelines.yml`) builds and deploys in place to the IIS sites on **UATWEB01** via a
self-hosted agent. (GitHub is kept only as a backup/mirror after the move.)

## 1. One-time move: GitHub → Azure Repos (all history)

Run on a machine that can reach **both** remotes (your workstation, or UATWEB01). Needs an Azure DevOps
**PAT** with scope **Code (Read & Write)** — User settings → Personal access tokens → New token.

```powershell
# Get the exact Azure clone URL from: Azure DevOps > Repos > Clone
./deploy/Migrate-To-AzureRepos.ps1 `
  -AzureRepoUrl "https://TechSourcedxb@dev.azure.com/TechSourcedxb/Supplier_Portal/_git/Supplier_Portal"
```

This mirror-clones GitHub and mirror-pushes to Azure, so every branch, tag and commit is copied exactly.
After it finishes, `main` on Azure Repos matches `main` on GitHub.

> If the Azure repo already has stale commits from an earlier import, the mirror push overwrites them so
> the destination becomes an exact copy of GitHub. Do this only when you intend Azure to match GitHub.

## 2. Create the pipeline against Azure Repos

1. **Pipelines → New pipeline → Azure Repos Git →** select this repo.
2. **Existing Azure Pipelines YAML file → Branch `main`, Path `/azure-pipelines.yml`.**
3. **Variables:** set `uatAgentPool` to the UATWEB01 agent pool (default `Default`).
4. **Run.** On the first run, permit the agent-pool resource if prompted (**View → Permit**).

The pipeline is manual by default (`trigger: none`). To deploy automatically on every push to Azure `main`,
replace `trigger: none` in `azure-pipelines.yml` with:

```yaml
trigger:
  branches: { include: [ main ] }
  paths:    { include: [ 'src/**', 'deploy/**', 'azure-pipelines.yml' ] }
```

## 3. Day-to-day after the move

- Clone from **Azure Repos** and push there; it is the source of truth.
- The pipeline builds from Azure Repos and deploys to UATWEB01 — see `azure-pipelines.yml` for the sites
  and `Deploy-Site-Local.ps1` for the in-place deploy (preserves each site's `appsettings.Local.json`,
  `keys\`, `doc\`, `logs\`). A pre-deploy guard (`Assert-SiteConfig.ps1`) fails fast if a site is missing
  its connection string / secret.
- GitHub can be left as a read-only backup, or archived once Azure is verified.

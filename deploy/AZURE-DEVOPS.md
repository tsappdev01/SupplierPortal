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

## 3. Keeping Azure in sync (GitHub is where changes land)

Changes are committed on **GitHub**; **Azure Repos is the primary** copy the team clones and the pipeline
builds from. Keep Azure current one of two ways:

- **Automatic (recommended):** the GitHub Action `.github/workflows/mirror-to-azure.yml` mirrors `main`
  (all branches, tags, history) to Azure on every push. Enable it once:
  1. GitHub repo → Settings → Secrets and variables → Actions → New repository secret
     `AZURE_DEVOPS_PAT` = an Azure PAT with scope **Code (Read & Write)**.
  2. (Only if your Repos clone URL differs from the default) add a repository **variable**
     `AZURE_REPO_URL` with that URL.
  It is a mirror, so **do not commit directly to Azure** while this is on — Azure follows GitHub.
- **Manual:** re-run `deploy/Migrate-To-AzureRepos.ps1 -AzureRepoUrl "<your url>"` whenever you want to sync.

## 4. Day-to-day

- The pipeline builds from Azure Repos and deploys to UATWEB01 — see `azure-pipelines.yml` for the sites
  and `Deploy-Site-Local.ps1` for the in-place deploy (preserves each site's `appsettings.Local.json`,
  `keys\`, `doc\`, `logs\`). A pre-deploy guard (`Assert-SiteConfig.ps1`) fails fast if a site is missing
  its connection string / secret.
- GitHub remains the place commits are made; Azure stays in sync via the Action above.

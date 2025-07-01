# Initialize Git repository if it doesn't exist
if (-not (Test-Path -Path ".git")) {
    Write-Host "Initializing Git repository..."
    git init
}

# Add all files to Git
Write-Host "Adding files to Git..."
git add .

# Commit changes
$commitMessage = Read-Host -Prompt "Enter commit message"
if ([string]::IsNullOrWhiteSpace($commitMessage)) {
    $commitMessage = "Initial commit"
}
Write-Host "Committing changes with message: $commitMessage"
git commit -m $commitMessage

# Add remote repository if it doesn't exist
$remoteExists = git remote -v | Select-String -Pattern "origin"
if (-not $remoteExists) {
    Write-Host "Adding remote repository..."
    git remote add origin https://github.com/rr-brian/ai-site-net.git
}

# Push to GitHub
Write-Host "Pushing to GitHub..."
git push -u origin main

Write-Host "Deployment complete!"

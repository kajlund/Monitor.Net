
$ErrorActionPreference = 'Stop'

$sshTarget = 'luka@dreamquest'
$remoteRoot = '/opt/dreamquest/monitor-dotnet'
$serviceName = 'monitor-dotnet.service'
$statusUrl = 'http://127.0.0.1:50100/status'

function Assert-Success($step) {
  if ($LASTEXITCODE -ne 0) {
    throw "$step failed (exit code $LASTEXITCODE)."
  }
}

function Test-Deployment {
  for ($attempt = 1; $attempt -le 10; $attempt++) {
    $body = ssh $sshTarget "curl -fs --max-time 20 $statusUrl"
    $exitCode = $LASTEXITCODE

    if ($exitCode -eq 0 -and
        ($body -join "`n") -match '<h1>\s*Server status\s*</h1>') {
      return $true
    }

    Start-Sleep -Seconds 1
  }

  return $false
}

$release = [Guid]::NewGuid().ToString('N')
$publishDir = Join-Path ([System.IO.Path]::GetTempPath()) "monitor-$release"

$remoteStage = "$remoteRoot/stage-$release"
$remoteCurrent = "$remoteRoot/current"
$remotePrevious = "$remoteRoot/previous"

try {
  Write-Host 'Publishing Monitor...'

  $project = Join-Path $PSScriptRoot 'Monitor.csproj'
  dotnet publish $project -c Release --self-contained false -o $publishDir
  Assert-Success 'Publish'

  Write-Host 'Uploading release...'

  ssh $sshTarget "mkdir -p $remoteStage"
  Assert-Success 'Create staging directory'

  scp -r "$publishDir/." "${sshTarget}:$remoteStage/"
  Assert-Success 'Upload'

  Write-Host 'Installing release...'

  ssh $sshTarget "chmod -R u=rwX,go=rX $remoteStage && test -f $remoteStage/Monitor.dll && rm -rf $remotePrevious && mv $remoteCurrent $remotePrevious && (mv $remoteStage $remoteCurrent || { mv $remotePrevious $remoteCurrent && false; })"
  Assert-Success 'Install'

  Write-Host 'Restarting and verifying service...'

  ssh $sshTarget "sudo -n /usr/bin/systemctl restart $serviceName"

  $restartSucceeded = $LASTEXITCODE -eq 0

  if (-not $restartSucceeded -or -not (Test-Deployment)) {
    Write-Warning 'Deployment failed. Restoring previous version...'

    ssh $sshTarget "test -f $remotePrevious/Monitor.dll && rm -rf $remoteCurrent && mv $remotePrevious $remoteCurrent && sudo -n /usr/bin/systemctl restart $serviceName"
    Assert-Success 'Rollback'

    if (-not (Test-Deployment)) {
      throw 'Rollback verification failed. Check service logs.'
    }

    throw 'Deployment failed. Previous version restored successfully.'
  }

  Write-Host 'Monitor deployed successfully.' -ForegroundColor Green
  Write-Host "Current version:  $remoteCurrent"
  Write-Host "Previous version: $remotePrevious"
}
finally {
  ssh $sshTarget "rm -rf $remoteStage"

  if (Test-Path $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
  }
}

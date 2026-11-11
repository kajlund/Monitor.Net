$ErrorActionPreference = 'Stop'
$sshTarget = 'luka@dreamquest'

function Assert-Success($step) {
    if ($LASTEXITCODE -ne 0) {
        throw "$step failed (exit code $LASTEXITCODE)."
    }
}

$release = [Guid]::NewGuid().ToString('N')
$publishDir = Join-Path ([System.IO.Path]::GetTempPath()) "monitor-$release"
$remoteStage = "~/apps/monitor-dotnet-stage-$release"

try {
    $project = Join-Path $PSScriptRoot 'Monitor.csproj'
    dotnet publish $project -c Release --self-contained false -o $publishDir
    Assert-Success 'Publish'

    ssh $sshTarget "mkdir -p $remoteStage"
    Assert-Success 'Create staging directory'

    scp -r "$publishDir/." "${sshTarget}:$remoteStage/"
    Assert-Success 'Upload'

    ssh $sshTarget "test -f $remoteStage/Monitor.dll && mv ~/apps/monitor-dotnet ~/apps/monitor-dotnet-previous-$release && (mv $remoteStage ~/apps/monitor-dotnet || { mv ~/apps/monitor-dotnet-previous-$release ~/apps/monitor-dotnet && false; }) && sudo -n /usr/bin/systemctl restart monitor-dotnet.service"
    Assert-Success 'Install and restart'

    $verified = $false

    for ($attempt = 1; $attempt -le 10; $attempt++) {
      $body = ssh $sshTarget 'curl -fs --max-time 20 http://127.0.0.1:50100/status'
      $exitCode = $LASTEXITCODE

      if ($exitCode -eq 0 -and ($body -join "`n") -match   '<h1>\s*Server status\s*</h1>') {
        $verified = $true
        break
      }

      if ($attempt -lt 10) {
          Start-Sleep -Seconds 1
      }
    }

    if (-not $verified) {
      throw 'Monitor did not return its status page on port 50100. Check journalctl -u monitor-dotnet.service on DreamQuest.'
    }

    Write-Host 'Monitor deployed successfully.'
    Write-Host "Previous version: ~/apps/monitor-dotnet-previous-$release"
}
finally {
    if (Test-Path $publishDir) {
        Remove-Item -LiteralPath $publishDir -Recurse -Force
    }
}
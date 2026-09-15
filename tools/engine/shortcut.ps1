param([Parameter(Mandatory)][string]$Editor, [Parameter(Mandatory)][string]$Project,
      [Parameter(Mandatory)][string]$Destination, [Parameter(Mandatory)][string]$Bash,
      [Parameter(Mandatory)][string]$Entry)
$ErrorActionPreference = 'Stop'
$shell = New-Object -ComObject WScript.Shell
$link = $shell.CreateShortcut($Destination)
$link.TargetPath = $Bash
$link.Arguments = '--noprofile --norc "' + $Entry + '" open'
$link.WorkingDirectory = $Project
$link.Description = 'Open Ciallo with its required Godot editor'
$link.WindowStyle = 7
$link.IconLocation = $Editor + ',0'
$link.Save()
[System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($link) | Out-Null
[System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) | Out-Null

$dll = "D:\SteamLibrary\steamapps\common\Valheim\valheim_Data\Managed\assembly_valheim.dll"
$bytes = [IO.File]::ReadAllBytes($dll)
$text = [Text.Encoding]::ASCII.GetString($bytes)
$patterns = @(
  'Forge of Potential','piece_upgrader','piece_potential','CraftingStation_upgrader',
  'm_upgrader','Upgrader','AttemptRefinement','Refine','potential','Potential',
  'item_upgrader','DoUpgrade','UpgradeItem'
)
foreach ($p in $patterns) {
    $c = ([regex]::Matches($text, [regex]::Escape($p))).Count
    Write-Host ("{0} = {1}" -f $p, $c)
}
Write-Host '--- piece_ ---'
[regex]::Matches($text, 'piece_[A-Za-z0-9_]*upgrad[A-Za-z0-9_]*') | ForEach-Object { $_.Value } | Sort-Object -Unique | ForEach-Object { Write-Host $_ }
Write-Host '--- potential names ---'
[regex]::Matches($text, '[A-Za-z0-9_]*[Pp]otential[A-Za-z0-9_]*') | ForEach-Object { $_.Value } | Sort-Object -Unique | ForEach-Object { Write-Host $_ }
Write-Host '--- upgrader piece ---'
[regex]::Matches($text, '[A-Za-z0-9_]*[Uu]pgrader[A-Za-z0-9_]*') | ForEach-Object { $_.Value } | Sort-Object -Unique | Select-Object -First 80 | ForEach-Object { Write-Host $_ }

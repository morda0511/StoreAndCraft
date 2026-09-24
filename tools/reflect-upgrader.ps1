$ErrorActionPreference = "Continue"
$managed = "D:\SteamLibrary\steamapps\common\Valheim\valheim_Data\Managed"
[Reflection.Assembly]::LoadFrom("$managed\UnityEngine.CoreModule.dll") | Out-Null
$asm = [Reflection.Assembly]::LoadFrom("$managed\assembly_valheim.dll")

$t = $null
try { $t = $asm.GetType("Upgrader") } catch { Write-Host $_.Exception.Message }
if (-not $t) {
    Write-Host "scanning types..."
    try {
        $asm.GetTypes() | Where-Object { $_.Name -match 'Upgrad|Potential|Idol' } | ForEach-Object { Write-Host $_.FullName }
    } catch {
        Write-Host "GetTypes failed:" $_.Exception.Message
        if ($_.Exception.InnerException) { Write-Host $_.Exception.InnerException.Message }
        # LoaderExceptions
        $le = $_.Exception.LoaderExceptions
        if ($le) { $le | Select-Object -First 5 | ForEach-Object { Write-Host $_.Message } }
    }
} else {
    Write-Host "==== Upgrader ===="
    $t.GetFields([Reflection.BindingFlags]'Instance,Public,NonPublic,Static') | ForEach-Object { Write-Host ("F {0} {1} pub={2}" -f $_.Name, $_.FieldType.Name, $_.IsPublic) }
    $t.GetMethods([Reflection.BindingFlags]'Instance,Public,NonPublic,Static,DeclaredOnly') | ForEach-Object { Write-Host ("M {0} pub={1}" -f $_.Name, $_.IsPublic) }
}

Write-Host ""
Write-Host "==== Piece+Requirement upgrader fields ===="
$r = $asm.GetType("Piece+Requirement")
if ($r) {
    $r.GetFields() | ForEach-Object { Write-Host ("F {0} {1}" -f $_.Name, $_.FieldType.Name) }
}

Write-Host ""
Write-Host "==== InventoryGui DoCrafting ===="
$g = $asm.GetType("InventoryGui")
$g.GetMethods([Reflection.BindingFlags]'Instance,Public,NonPublic,DeclaredOnly') | Where-Object { $_.Name -match 'Craft|Upgrade' } | ForEach-Object {
    $ps = ($_.GetParameters() | ForEach-Object { "{0} {1}" -f $_.ParameterType.Name, $_.Name }) -join ", "
    Write-Host ("{0}({1})" -f $_.Name, $ps)
}

Write-Host ""
Write-Host "==== Inventory.CountItems ===="
$inv = $asm.GetType("Inventory")
$inv.GetMethods() | Where-Object { $_.Name -eq 'CountItems' } | ForEach-Object {
    $ps = ($_.GetParameters() | ForEach-Object { "{0} {1}" -f $_.ParameterType.Name, $_.Name }) -join ", "
    Write-Host ("CountItems({0})" -f $ps)
}

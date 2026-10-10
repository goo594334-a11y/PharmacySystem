$asm = [System.Reflection.Assembly]::LoadFrom("D:\Project\system\Pharmacy System\bin\Debug\net10.0\PharmacySystem.dll")
$type = $asm.GetType("PharmacySystem.Resources.SharedResource")
Write-Host "Type Name: $($type.FullName)"
$rm = New-Object System.Resources.ResourceManager("PharmacySystem.Resources.SharedResource", $asm)
$ci = [System.Globalization.CultureInfo]::GetCultureInfo("ar")
Write-Host "Direct RM ar AppTitle: $($rm.GetString('AppTitle', $ci))"
$rmDouble = New-Object System.Resources.ResourceManager("PharmacySystem.Resources.Resources.SharedResource", $asm)
try {
    Write-Host "Double RM ar AppTitle: $($rmDouble.GetString('AppTitle', $ci))"
} catch {
    Write-Host "Double RM failed: $_"
}

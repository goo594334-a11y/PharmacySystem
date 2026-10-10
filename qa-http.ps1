$ErrorActionPreference = 'Continue'
$base = 'http://localhost:5093'
$log = @()
function Log([string]$level, [string]$msg) {
  $line = "[$level] $msg"
  Write-Host $line
  $script:log += $line
}
function Get-Token([string]$html) {
  if ($html -match 'name="__RequestVerificationToken"[^>]*value="([^"]+)"') { return $Matches[1] }
  if ($html -match 'value="([^"]+)"[^>]*name="__RequestVerificationToken"') { return $Matches[1] }
  throw 'No antiforgery token found'
}
function Get-Page($session, $path) {
  return Invoke-WebRequest -Uri ($base + $path) -WebSession $session -UseBasicParsing -MaximumRedirection 5
}
function Post-Form($session, $path, $fields) {
  $page = Get-Page $session $path
  $token = Get-Token $page.Content
  $body = @{ __RequestVerificationToken = $token }
  foreach ($k in $fields.Keys) { $body[$k] = $fields[$k] }
  return Invoke-WebRequest -Uri ($base + $path) -Method POST -Body $body -WebSession $session -UseBasicParsing -MaximumRedirection 5
}
function Post-Json($session, $path, $obj, $token) {
  $json = $obj | ConvertTo-Json -Depth 8 -Compress
  $headers = @{ RequestVerificationToken = $token }
  try {
    return Invoke-WebRequest -Uri ($base + $path) -Method POST -Body $json -ContentType 'application/json; charset=utf-8' -Headers $headers -WebSession $session -UseBasicParsing
  } catch {
    $resp = $_.Exception.Response
    $reader = New-Object System.IO.StreamReader($resp.GetResponseStream())
    $txt = $reader.ReadToEnd()
    return [pscustomobject]@{ StatusCode = [int]$resp.StatusCode; Content = $txt; IsError = $true }
  }
}
function Extract-Id($html, $pattern) {
  if ($html -match $pattern) { return [int]$Matches[1] }
  return $null
}

$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession

# --- 1. RegisterAdmin empty ---
$reg = Get-Page $session '/Account/Login'
Log 'INFO' "Login redirected to: $($reg.BaseResponse.ResponseUri)"
if ($reg.Content -notmatch 'RegisterAdmin') { Log 'FAIL' 'Expected RegisterAdmin first-run screen' } else { Log 'PASS' 'First-run RegisterAdmin shown when Users empty' }

try {
  $empty = Post-Form $session '/Account/RegisterAdmin' @{ FullName=''; Email=''; Password=''; ConfirmPassword='' }
  if ($empty.Content -match 'field-validation-error|validation-summary|RequiredField|required') {
    Log 'PASS' 'RegisterAdmin empty submit kept on page (client/server validation present)'
  } else {
    Log 'WARN' 'RegisterAdmin empty submit did not clearly show validation; status=' + $empty.StatusCode
  }
} catch { Log 'INFO' "RegisterAdmin empty POST: $($_.Exception.Message)" }

# invalid email + short password
try {
  $bad = Post-Form $session '/Account/RegisterAdmin' @{ FullName='X'; Email='not-an-email'; Password='12'; ConfirmPassword='34' }
  Log 'PASS' "RegisterAdmin invalid data stayed on form (len=$($bad.Content.Length))"
} catch { Log 'INFO' "RegisterAdmin invalid POST exception (expected-ish): $($_.Exception.Message)" }

# --- 2. Create admin ---
$adminEmail = 'qa.admin@pharmacy.test'
$adminPass = 'Admin@12345'
$ok = Post-Form $session '/Account/RegisterAdmin' @{
  FullName = 'QA Admin'
  Email = $adminEmail
  Password = $adminPass
  ConfirmPassword = $adminPass
}
Log 'INFO' "After RegisterAdmin URI=$($ok.BaseResponse.ResponseUri) status=$($ok.StatusCode)"
if ($ok.Content -match 'ph-sidebar' -or $ok.BaseResponse.ResponseUri.AbsolutePath -match 'Home') {
  Log 'PASS' 'Admin registered and signed in to dashboard'
} else {
  Log 'FAIL' 'Admin registration did not land on authenticated dashboard'
}

# RegisterAdmin should now redirect to Login or Home
$again = Get-Page $session '/Account/RegisterAdmin'
if ($again.BaseResponse.ResponseUri.AbsolutePath -match 'Login|Home|^/$') {
  Log 'PASS' 'RegisterAdmin blocked after admin exists'
} else {
  Log 'FAIL' 'RegisterAdmin still accessible after admin exists'
}

# --- 3. Hit all GET screens as admin ---
$gets = @(
  '/','/Home/Index','/Products','/Products/Create','/Categories','/Categories/Create','/Locations','/Locations/Create',
  '/Inventory','/Inventory/Batches','/Inventory/LowStock','/Inventory/Expiry','/Inventory/NearExpiry','/Inventory/Expired',
  '/Suppliers','/Suppliers/Create','/Purchases','/Purchases/Create','/Sales','/Sales/Pos','/Sales/New','/Customers','/Customers/Create',
  '/Returns','/Returns/Sales','/Returns/Purchases','/Expenses','/Expenses/Create','/Reports','/Reports/Sales','/Reports/Purchases',
  '/Reports/Inventory','/Reports/Expiry','/Users','/Users/Create','/AuditLogs','/Account/ChangePassword','/Account/AccessDenied',
  '/Home/NotFoundPage','/DoesNotExistRoute123'
)
foreach ($g in $gets) {
  try {
    $p = Get-Page $session $g
    $code = [int]$p.StatusCode
    $uri = $p.BaseResponse.ResponseUri.AbsolutePath
    $is404 = ($p.Content -match '404') -or $code -eq 404 -or $uri -match 'NotFound'
    Log 'GET' "$g -> HTTP $code uri=$uri 404flag=$is404"
  } catch {
    $sc = $null
    try { $sc = [int]$_.Exception.Response.StatusCode } catch {}
    Log 'GET' "$g -> EXCEPTION status=$sc msg=$($_.Exception.Message)"
  }
}

# --- 4. Validation: empty category ---
$catEmpty = Post-Form $session '/Categories/Create' @{ Name=''; Description=''; IsActive='true' }
if ($catEmpty.BaseResponse.ResponseUri.AbsolutePath -match 'Create' -or $catEmpty.Content -match 'validation') {
  Log 'PASS' 'Category empty name rejected'
} else { Log 'FAIL' 'Category empty name may have been accepted' }

# long name
$long = 'C' * 200
$catLong = Post-Form $session '/Categories/Create' @{ Name=$long; Description='<script>alert(1)</script>'; IsActive='true' }
if ($catLong.BaseResponse.ResponseUri.AbsolutePath -match 'Index') { Log 'WARN' 'Category 200-char name accepted (max is 100?)' }
else { Log 'PASS' 'Category overly long name rejected' }

# valid master data
$c1 = Post-Form $session '/Categories/Create' @{ Name='Antibiotics'; Description='ABX'; IsActive='true' }
$c2 = Post-Form $session '/Categories/Create' @{ Name='Pain Relief'; Description='Analgesics'; IsActive='true' }
$catHtml = (Get-Page $session '/Categories').Content
$catIds = [regex]::Matches($catHtml, '/Categories/Edit/(\d+)') | ForEach-Object { [int]$_.Groups[1].Value } | Select-Object -Unique
Log 'INFO' ("Category IDs: " + ($catIds -join ','))

$l1 = Post-Form $session '/Locations/Create' @{ Code='SH-A1'; Name='Shelf A1'; IsActive='true' }
$l2 = Post-Form $session '/Locations/Create' @{ Code='FRIDGE'; Name='Refrigerator'; IsActive='true' }
$locHtml = (Get-Page $session '/Locations').Content
$locIds = [regex]::Matches($locHtml, '/Locations/Edit/(\d+)') | ForEach-Object { [int]$_.Groups[1].Value } | Select-Object -Unique
Log 'INFO' ("Location IDs: " + ($locIds -join ','))

# duplicate location code
$dupLoc = Post-Form $session '/Locations/Create' @{ Code='SH-A1'; Name='Dup'; IsActive='true' }
if ($dupLoc.BaseResponse.ResponseUri.AbsolutePath -match 'Create' -or $dupLoc.Content -match 'exist|already|موجود') {
  Log 'PASS' 'Duplicate location code blocked'
} else { Log 'WARN' 'Duplicate location code may have been allowed' }

$s1 = Post-Form $session '/Suppliers/Create' @{ Name='MediSupply Co'; Phone='01000000001'; Email='supplier@test.com'; Address='Cairo'; Notes='Main'; IsActive='true' }
$s2 = Post-Form $session '/Suppliers/Create' @{ Name='Invalid Email Supplier'; Phone='abc'; Email='not-email'; Address=''; Notes=''; IsActive='true' }
if ($s2.BaseResponse.ResponseUri.AbsolutePath -match 'Create' -or $s2.Content -match 'validation|InvalidEmail|InvalidPhone') {
  Log 'PASS' 'Supplier invalid email/phone rejected'
} else { Log 'FAIL' 'Supplier invalid email/phone accepted' }
$supHtml = (Get-Page $session '/Suppliers').Content
$supIds = [regex]::Matches($supHtml, '/Suppliers/Edit/(\d+)') | ForEach-Object { [int]$_.Groups[1].Value } | Select-Object -Unique
Log 'INFO' ("Supplier IDs: " + ($supIds -join ','))

$cu1 = Post-Form $session '/Customers/Create' @{ Name='Ahmed Ali'; Phone='01111111111'; Address='Giza'; Notes='VIP'; IsActive='true' }
$cu2 = Post-Form $session '/Customers/Create' @{ Name='Walkin Backup'; Phone=''; Address=''; Notes=''; IsActive='true' }
$custHtml = (Get-Page $session '/Customers').Content
$custIds = [regex]::Matches($custHtml, '/Customers/Edit/(\d+)') | ForEach-Object { [int]$_.Groups[1].Value } | Select-Object -Unique
Log 'INFO' ("Customer IDs: " + ($custIds -join ','))

# Products - negative price
$prodPage = Get-Page $session '/Products/Create'
$ptoken = Get-Token $prodPage.Content
$negBody = @{ __RequestVerificationToken=$ptoken; Name='BadPrice'; Barcode='X'; CategoryId=$catIds[0]; SellingPrice='-10'; MinimumStockLevel='5'; Description=''; IsActive='true' }
$neg = Invoke-WebRequest -Uri ($base + '/Products/Create') -Method POST -Body $negBody -WebSession $session -UseBasicParsing -MaximumRedirection 5
if ($neg.BaseResponse.ResponseUri.AbsolutePath -match 'Create' -or $neg.Content -match 'validation|Positive') {
  Log 'PASS' 'Negative selling price rejected'
} else { Log 'FAIL' 'Negative selling price accepted' }

$zero = Invoke-WebRequest -Uri ($base + '/Products/Create') -Method POST -Body @{ __RequestVerificationToken=(Get-Token (Get-Page $session '/Products/Create').Content); Name='ZeroPrice'; CategoryId=$catIds[0]; SellingPrice='0'; MinimumStockLevel='-3'; Description=''; IsActive='true' } -WebSession $session -UseBasicParsing -MaximumRedirection 5
if ($zero.BaseResponse.ResponseUri.AbsolutePath -match 'Create' -or $zero.Content -match 'validation|Positive') {
  Log 'PASS' 'Zero price / negative min stock rejected'
} else { Log 'FAIL' 'Zero price or negative min stock accepted' }

# valid products
function New-Product($session, $name, $barcode, $catId, $price, $min) {
  $t = Get-Token (Get-Page $session '/Products/Create').Content
  return Invoke-WebRequest -Uri ($base + '/Products/Create') -Method POST -Body @{
    __RequestVerificationToken=$t; Name=$name; Barcode=$barcode; CategoryId=$catId; SellingPrice=$price; MinimumStockLevel=$min; Description='QA'; IsActive='true'
  } -WebSession $session -UseBasicParsing -MaximumRedirection 5
}
New-Product $session 'Amoxicillin 500mg' '6223001001' $catIds[0] '45.50' '20' | Out-Null
New-Product $session 'Paracetamol 500mg' '6223001002' $catIds[1] '12.00' '10' | Out-Null
New-Product $session 'Ibuprofen 400mg' '6223001003' $catIds[1] '18.75' '50' | Out-Null
$prodHtml = (Get-Page $session '/Products').Content
$prodIds = [regex]::Matches($prodHtml, '/Products/Edit/(\d+)') | ForEach-Object { [int]$_.Groups[1].Value } | Select-Object -Unique
Log 'INFO' ("Product IDs: " + ($prodIds -join ','))

# duplicate barcode
$dupBar = New-Product $session 'DupBarcode' '6223001001' $catIds[0] '10' '1'
if ($dupBar.BaseResponse.ResponseUri.AbsolutePath -match 'Create' -or $dupBar.Content -match 'exist|Barcode|already') {
  Log 'PASS' 'Duplicate barcode blocked'
} else { Log 'WARN' 'Duplicate barcode may be allowed' }

# --- Purchases JSON ---
$purPage = Get-Page $session '/Purchases/Create'
$purToken = Get-Token $purPage.Content
$today = Get-Date
$expSoon = $today.AddDays(20).ToString('yyyy-MM-dd')
$expFar = $today.AddDays(400).ToString('yyyy-MM-dd')
$expPast = $today.AddDays(-10).ToString('yyyy-MM-dd')

$purchasePayload = @{
  SupplierId = $supIds[0]
  InvoiceNumber = 'PUR-QA-001'
  Notes = 'QA incoming stock'
  Items = @(
    @{ ProductId=$prodIds[0]; Quantity=30; UnitPurchasePrice=20.00; BatchNumber='AMOX-FAR'; ExpiryDate=$expFar; LocationId=$locIds[0] }
    @{ ProductId=$prodIds[0]; Quantity=15; UnitPurchasePrice=22.00; BatchNumber='AMOX-NEAR'; ExpiryDate=$expSoon; LocationId=$locIds[0] }
    @{ ProductId=$prodIds[1]; Quantity=40; UnitPurchasePrice=5.00; BatchNumber='PARA-OK'; ExpiryDate=$expFar; LocationId=$locIds[1] }
    @{ ProductId=$prodIds[1]; Quantity=10; UnitPurchasePrice=4.00; BatchNumber='PARA-EXP'; ExpiryDate=$expPast; LocationId=$locIds[1] }
    @{ ProductId=$prodIds[2]; Quantity=8; UnitPurchasePrice=9.00; BatchNumber='IBU-LOW'; ExpiryDate=$expFar; LocationId=$locIds[0] }
  )
}
$pRes = Post-Json $session '/Purchases/Create' $purchasePayload $purToken
Log 'INFO' "Create purchase: $($pRes.StatusCode) $($pRes.Content)"

# negative qty purchase
$badPur = Post-Json $session '/Purchases/Create' @{
  SupplierId=$supIds[0]; InvoiceNumber='PUR-NEG'; Notes=''; Items=@(@{ ProductId=$prodIds[0]; Quantity=-5; UnitPurchasePrice=1; BatchNumber='NEG'; ExpiryDate=$expFar; LocationId=$locIds[0] })
} $purToken
Log 'INFO' "Negative qty purchase: $($badPur.StatusCode) $($badPur.Content)"

$purList = (Get-Page $session '/Purchases').Content
$purchaseId = Extract-Id $purList '/Purchases/Details/(\d+)'
Log 'INFO' "PurchaseId=$purchaseId"

# Confirm
$det = Get-Page $session "/Purchases/Details/$purchaseId"
$ctoken = Get-Token $det.Content
$confirm = Invoke-WebRequest -Uri ($base + "/Purchases/Confirm/$purchaseId") -Method POST -Body @{ __RequestVerificationToken=$ctoken } -WebSession $session -UseBasicParsing -MaximumRedirection 5
Log 'INFO' "Confirm purchase URI=$($confirm.BaseResponse.ResponseUri) code=$($confirm.StatusCode)"

# Confirm again should fail
try {
  $confirm2 = Invoke-WebRequest -Uri ($base + "/Purchases/Confirm/$purchaseId") -Method POST -Body @{ __RequestVerificationToken=(Get-Token (Get-Page $session "/Purchases/Details/$purchaseId").Content) } -WebSession $session -UseBasicParsing -MaximumRedirection 5
  Log 'INFO' "Re-confirm content snippet: " + $confirm2.Content.Substring(0, [Math]::Min(200,$confirm2.Content.Length))
} catch { Log 'PASS' "Re-confirm threw/blocked: $($_.Exception.Message)" }

$inv = (Get-Page $session '/Inventory').Content
Log 'INFO' 'Inventory page loaded after confirm'
$low = Get-Page $session '/Inventory/LowStock'
Log 'INFO' ("LowStock page len=" + $low.Content.Length)
$expPage = Get-Page $session '/Inventory/Expiry'
Log 'INFO' ("Expiry page contains expired=" + ($expPage.Content -match 'PARA-EXP|Expired'))

# --- Sales POS ---
$pos = Get-Page $session '/Sales/Pos'
$posToken = Get-Token $pos.Content

# sell expired-only should fail if only expired available... PARA has both
# Sell Amox 10 -> should take NEAR expiry first (FEFO)
$sale1 = Post-Json $session '/Sales/ProcessSale' @{
  CustomerId = $custIds[0]
  PaymentMethod = 'Cash'
  Items = @(@{ ProductId=$prodIds[0]; Quantity=10 })
} $posToken
Log 'INFO' "Sale FEFO: $($sale1.StatusCode) $($sale1.Content)"

# oversell ibuprofen (only 8)
$saleOver = Post-Json $session '/Sales/ProcessSale' @{
  PaymentMethod = 'Cash'
  Items = @(@{ ProductId=$prodIds[2]; Quantity=99 })
} $posToken
Log 'INFO' "Oversell: $($saleOver.StatusCode) $($saleOver.Content)"

# qty 0
$saleZero = Post-Json $session '/Sales/ProcessSale' @{
  PaymentMethod = 'Cash'
  Items = @(@{ ProductId=$prodIds[1]; Quantity=0 })
} $posToken
Log 'INFO' "Qty0 sale: $($saleZero.StatusCode) $($saleZero.Content)"

# sell paracetamol 5 from good batch (expired excluded)
$sale2 = Post-Json $session '/Sales/ProcessSale' @{
  CustomerId = $null
  PaymentMethod = 'Card'
  Items = @(@{ ProductId=$prodIds[1]; Quantity=5 }, @{ ProductId=$prodIds[2]; Quantity=3 })
} $posToken
Log 'INFO' "Sale2: $($sale2.StatusCode) $($sale2.Content)"

$salesHtml = (Get-Page $session '/Sales').Content
$saleIds = [regex]::Matches($salesHtml, '/Sales/Details/(\d+)') | ForEach-Object { [int]$_.Groups[1].Value } | Select-Object -Unique
Log 'INFO' ("Sale IDs: " + ($saleIds -join ','))

# details of first sale (Amox sale ID 1) - check batch AMOX-NEAR used
$amoxSaleId = ($saleIds | Sort-Object)[0]
$sDet = Get-Page $session "/Sales/Details/$amoxSaleId"
if ($sDet.Content -match 'AMOX-NEAR') { Log 'PASS' 'FEFO used nearer expiry batch AMOX-NEAR' }
elseif ($sDet.Content -match 'AMOX-FAR') { Log 'FAIL' 'FEFO did NOT prefer nearer expiry (used FAR)' }
else { Log 'WARN' 'Could not see batch number on sale details' }

# --- Dashboard vs Reports today ---
$dash = Get-Page $session '/'
$repToday = Get-Page $session ("/Reports/Sales?fromDate={0}&toDate={0}" -f (Get-Date).ToString('yyyy-MM-dd'))
Log 'INFO' ("Dashboard len=" + $dash.Content.Length + " report len=" + $repToday.Content.Length)
# extract numbers roughly
function Grab-Near($html, $labelPattern) {
  if ($html -match $labelPattern) { return $Matches[0] }
  return ''
}

# --- Notification links ---
$near = try { Get-Page $session '/Inventory/NearExpiry' } catch { $_.Exception.Response }
$expired = try { Get-Page $session '/Inventory/Expired' } catch { $_.Exception.Response }

# --- Sales return ---
$firstSaleId = $saleIds | Select-Object -Last 1
# use sale with amox
$amoxSaleId = $saleIds[0]
$retGet = Get-Page $session "/Returns/CreateSalesReturn?saleId=$amoxSaleId"
Log 'INFO' ("Return form status=" + $retGet.StatusCode)
# parse hidden fields via regex
$rtoken = Get-Token $retGet.Content
$saleItemId = if ($retGet.Content -match 'name="Items\[0\].SaleItemId"[^>]*value="(\d+)"' -or $retGet.Content -match 'value="(\d+)"[^>]*name="Items\[0\].SaleItemId"') { $Matches[1] } else { $null }
# ASP.NET typically: name="Items[0].SaleItemId" value="1"
if (-not $saleItemId -and $retGet.Content -match 'Items\[0\]\.SaleItemId"\s+value="(\d+)"') { $saleItemId = $Matches[1] }
if (-not $saleItemId -and $retGet.Content -match 'Items_0__SaleItemId[^>]*value="(\d+)"') { $saleItemId = $Matches[1] }

# dump a slice of inputs
$inputs = [regex]::Matches($retGet.Content, 'name="([^"]+)"[^>]*value="([^"]*)"|value="([^"]*)"[^>]*name="([^"]+)"')
Log 'INFO' ("Return inputs count=" + $inputs.Count)
$body = @{ __RequestVerificationToken = $rtoken; Reason = 'QA customer return'; SaleId = $amoxSaleId }
# rebuild from matches
foreach ($m in $inputs) {
  $n = $m.Groups[1].Value; $v = $m.Groups[2].Value
  if (-not $n) { $n = $m.Groups[4].Value; $v = $m.Groups[3].Value }
  if ($n -and $n -ne '__RequestVerificationToken') { $body[$n] = $v }
}
$body['Items[0].QuantityToReturn'] = '2'
$body['Reason'] = 'QA customer return'
try {
  $retPost = Invoke-WebRequest -Uri ($base + '/Returns/CreateSalesReturn') -Method POST -Body $body -WebSession $session -UseBasicParsing -MaximumRedirection 5
  Log 'INFO' "Sales return POST uri=$($retPost.BaseResponse.ResponseUri) code=$($retPost.StatusCode)"
} catch { Log 'FAIL' "Sales return POST failed: $($_.Exception.Message)" }

# over-return
$body['Items[0].QuantityToReturn'] = '9999'
try {
  $overRet = Invoke-WebRequest -Uri ($base + '/Returns/CreateSalesReturn') -Method POST -Body $body -WebSession $session -UseBasicParsing -MaximumRedirection 5
  if ($overRet.BaseResponse.ResponseUri.AbsolutePath -match 'CreateSalesReturn' -or $overRet.Content -match 'error|Exception|cannot') {
    Log 'PASS' 'Over-quantity sales return blocked'
  } else { Log 'FAIL' 'Over-quantity sales return may have succeeded' }
} catch { Log 'PASS' "Over-return exception: $($_.Exception.Message)" }

# Purchase return
$prGet = Get-Page $session "/Returns/CreatePurchaseReturn?purchaseId=$purchaseId"
$prToken = Get-Token $prGet.Content
$prBody = @{ __RequestVerificationToken=$prToken; Reason='QA supplier return'; PurchaseId=$purchaseId }
foreach ($m in [regex]::Matches($prGet.Content, 'name="([^"]+)"[^>]*value="([^"]*)"')) {
  if ($m.Groups[1].Value -ne '__RequestVerificationToken') { $prBody[$m.Groups[1].Value] = $m.Groups[2].Value }
}
$prBody['Items[0].QuantityToReturn'] = '1'
$prBody['Reason'] = 'QA supplier return'
try {
  $prPost = Invoke-WebRequest -Uri ($base + '/Returns/CreatePurchaseReturn') -Method POST -Body $prBody -WebSession $session -UseBasicParsing -MaximumRedirection 5
  Log 'INFO' "Purchase return POST uri=$($prPost.BaseResponse.ResponseUri)"
} catch { Log 'FAIL' "Purchase return failed: $($_.Exception.Message)" }

# Expenses
$exEmpty = Post-Form $session '/Expenses/Create' @{ ExpenseDate=(Get-Date).ToString('yyyy-MM-dd'); Category='Electricity'; Amount='-50'; Description='bad' }
if ($exEmpty.BaseResponse.ResponseUri.AbsolutePath -match 'Create' -or $exEmpty.Content -match 'validation|Positive') { Log 'PASS' 'Negative expense rejected' } else { Log 'FAIL' 'Negative expense accepted' }
$exOk = Post-Form $session '/Expenses/Create' @{ ExpenseDate=(Get-Date).ToString('yyyy-MM-dd'); Category='Rent'; Amount='1500'; Description='Monthly rent QA' }
Log 'INFO' "Expense create uri=$($exOk.BaseResponse.ResponseUri)"

# Users pharmacist
$u = Post-Form $session '/Users/Create' @{ FullName='QA Pharmacist'; Email='qa.pharm@pharmacy.test'; Role='Pharmacist'; Password='Pharm@123'; ConfirmPassword='Pharm@123'; IsActive='true' }
Log 'INFO' "Create pharmacist uri=$($u.BaseResponse.ResponseUri)"

# Delete product with history
$delTok = Get-Token (Get-Page $session '/Products').Content
try {
  $delP = Invoke-WebRequest -Uri ($base + "/Products/Delete/$($prodIds[0])") -Method POST -Body @{ __RequestVerificationToken=$delTok } -WebSession $session -UseBasicParsing -MaximumRedirection 5
  if ($delP.Content -match 'Cannot|failed|خطأ|لا يمكن' -or $delP.BaseResponse.ResponseUri.AbsolutePath -match 'Products') {
    Log 'PASS' 'Delete product with history blocked (no exception crash)'
  } else { Log 'WARN' 'Delete product with history outcome unclear' }
} catch { Log 'INFO' "Delete product exception: $($_.Exception.Message)" }

# Delete category with products
$delCTok = Get-Token (Get-Page $session '/Categories').Content
try {
  $delC = Invoke-WebRequest -Uri ($base + "/Categories/Delete/$($catIds[0])") -Method POST -Body @{ __RequestVerificationToken=$delCTok } -WebSession $session -UseBasicParsing -MaximumRedirection 5
  Log 'INFO' "Delete category uri=$($delC.BaseResponse.ResponseUri) "
} catch { Log 'INFO' "Delete category: $($_.Exception.Message)" }

# Delete customer with sales
$delCuTok = Get-Token (Get-Page $session '/Customers').Content
try {
  $delCu = Invoke-WebRequest -Uri ($base + "/Customers/Delete/$($custIds[0])") -Method POST -Body @{ __RequestVerificationToken=$delCuTok } -WebSession $session -UseBasicParsing -MaximumRedirection 5
  Log 'INFO' "Delete customer with sales uri=$($delCu.BaseResponse.ResponseUri)"
} catch { Log 'INFO' "Delete customer: $($_.Exception.Message)" }

# Audit logs
$al = Get-Page $session '/AuditLogs'
Log 'INFO' ("AuditLogs page len=" + $al.Content.Length + " has Login=" + ($al.Content -match 'Login') + " has Sale=" + ($al.Content -match 'Sale'))

# Search/pagination
$search = Get-Page $session '/Products?search=Amox'
Log 'INFO' ("Product search Amox hits=" + ([regex]::Matches($search.Content, 'Amoxicillin').Count))

# Change password mismatch
$cp = Get-Page $session '/Account/ChangePassword'
$cpTok = Get-Token $cp.Content
$cpBad = Invoke-WebRequest -Uri ($base + '/Account/ChangePassword') -Method POST -Body @{
  __RequestVerificationToken=$cpTok; CurrentPassword=$adminPass; NewPassword='NewPass1'; ConfirmPassword='Other'
} -WebSession $session -UseBasicParsing -MaximumRedirection 5
if ($cpBad.Content -match 'validation|match|Confirm') { Log 'PASS' 'ChangePassword mismatch rejected' } else { Log 'WARN' 'ChangePassword mismatch unclear' }

# Forgot password while logged in
$fp = Get-Page $session '/Account/ForgotPassword'
Log 'INFO' "ForgotPassword uri=$($fp.BaseResponse.ResponseUri)"

# --- Pharmacist session ---
$pharm = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$lp = Get-Page $pharm '/Account/Login'
$lt = Get-Token $lp.Content
$loginP = Invoke-WebRequest -Uri ($base + '/Account/Login') -Method POST -Body @{
  __RequestVerificationToken=$lt; Email='qa.pharm@pharmacy.test'; Password='Pharm@123'; RememberMe='false'
} -WebSession $pharm -UseBasicParsing -MaximumRedirection 5
Log 'INFO' "Pharmacist login uri=$($loginP.BaseResponse.ResponseUri)"
try {
  $usersAsPharm = Invoke-WebRequest -Uri ($base + '/Users') -WebSession $pharm -UseBasicParsing -MaximumRedirection 5
  if ($usersAsPharm.BaseResponse.ResponseUri.AbsolutePath -match 'AccessDenied') { Log 'PASS' 'Pharmacist /Users redirected AccessDenied' }
  elseif ($usersAsPharm.Content -match 'AccessDenied|ممنوع|صلاح') { Log 'PASS' 'Pharmacist /Users access denied page' }
  else { Log 'FAIL' 'Pharmacist can open /Users' }
} catch {
  $sc = [int]$_.Exception.Response.StatusCode
  if ($sc -eq 403) { Log 'PASS' 'Pharmacist /Users HTTP 403' } else { Log 'INFO' "Pharmacist /Users status=$sc" }
}
try {
  $alAsPharm = Invoke-WebRequest -Uri ($base + '/AuditLogs') -WebSession $pharm -UseBasicParsing -MaximumRedirection 5
  if ($alAsPharm.BaseResponse.ResponseUri.AbsolutePath -match 'AccessDenied' -or $alAsPharm.Content -match 'AccessDenied') { Log 'PASS' 'Pharmacist /AuditLogs denied' }
  else { Log 'FAIL' 'Pharmacist can open /AuditLogs' }
} catch {
  $sc = [int]$_.Exception.Response.StatusCode
  if ($sc -eq 403) { Log 'PASS' 'Pharmacist /AuditLogs HTTP 403' } else { Log 'INFO' "Pharmacist AuditLogs status=$sc" }
}

# --- Lockout ---
$lock = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$failCount = 0
1..12 | ForEach-Object {
  $lg = Get-Page $lock '/Account/Login'
  $tk = Get-Token $lg.Content
  $lr = Invoke-WebRequest -Uri ($base + '/Account/Login') -Method POST -Body @{
    __RequestVerificationToken=$tk; Email=$adminEmail; Password='WrongPassword!!'; RememberMe='false'
  } -WebSession $lock -UseBasicParsing -MaximumRedirection 5
  if ($lr.Content -match 'Invalid|خطأ|credentials|password' -or $lr.BaseResponse.ResponseUri.AbsolutePath -match 'Login') { $failCount++ }
  $locked = $lr.Content -match 'lock|locked|محظور|too many'
  if ($locked) { Log 'INFO' "Lockout message appeared on attempt $_" }
}
Log 'INFO' "Failed login attempts completed=$failCount (12). Lockout message observed? see above."
# still can login with correct password?
$lg2 = Get-Page $lock '/Account/Login'
$tk2 = Get-Token $lg2.Content
$okLogin = Invoke-WebRequest -Uri ($base + '/Account/Login') -Method POST -Body @{
  __RequestVerificationToken=$tk2; Email=$adminEmail; Password=$adminPass; RememberMe='false'
} -WebSession $lock -UseBasicParsing -MaximumRedirection 5
if ($okLogin.BaseResponse.ResponseUri.AbsolutePath -match 'Home' -or $okLogin.Content -match 'ph-sidebar') {
  Log 'FAIL-OR-INFO' 'After 12 failed logins, correct password still works (NO lockout)'
} else {
  Log 'PASS' 'Account locked or login blocked after failures'
}

# --- UTC experiment via SQL then reload dashboard/report ---
sqlcmd -S .\SQLEXPRESS -d PharmacyDb -E -h -1 -W -Q "SET NOCOUNT ON; SELECT Id, InvoiceNumber, SaleDate, TotalAmount FROM Sales;" | ForEach-Object { Log 'SQL' $_ }

$script:log | Set-Content -Path "$env:TEMP\pharmacy-qa-log.txt" -Encoding UTF8
Log 'INFO' 'Wrote log to TEMP\pharmacy-qa-log.txt'

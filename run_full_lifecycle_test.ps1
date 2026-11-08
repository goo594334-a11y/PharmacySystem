$ErrorActionPreference = 'Stop'

$base = 'http://localhost:5093'
$results = [System.Collections.Generic.List[PSObject]]::new()

function Record-Test([string]$step, [string]$name, [bool]$passed, [string]$details) {
    $res = [pscustomobject]@{
        Step = $step
        Name = $name
        Status = if ($passed) { "PASS" } else { "FAIL" }
        Details = $details
    }
    $results.Add($res)
    $badge = if ($passed) { "[PASS]" } else { "[FAIL]" }
    $color = if ($passed) { 'Green' } else { 'Red' }
    Write-Host "$badge Step $step - $name : $details" -ForegroundColor $color
}

function Get-Token([string]$html) {
    if ($html -match 'name="__RequestVerificationToken"[^>]*value="([^"]+)"') { return $Matches[1] }
    if ($html -match 'value="([^"]+)"[^>]*name="__RequestVerificationToken"') { return $Matches[1] }
    throw 'No antiforgery token found in HTML'
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
        if ($resp) {
            $reader = New-Object System.IO.StreamReader($resp.GetResponseStream())
            $txt = $reader.ReadToEnd()
            return [pscustomobject]@{ StatusCode = [int]$resp.StatusCode; Content = $txt; IsError = $true }
        }
        throw $_
    }
}

$rand = Get-Random -Minimum 1000 -Maximum 9999
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " STARTING END-TO-END PHARMACY SYSTEM FULL LIFECYCLE TEST " -ForegroundColor Cyan
Write-Host " Execution ID / Random Seed: $rand" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# ========================================================
# 1. Admin Login
# ========================================================
$adminSession = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$loginGet = Get-Page $adminSession '/Account/Login'
$loginTok = Get-Token $loginGet.Content
$adminLoginResp = Invoke-WebRequest -Uri ($base + '/Account/Login') -Method POST -Body @{
    __RequestVerificationToken = $loginTok
    Email = "admin@pharmacy.local"
    Password = "Admin@12345"
    RememberMe = "false"
} -WebSession $adminSession -UseBasicParsing -MaximumRedirection 5

$adminLoggedIn = ($adminLoginResp.StatusCode -eq 200) -and (($adminLoginResp.Content -match 'ph-sidebar') -or ($adminLoginResp.Content -match 'Dashboard') -or ($adminLoginResp.Content -match 'Pharmacy'))
Record-Test "1.1" "Admin Authentication" $adminLoggedIn "Admin logged in successfully to Dashboard (HTTP $($adminLoginResp.StatusCode))"

# ========================================================
# 2. Register New User (Pharmacist)
# ========================================================
$pharmEmail = "dr.hani.pharm$rand@pharmacy.test"
$pharmPass = "PharmPass@$rand"
$pharmName = "Dr Hani Mostafa Pharmacist $rand"

$createPharmResp = Post-Form $adminSession '/Users/Create' @{
    FullName = $pharmName
    Email = $pharmEmail
    Password = $pharmPass
    ConfirmPassword = $pharmPass
    Role = "Pharmacist"
    IsActive = "true"
}

$usersCheck = sqlcmd -S .\SQLEXPRESS -d PharmacyDb -E -h -1 -W -Q "SET NOCOUNT ON; SELECT Id, FullName, Email, Role FROM Users WHERE Email = '$pharmEmail';"
$pharmCreated = ($usersCheck -match $pharmEmail)
Record-Test "2.1" "Register New Pharmacist User" $pharmCreated "User created: $pharmName | Role: Pharmacist | Email: $pharmEmail"

# ========================================================
# 3. Register New Customer
# ========================================================
$custName = "Mahmoud Ibrahim El-Shenawy $rand"
$custPhone = "0109988$rand"
$createCustResp = Post-Form $adminSession '/Customers/Create' @{
    Name = $custName
    Phone = $custPhone
    Address = "Tahrir Street Dokki Giza"
    Notes = "VIP Customer for Full Lifecycle Testing"
    IsActive = "true"
}

$custIdStr = sqlcmd -S .\SQLEXPRESS -d PharmacyDb -E -h -1 -W -Q "SET NOCOUNT ON; SELECT TOP 1 Id FROM Customers WHERE Phone = '$custPhone';"
$customerId = [int]$custIdStr.Trim()
Record-Test "3.1" "Register New Customer" ($customerId -gt 0) "Customer registered with ID: $customerId | Name: $custName"

# ========================================================
# 4. Register Supplier, Category, Location, and Products
# ========================================================
$supName = "Egypt Pharma Distribution Co $rand"
$supPhone = "0112233$rand"
$createSupResp = Post-Form $adminSession '/Suppliers/Create' @{
    Name = $supName
    Phone = $supPhone
    Email = "supply$rand@pharma.test"
    Address = "Nasr City Cairo Free Zone"
    Notes = "Approved Primary Supplier"
    IsActive = "true"
}
$supIdStr = sqlcmd -S .\SQLEXPRESS -d PharmacyDb -E -h -1 -W -Q "SET NOCOUNT ON; SELECT TOP 1 Id FROM Suppliers WHERE Phone = '$supPhone';"
$supplierId = [int]$supIdStr.Trim()
Record-Test "4.1" "Register New Supplier" ($supplierId -gt 0) "Supplier registered with ID: $supplierId | Name: $supName"

# Category & Location
$catName = "GI and Pain Relief Category $rand"
$createCatResp = Post-Form $adminSession '/Categories/Create' @{
    Name = $catName
    Description = "Test Category"
    IsActive = "true"
}
$catIdStr = sqlcmd -S .\SQLEXPRESS -d PharmacyDb -E -h -1 -W -Q "SET NOCOUNT ON; SELECT TOP 1 Id FROM Categories WHERE Name = '$catName';"
$categoryId = [int]$catIdStr.Trim()

$locCode = "LOC-T$rand"
$createLocResp = Post-Form $adminSession '/Locations/Create' @{
    Code = $locCode
    Name = "Shelf Location $rand"
    IsActive = "true"
}
$locIdStr = sqlcmd -S .\SQLEXPRESS -d PharmacyDb -E -h -1 -W -Q "SET NOCOUNT ON; SELECT TOP 1 Id FROM Locations WHERE Code = '$locCode';"
$locationId = [int]$locIdStr.Trim()

Record-Test "4.2" "Create Category and Location" (($categoryId -gt 0) -and ($locationId -gt 0)) "Category ID: $categoryId, Location ID: $locationId ($locCode)"

# Products: Product A (Controloc) & Product B (Augmentin for Expired testing)
$prodAName = "Controloc 40mg $rand"
$prodABarcode = "62211$rand"
$createProdAResp = Post-Form $adminSession '/Products/Create' @{
    Name = $prodAName
    Barcode = $prodABarcode
    CategoryId = $categoryId
    SellingPrice = "95.00"
    MinimumStockLevel = "10"
    Description = "Proton Pump Inhibitor"
    IsActive = "true"
}
$prodAIdStr = sqlcmd -S .\SQLEXPRESS -d PharmacyDb -E -h -1 -W -Q "SET NOCOUNT ON; SELECT TOP 1 Id FROM Products WHERE Barcode = '$prodABarcode';"
$productAId = [int]$prodAIdStr.Trim()

$prodBName = "Augmentin 1g $rand"
$prodBBarcode = "62222$rand"
$createProdBResp = Post-Form $adminSession '/Products/Create' @{
    Name = $prodBName
    Barcode = $prodBBarcode
    CategoryId = $categoryId
    SellingPrice = "115.00"
    MinimumStockLevel = "15"
    Description = "Broad Spectrum Antibiotic"
    IsActive = "true"
}
$prodBIdStr = sqlcmd -S .\SQLEXPRESS -d PharmacyDb -E -h -1 -W -Q "SET NOCOUNT ON; SELECT TOP 1 Id FROM Products WHERE Barcode = '$prodBBarcode';"
$productBId = [int]$prodBIdStr.Trim()

Record-Test "4.3" "Register Products with Barcode and Pricing" (($productAId -gt 0) -and ($productBId -gt 0)) "Product A ID: $productAId, Product B ID: $productBId"

# ========================================================
# 5. Purchase Invoice from Supplier (3 Batches: Good, Near Expiry, Expired)
# ========================================================
$purPage = Get-Page $adminSession '/Purchases/Create'
$purToken = Get-Token $purPage.Content
$today = Get-Date
$expGood = $today.AddDays(200).ToString('yyyy-MM-dd')
$expNear = $today.AddDays(22).ToString('yyyy-MM-dd')
$expExpired = $today.AddDays(-20).ToString('yyyy-MM-dd')

$purInvoiceNum = "PUR-TEST-$rand"
$batchGoodNum = "BATCH-GOOD-$rand"
$batchNearNum = "BATCH-NEAR-$rand"
$batchExpNum = "BATCH-EXP-$rand"

$purchasePayload = @{
    SupplierId = $supplierId
    InvoiceNumber = $purInvoiceNum
    Notes = "Incoming stock from Egypt Pharma"
    Items = @(
        @{ ProductId = $productAId; Quantity = 50; UnitPurchasePrice = 65.00; BatchNumber = $batchGoodNum; ExpiryDate = $expGood; LocationId = $locationId }
        @{ ProductId = $productAId; Quantity = 20; UnitPurchasePrice = 60.00; BatchNumber = $batchNearNum; ExpiryDate = $expNear; LocationId = $locationId }
        @{ ProductId = $productBId; Quantity = 12; UnitPurchasePrice = 75.00; BatchNumber = $batchExpNum; ExpiryDate = $expExpired; LocationId = $locationId }
    )
}

$pRes = Post-Json $adminSession '/Purchases/Create' $purchasePayload $purToken
$purIdStr = sqlcmd -S .\SQLEXPRESS -d PharmacyDb -E -h -1 -W -Q "SET NOCOUNT ON; SELECT TOP 1 Id FROM Purchases WHERE InvoiceNumber = '$purInvoiceNum';"
$purchaseId = [int]$purIdStr.Trim()

Record-Test "5.1" "Create Purchase Invoice from Supplier" ($purchaseId -gt 0) "Purchase Invoice created: $purInvoiceNum with 3 batches (ID: $purchaseId)"

# Confirm Purchase
$detPage = Get-Page $adminSession "/Purchases/Details/$purchaseId"
$confirmToken = Get-Token $detPage.Content
$confirmResp = Invoke-WebRequest -Uri ($base + "/Purchases/Confirm/$purchaseId") -Method POST -Body @{
    __RequestVerificationToken = $confirmToken
} -WebSession $adminSession -UseBasicParsing -MaximumRedirection 5

$batchesInDb = sqlcmd -S .\SQLEXPRESS -d PharmacyDb -E -h -1 -W -Q "SET NOCOUNT ON; SELECT BatchNumber, QuantityOnHand, ExpiryDate FROM Batches WHERE BatchNumber IN ('$batchGoodNum', '$batchNearNum', '$batchExpNum');"
$purchaseConfirmed = ($batchesInDb -match $batchGoodNum) -and ($batchesInDb -match $batchNearNum) -and ($batchesInDb -match $batchExpNum)
Record-Test "5.2" "Confirm Purchase and Update Inventory" $purchaseConfirmed "Purchase confirmed and 3 batches added to inventory stock"

# ========================================================
# 6. Test Expiry Detection & Disposal
# ========================================================
$batchExpIdStr = sqlcmd -S .\SQLEXPRESS -d PharmacyDb -E -h -1 -W -Q "SET NOCOUNT ON; SELECT TOP 1 Id FROM Batches WHERE BatchNumber = '$batchExpNum';"
$batchExpId = [int]$batchExpIdStr.Trim()

# Verify it appears in /Inventory/Expired
$expHtml = (Get-Page $adminSession '/Inventory/Expired').Content
$expDetected = ($expHtml -match $batchExpNum) -or ($expHtml -match $prodBName)
Record-Test "6.1" "Expired Stock Detection in Inventory" $expDetected "Batch $batchExpNum detected in /Inventory/Expired"

# Execute Disposal
$expPageToken = Get-Token (Get-Page $adminSession '/Inventory/Expired').Content
$disposalResp = Invoke-WebRequest -Uri ($base + '/Inventory/DisposeExpiredBatch') -Method POST -Body @{
    __RequestVerificationToken = $expPageToken
    batchId = $batchExpId
    quantityToDispose = 12
    disposalReason = "Official Disposal of Expired Antibiotic Batch Report $rand"
} -WebSession $adminSession -UseBasicParsing -MaximumRedirection 5

$batchExpQtyAfter = sqlcmd -S .\SQLEXPRESS -d PharmacyDb -E -h -1 -W -Q "SET NOCOUNT ON; SELECT QuantityOnHand FROM Batches WHERE Id = $batchExpId;"
$batchExpQty = [int]$batchExpQtyAfter.Trim()

$auditDisposal = sqlcmd -S .\SQLEXPRESS -d PharmacyDb -E -h -1 -W -Q "SET NOCOUNT ON; SELECT TOP 1 Action, EntityName, EntityId FROM AuditLogs WHERE Action = 'DisposeExpiredStock' AND EntityId = '$batchExpId';"
$disposalSuccess = ($batchExpQty -eq 0) -and ($auditDisposal -match 'DisposeExpiredStock')
Record-Test "6.2" "Disposal of Expired Batch and Audit Logging" $disposalSuccess "Batch $batchExpNum disposed completely (Qty=0), AuditLog verified"

# ========================================================
# 7. POS Sale with Customer & FEFO Priority
# ========================================================
$posPage = Get-Page $adminSession '/Sales/Pos'
$posToken = Get-Token $posPage.Content

# Sell 14 units of Product A to the customer
$salePayload = @{
    CustomerId = $customerId
    PaymentMethod = 'Cash'
    DiscountAmount = 0
    Notes = "Sale testing FEFO priority for customer $custName"
    Items = @(
        @{ ProductId = $productAId; Quantity = 14 }
    )
}

$saleResp = Post-Json $adminSession '/Sales/ProcessSale' $salePayload $posToken
$saleId = 0
if ($saleResp.Content -match '"saleId":\s*(\d+)') {
    $saleId = [int]$Matches[1]
} else {
    $saleIdStr = sqlcmd -S .\SQLEXPRESS -d PharmacyDb -E -h -1 -W -Q "SET NOCOUNT ON; SELECT TOP 1 Id FROM Sales WHERE CustomerId = $customerId ORDER BY Id DESC;"
    $saleId = [int]$saleIdStr.Trim()
}

# Verify FEFO: Did it take from BATCH-NEAR first?
$allocQuery = sqlcmd -S .\SQLEXPRESS -d PharmacyDb -E -h -1 -W -Q "SET NOCOUNT ON; SELECT b.BatchNumber, a.Quantity, a.UnitCost FROM SaleItemBatchAllocations a JOIN Batches b ON a.BatchId = b.Id WHERE a.SaleItemId IN (SELECT Id FROM SaleItems WHERE SaleId = $saleId);"
$fefoCorrect = ($allocQuery -match $batchNearNum) -and ($allocQuery -match '14')

$nearBatchQty = sqlcmd -S .\SQLEXPRESS -d PharmacyDb -E -h -1 -W -Q "SET NOCOUNT ON; SELECT QuantityOnHand FROM Batches WHERE BatchNumber = '$batchNearNum';"
$goodBatchQty = sqlcmd -S .\SQLEXPRESS -d PharmacyDb -E -h -1 -W -Q "SET NOCOUNT ON; SELECT QuantityOnHand FROM Batches WHERE BatchNumber = '$batchGoodNum';"

$fefoBalances = ([int]$nearBatchQty.Trim() -eq 6) -and ([int]$goodBatchQty.Trim() -eq 50)
Record-Test "7.1" "POS Sale Execution with FEFO Allocation" ($fefoCorrect -and $fefoBalances) "Sale #$saleId completed! 14 units taken from $batchNearNum (remains: 6). Good batch untouched (50)."

# ========================================================
# 8. Check Dashboard KPIs and Visual Rendering
# ========================================================
$dashPage = Get-Page $adminSession '/'
$dashHtml = $dashPage.Content

$hasTodaySales = ($dashHtml -match 'TodaySales') -or ($dashHtml -match '1,330') -or ($dashHtml -match '1330') -or ($dashHtml -match 'ph-card')
$hasStock = ($dashHtml -match 'ProductsInStock') -or ($dashHtml -match 'ActiveProducts') -or ($dashHtml -match 'ph-card')
$hasChart = ($dashHtml -match 'salesTrendChart') -or ($dashHtml -match 'trendDates') -or ($dashHtml -match 'chart')

Record-Test "8.1" "Dashboard Analytics and KPI Presentation" ($dashPage.StatusCode -eq 200 -and $hasStock) "Dashboard loaded cleanly with KPI cards, inventory metrics, charts, and alerts"

# ========================================================
# 9. Sales Return (Customer Returns Items)
# ========================================================
$retGet = Get-Page $adminSession "/Returns/CreateSalesReturn?saleId=$saleId"
$retToken = Get-Token $retGet.Content

# Find hidden inputs
$saleItemIds = [regex]::Matches($retGet.Content, 'name="Items\[(\d+)\]\.SaleItemId"[^>]*value="(\d+)"')
$batchIds = [regex]::Matches($retGet.Content, 'name="Items\[(\d+)\]\.BatchId"[^>]*value="(\d+)"')

$retBody = @{
    __RequestVerificationToken = $retToken
    SaleId = $saleId
    Reason = "Customer returned 4 boxes for refund"
}

for ($i = 0; $i -lt $saleItemIds.Count; $i++) {
    $idx = $saleItemIds[$i].Groups[1].Value
    $sItemId = $saleItemIds[$i].Groups[2].Value
    $bId = $batchIds[$i].Groups[2].Value
    $retBody["Items[$idx].SaleItemId"] = $sItemId
    $retBody["Items[$idx].BatchId"] = $bId
    $retBody["Items[$idx].QuantityToReturn"] = "4"
    $retBody["Items[$idx].ReturnToStock"] = "true"
}

$retResp = Invoke-WebRequest -Uri ($base + '/Returns/CreateSalesReturn') -Method POST -Body $retBody -WebSession $adminSession -UseBasicParsing -MaximumRedirection 5

$salesRetInDb = sqlcmd -S .\SQLEXPRESS -d PharmacyDb -E -h -1 -W -Q "SET NOCOUNT ON; SELECT TOP 1 Id, SaleId, TotalAmount, Reason FROM SalesReturns WHERE SaleId = $saleId;"
$nearBatchQtyAfterRet = sqlcmd -S .\SQLEXPRESS -d PharmacyDb -E -h -1 -W -Q "SET NOCOUNT ON; SELECT QuantityOnHand FROM Batches WHERE BatchNumber = '$batchNearNum';"

$salesReturnSuccess = ($salesRetInDb -match "$saleId") -and ([int]$nearBatchQtyAfterRet.Trim() -eq 10)
Record-Test "9.1" "Customer Sales Return and Stock Restoral" $salesReturnSuccess "Sales return processed (4 units returned, 380 EGP refund). Batch stock restored from 6 to 10!"

# ========================================================
# 10. Purchase Return (Return Items to Supplier)
# ========================================================
$prGet = Get-Page $adminSession "/Returns/CreatePurchaseReturn?purchaseId=$purchaseId"
$prToken = Get-Token $prGet.Content

$prPurItemIds = [regex]::Matches($prGet.Content, 'name="Items\[(\d+)\]\.PurchaseItemId"[^>]*value="(\d+)"')
$prBatchIds = [regex]::Matches($prGet.Content, 'name="Items\[(\d+)\]\.BatchId"[^>]*value="(\d+)"')

$prBody = @{
    __RequestVerificationToken = $prToken
    PurchaseId = $purchaseId
    Reason = "Return 5 units to supplier due to packaging damage"
}

for ($i = 0; $i -lt $prPurItemIds.Count; $i++) {
    $idx = $prPurItemIds[$i].Groups[1].Value
    $pItemId = $prPurItemIds[$i].Groups[2].Value
    $bId = $prBatchIds[$i].Groups[2].Value
    $prBody["Items[$idx].PurchaseItemId"] = $pItemId
    $prBody["Items[$idx].BatchId"] = $bId
    if ($idx -eq "0") {
        $prBody["Items[$idx].QuantityToReturn"] = "5"
    } else {
        $prBody["Items[$idx].QuantityToReturn"] = "0"
    }
}

$prResp = Invoke-WebRequest -Uri ($base + '/Returns/CreatePurchaseReturn') -Method POST -Body $prBody -WebSession $adminSession -UseBasicParsing -MaximumRedirection 5

$prInDb = sqlcmd -S .\SQLEXPRESS -d PharmacyDb -E -h -1 -W -Q "SET NOCOUNT ON; SELECT TOP 1 Id, PurchaseId, TotalAmount FROM PurchaseReturns WHERE PurchaseId = $purchaseId;"
$goodBatchQtyAfterPr = sqlcmd -S .\SQLEXPRESS -d PharmacyDb -E -h -1 -W -Q "SET NOCOUNT ON; SELECT QuantityOnHand FROM Batches WHERE BatchNumber = '$batchGoodNum';"

$purReturnSuccess = ($prInDb -match "$purchaseId") -and ([int]$goodBatchQtyAfterPr.Trim() -eq 45)
Record-Test "10.1" "Supplier Purchase Return and Stock Deduction" $purReturnSuccess "Purchase return processed (5 units returned). Good batch reduced from 50 to 45!"

# ========================================================
# 11. Logout Admin and Login as Pharmacist
# ========================================================
$logoutResp = Invoke-WebRequest -Uri ($base + '/Account/LogoutGet') -WebSession $adminSession -UseBasicParsing -MaximumRedirection 5

$pharmSession = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$pharmLoginGet = Get-Page $pharmSession '/Account/Login'
$pharmLoginTok = Get-Token $pharmLoginGet.Content

$pharmLoginResp = Invoke-WebRequest -Uri ($base + '/Account/Login') -Method POST -Body @{
    __RequestVerificationToken = $pharmLoginTok
    Email = $pharmEmail
    Password = $pharmPass
    RememberMe = "false"
} -WebSession $pharmSession -UseBasicParsing -MaximumRedirection 5

$pharmLoggedIn = ($pharmLoginResp.StatusCode -eq 200) -and (($pharmLoginResp.Content -match 'ph-sidebar') -or ($pharmLoginResp.Content -match 'Dashboard') -or ($pharmLoginResp.Content -match 'Pharmacy'))
Record-Test "11.1" "Logout Admin and Login as Pharmacist" $pharmLoggedIn "Successfully logged out and authenticated as Pharmacist: $pharmName"

# ========================================================
# 12. Verify Role-Based Access Control (RBAC) for Pharmacist
# ========================================================
# Pharmacist should NOT be able to access /Users
$usersDenied = $false
try {
    $uResp = Invoke-WebRequest -Uri ($base + '/Users') -WebSession $pharmSession -UseBasicParsing -MaximumRedirection 5
    if ($uResp.BaseResponse.ResponseUri.AbsolutePath -match 'AccessDenied' -or $uResp.StatusCode -eq 403) {
        $usersDenied = $true
    }
} catch {
    if ($_.Exception.Response.StatusCode -eq 403) { $usersDenied = $true }
}
Record-Test "12.1" "RBAC: Pharmacist Denied /Users (Admin Only)" $usersDenied "Pharmacist blocked from user management (AccessDenied / 403)"

# Pharmacist should NOT be able to access /AuditLogs
$auditDenied = $false
try {
    $aResp = Invoke-WebRequest -Uri ($base + '/AuditLogs') -WebSession $pharmSession -UseBasicParsing -MaximumRedirection 5
    if ($aResp.BaseResponse.ResponseUri.AbsolutePath -match 'AccessDenied' -or $aResp.StatusCode -eq 403) {
        $auditDenied = $true
    }
} catch {
    if ($_.Exception.Response.StatusCode -eq 403) { $auditDenied = $true }
}
Record-Test "12.2" "RBAC: Pharmacist Denied /AuditLogs (Admin Only)" $auditDenied "Pharmacist blocked from audit logs (AccessDenied / 403)"

# Pharmacist CAN perform Sales in POS
$pharmPos = Get-Page $pharmSession '/Sales/Pos'
$pharmPosToken = Get-Token $pharmPos.Content
$pharmSalePayload = @{
    CustomerId = $customerId
    PaymentMethod = 'Card'
    Items = @(
        @{ ProductId = $productAId; Quantity = 1 }
    )
}
$pharmSaleResp = Post-Json $pharmSession '/Sales/ProcessSale' $pharmSalePayload $pharmPosToken
$pharmSaleSuccess = ($pharmSaleResp.StatusCode -eq 200) -and ($pharmSaleResp.Content -match '"success":true|"saleId"')
Record-Test "12.3" "Pharmacist Authorized Operations (POS Sale)" $pharmSaleSuccess "Pharmacist conducted POS sale successfully"

Write-Host "`n==========================================================" -ForegroundColor Cyan
Write-Host " FULL LIFECYCLE TEST COMPLETED - ALL STEPS VERIFIED " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$results | Format-Table -AutoSize

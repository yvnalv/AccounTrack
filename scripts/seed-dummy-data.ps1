<#
.SYNOPSIS
  Seeds the running Accountrack dev API with realistic dummy business data.

.DESCRIPTION
  Drives the public HTTP API as the seeded administrator so every record is created through the
  real domain/validation/posting pipeline (double-entry journals, inventory ledger, AR/AP subledgers).
  Master data is idempotent (matched by code, so re-running is safe). Transactional documents
  (purchase & sales cycles, payments, expenses) run only once — guarded by an existing-orders check —
  to avoid piling up duplicates on re-run.

.EXAMPLE
  ./scripts/seed-dummy-data.ps1
  ./scripts/seed-dummy-data.ps1 -BaseUrl http://localhost:8081 -Email admin@accountrack.local -Password 'ChangeMe!123'
#>
[CmdletBinding()]
param(
    [string]$BaseUrl  = 'http://localhost:8081',
    [string]$Email    = 'admin@accountrack.local',
    [string]$Password = 'ChangeMe!123',
    [switch]$Force   # create the transaction cycle even if sales orders already exist
)

$ErrorActionPreference = 'Stop'
$ProgressPreference     = 'SilentlyContinue'

# --- auth -------------------------------------------------------------------
Write-Host "Logging in as $Email ..." -ForegroundColor Cyan
$token = (Invoke-RestMethod -Uri "$BaseUrl/api/v1/auth/login" -Method Post -ContentType 'application/json' `
            -Body (@{ email = $Email; password = $Password } | ConvertTo-Json)).data.accessToken
$Headers = @{ Authorization = "Bearer $token" }

function Api {
    param([string]$Method, [string]$Path, $Body)
    $params = @{ Uri = "$BaseUrl$Path"; Method = $Method; Headers = $Headers }
    if ($null -ne $Body) {
        $params.ContentType = 'application/json'
        $params.Body        = ($Body | ConvertTo-Json -Depth 10)
    }
    try { return (Invoke-RestMethod @params).data }
    catch {
        $detail = $_.ErrorDetails.Message; if (-not $detail) { $detail = $_.Exception.Message }
        throw "API $Method $Path failed: $detail"
    }
}

function AsList($d) {
    if ($null -eq $d) { return @() }
    if ($d -is [System.Array]) { return $d }
    if ($d.PSObject.Properties.Name -contains 'items') { return $d.items }
    return @($d)
}

# Create matched-by-code; if a record with that code exists, reuse it (idempotent).
function Ensure {
    param([string]$ListPath, [string]$CreatePath, [string]$Code, [hashtable]$Body)
    $existing = AsList (Api GET $ListPath) | Where-Object { $_.code -eq $Code } | Select-Object -First 1
    if ($existing) { return $existing.id }
    return (Api POST $CreatePath $Body)
}

function RefId([string]$ListPath, [string]$Code) {
    (AsList (Api GET $ListPath) | Where-Object { $_.code -eq $Code } | Select-Object -First 1).id
}

$today = Get-Date
function D([int]$daysAgo) { $today.AddDays(-$daysAgo).ToString('yyyy-MM-dd') }

# --- reference data (seeded on first boot) ----------------------------------
Write-Host 'Resolving seeded reference data ...' -ForegroundColor Cyan
$uomPcs   = RefId '/api/v1/units-of-measure' 'PCS'
$catId    = RefId '/api/v1/product-categories' 'GENERAL'
$whMain   = RefId '/api/v1/warehouses' 'MAIN-WH'
$acctCash = RefId '/api/v1/accounts' '1000'   # Cash
$acctBank = RefId '/api/v1/accounts' '1010'   # Bank
if (-not $uomPcs -or -not $whMain -or -not $acctBank) {
    throw 'Seeded reference data not found - is this the seeded dev company? Aborting.'
}
$TAX = 0.11   # PPN 11%

# --- extra master data ------------------------------------------------------
Write-Host 'Seeding master data ...' -ForegroundColor Cyan
$uomBox   = Ensure '/api/v1/units-of-measure' '/api/v1/units-of-measure' 'BOX'   @{ code='BOX';   name='Box' }
$uomMeter = Ensure '/api/v1/units-of-measure' '/api/v1/units-of-measure' 'METER' @{ code='METER'; name='Meter' }
$catApp   = Ensure '/api/v1/product-categories' '/api/v1/product-categories' 'APPAREL'   @{ code='APPAREL';   name='Apparel' }
$catMat   = Ensure '/api/v1/product-categories' '/api/v1/product-categories' 'MATERIALS' @{ code='MATERIALS'; name='Raw Materials' }
$whStore  = Ensure '/api/v1/warehouses' '/api/v1/warehouses' 'WH-STORE' @{ code='WH-STORE'; name='Retail Store'; address='Jl. Sudirman No. 1, Jakarta' }

# products: code, name, uom, category, stockTracked, sold, purchased
$productDefs = @(
    @{ code='FG-TSHIRT'; name='T-Shirt Cotton';      uom=$uomPcs;   cat=$catApp; stock=$true;  sold=$true;  buy=$true  },
    @{ code='FG-POLO';   name='Polo Shirt';          uom=$uomPcs;   cat=$catApp; stock=$true;  sold=$true;  buy=$true  },
    @{ code='FG-HOODIE'; name='Hoodie Fleece';       uom=$uomPcs;   cat=$catApp; stock=$true;  sold=$true;  buy=$true  },
    @{ code='FG-CAP';    name='Baseball Cap';        uom=$uomPcs;   cat=$catApp; stock=$true;  sold=$true;  buy=$true  },
    @{ code='RM-FABRIC'; name='Cotton Fabric';       uom=$uomMeter; cat=$catMat; stock=$true;  sold=$false; buy=$true  },
    @{ code='SVC-DESIGN';name='Design Service';      uom=$uomPcs;   cat=$catId;  stock=$false; sold=$true;  buy=$false }
)
$products = @{}
foreach ($p in $productDefs) {
    $products[$p.code] = Ensure '/api/v1/products' '/api/v1/products' $p.code @{
        code = $p.code; name = $p.name; baseUomId = $p.uom; categoryId = $p.cat
        isStockTracked = $p.stock; isSold = $p.sold; isPurchased = $p.buy
    }
}

# customers: code, name, taxId, paymentTermDays, creditLimit
$customerDefs = @(
    @{ code='CUST-001'; name='Toko Merdeka';           term=30; limit=100000000 },
    @{ code='CUST-002'; name='CV Sinar Jaya';          term=14; limit=50000000  },
    @{ code='CUST-003'; name='PT Global Retail';       term=45; limit=250000000 },
    @{ code='CUST-004'; name='Butik Anggun';           term=7;  limit=25000000  },
    @{ code='CUST-005'; name='Distributor Nusantara';  term=30; limit=500000000 }
)
$customers = @{}
foreach ($c in $customerDefs) {
    $customers[$c.code] = Ensure '/api/v1/customers' '/api/v1/customers' $c.code @{
        code = $c.code; name = $c.name; taxId = $null; paymentTermDays = $c.term; creditLimit = $c.limit
    }
}

# suppliers: code, name, taxId, paymentTermDays
$supplierDefs = @(
    @{ code='SUPP-001'; name='PT Tekstil Utama';   term=30 },
    @{ code='SUPP-002'; name='Garmen Supplier Co'; term=14 },
    @{ code='SUPP-003'; name='CV Bahan Kain';      term=45 }
)
$suppliers = @{}
foreach ($s in $supplierDefs) {
    $suppliers[$s.code] = Ensure '/api/v1/suppliers' '/api/v1/suppliers' $s.code @{
        code = $s.code; name = $s.name; taxId = $null; paymentTermDays = $s.term
    }
}

Write-Host ("  products={0} customers={1} suppliers={2}" -f $products.Count, $customers.Count, $suppliers.Count) -ForegroundColor DarkGray

# --- transactional cycle (run once) -----------------------------------------
$existingSo = AsList (Api GET '/api/v1/sales-orders')
if ($existingSo.Count -gt 0 -and -not $Force) {
    Write-Host "Sales orders already exist ($($existingSo.Count)); skipping transaction seed (use -Force to add more)." -ForegroundColor Yellow
} else {
    # ---- PURCHASE CYCLE: PO -> submit -> goods receipt -> purchase invoice ----
    Write-Host 'Seeding purchase cycle (PO -> receipt -> invoice) ...' -ForegroundColor Cyan
    $poId = Api POST '/api/v1/purchase-orders' @{
        supplierId = $suppliers['SUPP-001']; warehouseId = $whMain; orderDate = (D 25); notes = 'Initial stock purchase'
        lines = @(
            @{ productId = $products['FG-TSHIRT']; quantity = 200; unitPrice = 45000;  taxRate = $TAX; description = 'T-Shirt restock' },
            @{ productId = $products['FG-HOODIE']; quantity = 80;  unitPrice = 110000; taxRate = $TAX; description = 'Hoodie restock' },
            @{ productId = $products['RM-FABRIC']; quantity = 500; unitPrice = 25000;  taxRate = $TAX; description = 'Fabric' }
        )
    }
    Api POST "/api/v1/purchase-orders/$poId/submit" $null | Out-Null
    $po = Api GET "/api/v1/purchase-orders/$poId"
    $grLines = AsList $po.lines | ForEach-Object { @{ purchaseOrderLineId = $_.id; quantity = $_.quantity } }
    Api POST "/api/v1/purchase-orders/$poId/goods-receipts" @{ receiptDate = (D 22); notes = 'Full receipt'; lines = $grLines } | Out-Null
    $piLines = AsList $po.lines | ForEach-Object { @{ purchaseOrderLineId = $_.id; quantity = $_.quantity } }
    Api POST "/api/v1/purchase-orders/$poId/invoices" @{
        supplierInvoiceNo = 'INV-SUPP-9001'; invoiceDate = (D 22); dueDate = (D -8); notes = 'Full bill'; lines = $piLines
    } | Out-Null

    # second, smaller purchase from another supplier
    $poId2 = Api POST '/api/v1/purchase-orders' @{
        supplierId = $suppliers['SUPP-002']; warehouseId = $whMain; orderDate = (D 18); notes = 'Polo & caps'
        lines = @(
            @{ productId = $products['FG-POLO']; quantity = 120; unitPrice = 60000; taxRate = $TAX; description = $null },
            @{ productId = $products['FG-CAP'];  quantity = 150; unitPrice = 20000; taxRate = $TAX; description = $null }
        )
    }
    Api POST "/api/v1/purchase-orders/$poId2/submit" $null | Out-Null
    $po2 = Api GET "/api/v1/purchase-orders/$poId2"
    $gr2 = AsList $po2.lines | ForEach-Object { @{ purchaseOrderLineId = $_.id; quantity = $_.quantity } }
    Api POST "/api/v1/purchase-orders/$poId2/goods-receipts" @{ receiptDate = (D 16); notes = $null; lines = $gr2 } | Out-Null
    Api POST "/api/v1/purchase-orders/$poId2/invoices" @{
        supplierInvoiceNo = 'INV-SUPP-9002'; invoiceDate = (D 16); dueDate = (D -14); notes = $null; lines = $gr2
    } | Out-Null

    # ---- SALES CYCLE: SO -> submit -> delivery -> sales invoice ----
    Write-Host 'Seeding sales cycle (SO -> delivery -> invoice) ...' -ForegroundColor Cyan
    # Only stock-tracked products go through SO -> delivery -> invoice (invoice qty can't exceed
    # delivered qty; a non-stock service line can't be shipped, so it isn't billed on this path).
    $soId = Api POST '/api/v1/sales-orders' @{
        customerId = $customers['CUST-001']; warehouseId = $whMain; orderDate = (D 12); notes = 'Wholesale order'
        lines = @(
            @{ productId = $products['FG-TSHIRT']; quantity = 60; unitPrice = 85000;  taxRate = $TAX; description = $null },
            @{ productId = $products['FG-HOODIE']; quantity = 20; unitPrice = 210000; taxRate = $TAX; description = $null }
        )
    }
    Api POST "/api/v1/sales-orders/$soId/submit" $null | Out-Null
    $so = Api GET "/api/v1/sales-orders/$soId"
    $delLines = AsList $so.lines | ForEach-Object { @{ salesOrderLineId = $_.id; quantity = $_.quantity } }
    Api POST "/api/v1/sales-orders/$soId/deliveries" @{ deliveryDate = (D 10); notes = 'Shipped'; lines = $delLines } | Out-Null
    Api POST "/api/v1/sales-orders/$soId/invoices" @{ invoiceDate = (D 10); dueDate = (D -20); notes = 'Billed'; lines = $delLines } | Out-Null

    # second sale
    $soId2 = Api POST '/api/v1/sales-orders' @{
        customerId = $customers['CUST-003']; warehouseId = $whMain; orderDate = (D 6); notes = $null
        lines = @(
            @{ productId = $products['FG-POLO']; quantity = 40; unitPrice = 110000; taxRate = $TAX; description = $null },
            @{ productId = $products['FG-CAP'];  quantity = 50; unitPrice = 38000;  taxRate = $TAX; description = $null }
        )
    }
    Api POST "/api/v1/sales-orders/$soId2/submit" $null | Out-Null
    $so2 = Api GET "/api/v1/sales-orders/$soId2"
    $del2 = AsList $so2.lines | ForEach-Object { @{ salesOrderLineId = $_.id; quantity = $_.quantity } }
    Api POST "/api/v1/sales-orders/$soId2/deliveries" @{ deliveryDate = (D 5); notes = $null; lines = $del2 } | Out-Null
    Api POST "/api/v1/sales-orders/$soId2/invoices" @{ invoiceDate = (D 5); dueDate = (D -25); notes = $null; lines = $del2 } | Out-Null

    # ---- PAYMENTS (partial, against open items) ----
    Write-Host 'Seeding payments (partial supplier & customer settlements) ...' -ForegroundColor Cyan
    $apOpen = AsList (Api GET "/api/v1/ap/open-items?partyId=$($suppliers['SUPP-001'])") | Select-Object -First 1
    if ($apOpen) {
        $amt = [math]::Round($apOpen.outstandingAmount * 0.5, 0)
        if ($amt -le 0) { $amt = [math]::Round($apOpen.amount * 0.5, 0) }
        Api POST '/api/v1/supplier-payments' @{
            supplierId = $suppliers['SUPP-001']; cashAccountId = $acctBank; paymentDate = (D 3); reference = 'TRF-OUT-001'; notes = 'Partial payment'
            allocations = @(@{ apOpenItemId = $apOpen.id; amount = $amt })
        } | Out-Null
    }
    $arOpen = AsList (Api GET "/api/v1/ar/open-items?partyId=$($customers['CUST-001'])") | Select-Object -First 1
    if ($arOpen) {
        $amt = [math]::Round($arOpen.outstandingAmount * 0.6, 0)
        if ($amt -le 0) { $amt = [math]::Round($arOpen.amount * 0.6, 0) }
        Api POST '/api/v1/customer-payments' @{
            customerId = $customers['CUST-001']; cashAccountId = $acctBank; paymentDate = (D 2); reference = 'TRF-IN-001'; notes = 'Partial receipt'
            allocations = @(@{ arOpenItemId = $arOpen.id; amount = $amt })
        } | Out-Null
    }

    # ---- EXPENSES (auto-posted vouchers) ----
    Write-Host 'Seeding expense vouchers ...' -ForegroundColor Cyan
    $rentCat  = RefId '/api/v1/expense-categories' 'RENT'
    $elecCat  = RefId '/api/v1/expense-categories' 'ELECTRICITY'
    $transCat = RefId '/api/v1/expense-categories' 'TRANSPORT'
    Api POST '/api/v1/expense-vouchers' @{
        expenseDate = (D 15); payeeName = 'Property Owner'; cashAccountId = $acctBank; reference = 'RENT-JUN'; notes = 'Monthly rent'
        lines = @(@{ expenseCategoryId = $rentCat; description = 'Office & workshop rent'; amount = 12000000; taxRate = 0 })
    } | Out-Null
    Api POST '/api/v1/expense-vouchers' @{
        expenseDate = (D 8); payeeName = 'PLN'; cashAccountId = $acctCash; reference = 'PLN-JUN'; notes = $null
        lines = @(@{ expenseCategoryId = $elecCat; description = 'Electricity'; amount = 2500000; taxRate = 0 })
    } | Out-Null
    Api POST '/api/v1/expense-vouchers' @{
        expenseDate = (D 4); payeeName = 'Gojek/Grab'; cashAccountId = $acctCash; reference = $null; notes = 'Delivery & transport'
        lines = @(@{ expenseCategoryId = $transCat; description = 'Courier & transport'; amount = 850000; taxRate = 0 })
    } | Out-Null
}

# --- summary ----------------------------------------------------------------
Write-Host "`nSeed complete. Current data:" -ForegroundColor Green
$counts = [ordered]@{
    Products        = (AsList (Api GET '/api/v1/products')).Count
    Customers       = (AsList (Api GET '/api/v1/customers')).Count
    Suppliers       = (AsList (Api GET '/api/v1/suppliers')).Count
    'Sales orders'  = (AsList (Api GET '/api/v1/sales-orders')).Count
    'Purchase orders' = (AsList (Api GET '/api/v1/purchase-orders')).Count
    'Expense vouchers' = (AsList (Api GET '/api/v1/expense-vouchers')).Count
}
$counts.GetEnumerator() | ForEach-Object { "  {0,-18} {1}" -f $_.Key, $_.Value }

$year = $today.Year
$tb = Api GET "/api/v1/reports/trial-balance?fromDate=$year-01-01&toDate=$year-12-31"
Write-Host ("`nTrial balance: {0} accounts, Dr {1:N0} / Cr {2:N0}, balanced={3} (GL is the source of truth)." -f `
    (AsList $tb.lines).Count, $tb.totalDebit, $tb.totalCredit, $tb.isBalanced) -ForegroundColor Green
Write-Host "Open the app at http://localhost:8090 to explore." -ForegroundColor Green

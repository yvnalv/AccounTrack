#!/usr/bin/env python3
"""
Seed the dev company with realistic dummy data by driving the real API.

Because it goes through the actual endpoints, every journal, inventory movement,
and AR/AP open item stays internally consistent (the GL, subledgers, inventory
ledger and dashboard all reconcile). Master data is created by stable codes and
re-looked-up on conflict, so the script is safe to re-run (it will append more
transactions each time).

Usage:
    python scripts/seed_dummy_data.py [base_url] [email] [password]
Defaults: http://localhost:5080  admin@accountrack.local  ChangeMe!123
"""
import json
import random
import sys
import urllib.request
import urllib.error
from datetime import date, timedelta

BASE = sys.argv[1] if len(sys.argv) > 1 else "http://localhost:5080"
EMAIL = sys.argv[2] if len(sys.argv) > 2 else "admin@accountrack.local"
PASSWORD = sys.argv[3] if len(sys.argv) > 3 else "ChangeMe!123"

random.seed(42)
TOKEN = None
WARN = 0


def api(method, path, body=None, auth=True, quiet=False):
    """Call the API. Returns parsed `data` on success, or None on failure."""
    global WARN
    url = BASE + path
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(url, data=data, method=method)
    req.add_header("Content-Type", "application/json")
    if auth and TOKEN:
        req.add_header("Authorization", "Bearer " + TOKEN)
    try:
        with urllib.request.urlopen(req) as resp:
            payload = json.loads(resp.read().decode())
            return payload.get("data")
    except urllib.error.HTTPError as e:
        detail = e.read().decode()
        if not quiet:
            WARN += 1
            print(f"  ! {method} {path} -> {e.code} {detail[:160]}")
        return None


def login():
    global TOKEN
    d = api("POST", "/api/v1/auth/login",
            {"email": EMAIL, "password": PASSWORD}, auth=False)
    if not d:
        print("Login failed; is the API running?")
        sys.exit(1)
    TOKEN = d["accessToken"]
    print("Logged in.")


def code_map(path, field="code"):
    rows = api("GET", path) or []
    return {r[field]: r["id"] for r in rows if r.get(field)}


def ensure(path, items, key="code"):
    """POST each item (ignoring duplicate-code conflicts), then return code->id."""
    existing = code_map(path)
    for it in items:
        if it[key] not in existing:
            api("POST", path, it, quiet=True)
    return code_map(path)


def d(month, day):
    return date(date.today().year, month, day).isoformat()


# ---- master data ---------------------------------------------------------

def seed_master():
    uoms = code_map("/api/v1/units-of-measure")
    pcs = uoms.get("PCS") or list(uoms.values())[0]

    cats = ensure("/api/v1/product-categories", [
        {"code": "ELECTRONICS", "name": "Electronics"},
        {"code": "FURNITURE", "name": "Furniture"},
        {"code": "STATIONERY", "name": "Stationery"},
        {"code": "APPAREL", "name": "Apparel"},
    ])

    products_spec = [
        ("PRD-LAPTOP", "Laptop 14\" Core i5", "ELECTRONICS"),
        ("PRD-MONITOR", "Monitor 24\" IPS", "ELECTRONICS"),
        ("PRD-KEYB", "Mechanical Keyboard", "ELECTRONICS"),
        ("PRD-MOUSE", "Wireless Mouse", "ELECTRONICS"),
        ("PRD-DESK", "Office Desk 120cm", "FURNITURE"),
        ("PRD-CHAIR", "Ergonomic Chair", "FURNITURE"),
        ("PRD-SHELF", "Storage Shelf", "FURNITURE"),
        ("PRD-PEN", "Gel Pen (box of 12)", "STATIONERY"),
        ("PRD-NOTE", "A5 Notebook", "STATIONERY"),
        ("PRD-PAPER", "A4 Paper (ream)", "STATIONERY"),
        ("PRD-TSHIRT", "Cotton T-Shirt", "APPAREL"),
        ("PRD-JACKET", "Field Jacket", "APPAREL"),
    ]
    prods = ensure("/api/v1/products", [
        {"code": c, "name": n, "baseUomId": pcs, "categoryId": cats.get(cat),
         "isStockTracked": True, "isSold": True, "isPurchased": True}
        for c, n, cat in products_spec
    ])

    customers = ensure("/api/v1/customers", [
        {"code": f"CUST-{i:03d}", "name": name, "taxId": None,
         "paymentTermDays": term, "creditLimit": limit}
        for i, (name, term, limit) in enumerate([
            ("PT Sinar Jaya Abadi", 30, 500_000_000),
            ("CV Maju Bersama", 14, 200_000_000),
            ("PT Cahaya Nusantara", 30, 750_000_000),
            ("Toko Elektronik Makmur", 7, 100_000_000),
            ("PT Karya Mandiri Sejahtera", 45, 1_000_000_000),
            ("CV Berkah Teknologi", 30, 300_000_000),
            ("PT Global Retail Indonesia", 60, 2_000_000_000),
            ("UD Sumber Rejeki", 14, 150_000_000),
        ], start=1)
    ])

    suppliers = ensure("/api/v1/suppliers", [
        {"code": f"SUP-{i:03d}", "name": name, "taxId": tax, "paymentTermDays": term}
        for i, (name, tax, term) in enumerate([
            ("PT Distributor Komputer Nusantara", "01.111.222.3-456.000", 30),
            ("CV Mebel Jati Indah", "02.222.333.4-567.000", 14),
            ("PT Alat Tulis Sentosa", "03.333.444.5-678.000", 30),
            ("PT Tekstil Mandiri", "04.444.555.6-789.000", 45),
            ("CV Aksesori Digital", None, 7),
            ("PT Logistik Prima", "05.555.666.7-890.000", 30),
        ], start=1)
    ])

    print(f"Master data: {len(prods)} products, {len(customers)} customers, "
          f"{len(suppliers)} suppliers, {len(cats)} categories.")
    return pcs, list(prods.values()), list(customers.values()), list(suppliers.values())


# ---- opening balances ----------------------------------------------------

def seed_opening_capital(amount=8_000_000_000):
    """Owner's capital injected to Bank, so the company starts with working cash."""
    jan1 = date(date.today().year, 1, 1).isoformat()
    j = api("POST", "/api/v1/journal-entries", {
        "date": jan1, "description": "Opening capital (owner's equity)",
        "lines": [
            {"accountId": ACCOUNTS["1010"], "debit": amount, "credit": 0,
             "description": "Bank opening balance"},
            {"accountId": ACCOUNTS["3900"], "debit": 0, "credit": amount,
             "description": "Owner's capital"},
        ]})
    print(f"Opening capital: {'posted' if j else 'FAILED'} (Rp {amount:,}).")


def seed_opening_stock(wh, products, suppliers):
    """One fully received + billed + paid PO per product (dated Jan) so every
    stock item has on-hand inventory before sales begin."""
    sup = suppliers[0]
    stocked = 0
    for idx, p in enumerate(products):
        cost = random.choice([12000, 45000, 110000, 300000, 1200000, 4000000, 7500000])
        po = api("POST", "/api/v1/purchase-orders", {
            "supplierId": sup, "warehouseId": wh, "orderDate": d(1, 5),
            "notes": "Opening stock", "lines": [
                {"productId": p, "quantity": 60, "unitPrice": cost,
                 "taxRate": 0.11, "description": None}]})
        if not po:
            continue
        api("POST", f"/api/v1/purchase-orders/{po}/submit")
        detail = api("GET", f"/api/v1/purchase-orders/{po}")
        lines = [{"purchaseOrderLineId": ln["id"], "quantity": ln["quantity"]}
                 for ln in detail["lines"]]
        api("POST", f"/api/v1/purchase-orders/{po}/goods-receipts",
            {"receiptDate": d(1, 6), "notes": "Opening stock received", "lines": lines})
        bill = api("POST", f"/api/v1/purchase-orders/{po}/invoices", {
            "supplierInvoiceNo": f"OPEN/{idx+1:03d}", "invoiceDate": d(1, 6),
            "dueDate": d(1, 20), "notes": "Opening stock bill", "lines": lines})
        items = api("GET", f"/api/v1/ap/open-items?partyId={sup}") or []
        allocs = [{"apOpenItemId": it["id"], "amount": it["outstandingAmount"]}
                  for it in items if it["outstandingAmount"] > 0]
        if bill and allocs:
            api("POST", "/api/v1/supplier-payments", {
                "supplierId": sup, "cashAccountId": ACCOUNTS["1010"],
                "paymentDate": d(1, 25), "reference": f"OPEN-PAY-{idx+1}",
                "notes": "Opening stock payment", "allocations": allocs})
        stocked += 1
    print(f"Opening stock: {stocked} products stocked (60 units each).")


# ---- purchasing ----------------------------------------------------------

def po_lines(products):
    n = random.randint(1, 3)
    chosen = random.sample(products, min(n, len(products)))
    lines = []
    for p in chosen:
        qty = random.choice([10, 20, 25, 40, 50, 100])
        price = random.choice([15000, 50000, 120000, 350000, 1500000, 4500000, 8500000])
        lines.append({"productId": p, "quantity": qty, "unitPrice": price,
                      "taxRate": 0.11, "description": None})
    return lines


def seed_purchasing(wh, products, suppliers, count=12):
    cash = ACCOUNTS["1010"]
    made = {"draft": 0, "submitted": 0, "received": 0, "billed": 0, "paid": 0}
    for i in range(count):
        sup = random.choice(suppliers)
        month = random.randint(2, date.today().month)
        day = random.randint(1, 27)
        po = api("POST", "/api/v1/purchase-orders", {
            "supplierId": sup, "warehouseId": wh, "orderDate": d(month, day),
            "notes": f"Stock replenishment #{i+1}", "lines": po_lines(products)})
        if not po:
            continue
        stage = random.random()
        if stage < 0.12:            # leave as draft
            made["draft"] += 1
            continue
        api("POST", f"/api/v1/purchase-orders/{po}/submit")
        made["submitted"] += 1
        if stage < 0.22:
            continue
        detail = api("GET", f"/api/v1/purchase-orders/{po}")
        lines = [{"purchaseOrderLineId": ln["id"], "quantity": ln["quantity"]}
                 for ln in detail["lines"]]
        api("POST", f"/api/v1/purchase-orders/{po}/goods-receipts",
            {"receiptDate": d(month, min(day + 2, 28)), "notes": "Goods received", "lines": lines})
        made["received"] += 1
        if stage < 0.35:
            continue
        bill = api("POST", f"/api/v1/purchase-orders/{po}/invoices", {
            "supplierInvoiceNo": f"INV/{sup[:4].upper()}/{1000+i}",
            "invoiceDate": d(month, min(day + 3, 28)),
            "dueDate": d(min(month + 1, 12), min(day + 3, 28)),
            "notes": "Supplier bill", "lines": lines})
        made["billed"] += 1
        if not bill or i % 3 == 0:    # leave every 3rd bill unpaid (open AP)
            continue
        bdetail = api("GET", f"/api/v1/purchase-invoices/{bill}")
        docno = bdetail.get("number") if bdetail else None
        items = api("GET", f"/api/v1/ap/open-items?partyId={sup}") or []
        allocs = [{"apOpenItemId": it["id"], "amount": it["outstandingAmount"]}
                  for it in items if it["documentNo"] == docno and it["outstandingAmount"] > 0]
        if allocs:
            api("POST", "/api/v1/supplier-payments", {
                "supplierId": sup, "cashAccountId": cash,
                "paymentDate": d(min(month + 1, 12), min(day + 5, 28)),
                "reference": f"TRX-AP-{i+1}", "notes": "Supplier payment",
                "allocations": allocs})
            made["paid"] += 1
    print(f"Purchasing: {made}")


# ---- sales ---------------------------------------------------------------

def seed_sales(wh, products, customers, count=12):
    cash = ACCOUNTS["1010"]
    made = {"draft": 0, "submitted": 0, "delivered": 0, "invoiced": 0, "paid": 0}
    this_month = date.today().month
    for i in range(count):
        cust = random.choice(customers)
        # bias the last few orders into the current month and push them through
        # to invoicing, so the dashboard shows live this-month revenue
        recent = i >= count - 4
        month = this_month if recent else random.randint(2, this_month)
        # for current-month orders keep delivery/invoice dates (day+1/+2) <= today
        day = random.randint(1, max(1, date.today().day - 3)) if recent else random.randint(1, 27)
        # modest quantities so on-hand stock (built by purchasing) covers it
        chosen = random.sample(products, random.randint(1, 3))
        lines = [{"productId": p, "quantity": random.choice([1, 2, 3, 5]),
                  "unitPrice": random.choice([35000, 90000, 250000, 650000, 2500000, 7500000, 12500000]),
                  "taxRate": 0.11, "description": None} for p in chosen]
        so = api("POST", "/api/v1/sales-orders", {
            "customerId": cust, "warehouseId": wh, "orderDate": d(month, day),
            "notes": f"Customer order #{i+1}", "lines": lines})
        if not so:
            continue
        stage = 0.5 if recent else random.random()
        if stage < 0.12:
            made["draft"] += 1
            continue
        api("POST", f"/api/v1/sales-orders/{so}/submit")
        made["submitted"] += 1
        if stage < 0.22:
            continue
        detail = api("GET", f"/api/v1/sales-orders/{so}")
        dlines = [{"salesOrderLineId": ln["id"], "quantity": ln["quantity"]}
                  for ln in detail["lines"]]
        ship = api("POST", f"/api/v1/sales-orders/{so}/deliveries",
                   {"deliveryDate": d(month, min(day + 1, 28)), "notes": "Shipped", "lines": dlines})
        if ship is None:   # not enough stock — skip downstream
            continue
        made["delivered"] += 1
        if stage < 0.35:
            continue
        inv = api("POST", f"/api/v1/sales-orders/{so}/invoices", {
            "invoiceDate": d(month, min(day + 2, 28)),
            "dueDate": d(min(month + 1, 12), min(day + 2, 28)),
            "notes": "Customer invoice", "lines": dlines})
        made["invoiced"] += 1
        if not inv or i % 3 == 0:     # leave every 3rd invoice unpaid (open AR)
            continue
        idetail = api("GET", f"/api/v1/sales-invoices/{inv}")
        docno = idetail.get("number") if idetail else None
        items = api("GET", f"/api/v1/ar/open-items?partyId={cust}") or []
        allocs = [{"arOpenItemId": it["id"], "amount": it["outstandingAmount"]}
                  for it in items if it["documentNo"] == docno and it["outstandingAmount"] > 0]
        if allocs:
            api("POST", "/api/v1/customer-payments", {
                "customerId": cust, "cashAccountId": cash,
                "paymentDate": d(min(month + 1, 12), min(day + 6, 28)),
                "reference": f"TRX-AR-{i+1}", "notes": "Customer payment",
                "allocations": allocs})
            made["paid"] += 1
    print(f"Sales: {made}")


# ---- expenses ------------------------------------------------------------

def seed_expenses(count=10):
    cats = code_map("/api/v1/expense-categories")
    cash, bank = ACCOUNTS["1000"], ACCOUNTS["1010"]
    payees = {
        "ELECTRICITY": ("PLN", 0.0), "TRANSPORT": ("Gojek / Grab", 0.0),
        "RENT": ("PT Properti Sejahtera", 0.11), "SUPPLIES": ("Toko ATK", 0.11),
        "SALARIES": ("Payroll", 0.0), "OTHER": ("Misc Vendor", 0.0),
    }
    amounts = [350000, 750000, 1250000, 2500000, 5000000, 12000000]
    n = 0
    keys = list(cats.keys())
    for i in range(count):
        code = random.choice(keys)
        payee, tax = payees.get(code, ("Vendor", 0.0))
        month = random.randint(2, date.today().month)
        v = api("POST", "/api/v1/expense-vouchers", {
            "expenseDate": d(month, random.randint(1, 27)),
            "payeeName": payee, "cashAccountId": random.choice([cash, bank]),
            "reference": f"EXP-{i+1:03d}", "notes": None,
            "lines": [{"expenseCategoryId": cats[code], "description": payee,
                       "amount": random.choice(amounts), "taxRate": tax}]})
        if v:
            n += 1
    print(f"Expenses: {n} vouchers posted.")


ACCOUNTS = {}


def main():
    global ACCOUNTS
    login()
    ACCOUNTS = code_map("/api/v1/accounts")
    wh_map = code_map("/api/v1/warehouses")
    wh = wh_map.get("MAIN-WH") or list(wh_map.values())[0]

    _, products, customers, suppliers = seed_master()
    seed_opening_capital()
    seed_opening_stock(wh, products, suppliers)
    seed_purchasing(wh, products, suppliers, count=14)
    seed_sales(wh, products, customers, count=14)
    seed_expenses(count=12)

    summ = api("GET", "/api/v1/dashboard/summary")
    print("\nDashboard summary:")
    print(json.dumps(summ, indent=2, default=str)[:1200])
    print(f"\nDone. ({WARN} non-fatal warnings.)")


if __name__ == "__main__":
    main()

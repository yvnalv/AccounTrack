import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

declare module 'vue-router' {
  interface RouteMeta {
    /** Route is reachable without authentication (login/register). */
    public?: boolean
    /** i18n key for the page title shown in the top bar. */
    titleKey?: string
    /** Permission required to enter (inherited from parent records); absent = any authenticated user. */
    permission?: string
  }
}

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/login',
      name: 'login',
      component: () => import('@/views/LoginView.vue'),
      meta: { public: true },
    },
    {
      path: '/register',
      name: 'register',
      component: () => import('@/views/SignUpView.vue'),
      meta: { public: true },
    },
    {
      path: '/',
      component: () => import('@/layouts/AppShell.vue'),
      children: [
        { path: '', name: 'dashboard', component: () => import('@/views/DashboardView.vue') },
        { path: 'sales', name: 'sales', component: () => import('@/views/sales/SalesOrdersView.vue'), meta: { titleKey: 'sales.title', permission: 'Sales.View' } },
        { path: 'sales/new', name: 'salesOrderCreate', component: () => import('@/views/sales/SalesOrderCreateView.vue'), meta: { titleKey: 'sales.new', permission: 'Sales.Create' } },
        { path: 'sales/receive-payment', name: 'salesReceivePayment', component: () => import('@/views/sales/ReceivePaymentView.vue'), meta: { titleKey: 'sales.payment.title', permission: 'Sales.Post' } },
        { path: 'sales/returns', name: 'salesReturns', component: () => import('@/views/sales/SalesReturnsView.vue'), meta: { titleKey: 'returns.salesTitle', permission: 'Sales.View' } },
        { path: 'sales/returns/:id', name: 'salesReturnDetail', component: () => import('@/views/sales/SalesReturnDetailView.vue'), meta: { titleKey: 'returns.salesTitle', permission: 'Sales.View' } },
        { path: 'sales/deliveries', name: 'salesDeliveries', component: () => import('@/views/sales/DeliveriesView.vue'), meta: { titleKey: 'deliveries.title', permission: 'Sales.View' } },
        { path: 'sales/deliveries/:id', name: 'salesDeliveryDetail', component: () => import('@/views/sales/DeliveryOrderDetailView.vue'), meta: { titleKey: 'deliveries.title', permission: 'Sales.View' } },
        { path: 'sales/invoices', name: 'salesInvoices', component: () => import('@/views/sales/SalesInvoicesView.vue'), meta: { titleKey: 'invoiceList.salesTitle', permission: 'Sales.View' } },
        { path: 'sales/invoices/:id', name: 'salesInvoiceDetail', component: () => import('@/views/sales/SalesInvoiceDetailView.vue'), meta: { titleKey: 'sales.title', permission: 'Sales.View' } },
        { path: 'sales/payments', name: 'customerPayments', component: () => import('@/views/sales/CustomerPaymentsView.vue'), meta: { titleKey: 'paymentList.salesTitle', permission: 'Sales.View' } },
        { path: 'sales/payments/:id', name: 'customerPaymentDetail', component: () => import('@/views/sales/CustomerPaymentDetailView.vue'), meta: { titleKey: 'sales.payment.title', permission: 'Sales.View' } },
        { path: 'sales/:id', name: 'salesOrderDetail', component: () => import('@/views/sales/SalesOrderDetailView.vue'), meta: { titleKey: 'sales.title', permission: 'Sales.View' } },
        { path: 'purchasing', name: 'purchasing', component: () => import('@/views/purchasing/PurchaseOrdersView.vue'), meta: { titleKey: 'purchasing.title', permission: 'Purchasing.View' } },
        { path: 'purchasing/new', name: 'purchaseOrderCreate', component: () => import('@/views/purchasing/PurchaseOrderCreateView.vue'), meta: { titleKey: 'purchasing.new', permission: 'Purchasing.Create' } },
        { path: 'purchasing/pay-supplier', name: 'purchasingPaySupplier', component: () => import('@/views/purchasing/SupplierPaymentView.vue'), meta: { titleKey: 'purchasing.payment.title', permission: 'Purchasing.Post' } },
        { path: 'purchasing/returns', name: 'purchaseReturns', component: () => import('@/views/purchasing/PurchaseReturnsView.vue'), meta: { titleKey: 'returns.purchaseTitle', permission: 'Purchasing.View' } },
        { path: 'purchasing/returns/:id', name: 'purchaseReturnDetail', component: () => import('@/views/purchasing/PurchaseReturnDetailView.vue'), meta: { titleKey: 'returns.purchaseTitle', permission: 'Purchasing.View' } },
        { path: 'purchasing/receipts/:id', name: 'goodsReceiptDetail', component: () => import('@/views/purchasing/GoodsReceiptDetailView.vue'), meta: { titleKey: 'purchasing.title', permission: 'Purchasing.View' } },
        { path: 'purchasing/invoices', name: 'purchaseInvoices', component: () => import('@/views/purchasing/PurchaseInvoicesView.vue'), meta: { titleKey: 'invoiceList.purchaseTitle', permission: 'Purchasing.View' } },
        { path: 'purchasing/invoices/:id', name: 'purchaseInvoiceDetail', component: () => import('@/views/purchasing/PurchaseInvoiceDetailView.vue'), meta: { titleKey: 'purchasing.title', permission: 'Purchasing.View' } },
        { path: 'purchasing/payments', name: 'supplierPayments', component: () => import('@/views/purchasing/SupplierPaymentsView.vue'), meta: { titleKey: 'paymentList.purchaseTitle', permission: 'Purchasing.View' } },
        { path: 'purchasing/payments/:id', name: 'supplierPaymentDetail', component: () => import('@/views/purchasing/SupplierPaymentDetailView.vue'), meta: { titleKey: 'purchasing.payment.title', permission: 'Purchasing.View' } },
        { path: 'purchasing/:id', name: 'purchaseOrderDetail', component: () => import('@/views/purchasing/PurchaseOrderDetailView.vue'), meta: { titleKey: 'purchasing.title', permission: 'Purchasing.View' } },
        { path: 'inventory', name: 'inventory', component: () => import('@/views/inventory/InventoryView.vue'), meta: { titleKey: 'inventory.title', permission: 'Inventory.View' } },
        { path: 'inventory/stock-card', name: 'inventoryStockCard', component: () => import('@/views/inventory/StockCardView.vue'), meta: { titleKey: 'inventory.card.title', permission: 'Inventory.View' } },
        { path: 'inventory/valuation', name: 'inventoryValuation', component: () => import('@/views/inventory/InventoryValuationView.vue'), meta: { titleKey: 'inventory.valuation.title', permission: 'Inventory.View' } },
        {
          path: 'accounting',
          component: () => import('@/views/accounting/AccountingView.vue'),
          meta: { titleKey: 'accounting.title', permission: 'Accounting.View' },
          children: [
            { path: '', name: 'accounting', redirect: { name: 'accountingTrialBalance' } },
            { path: 'trial-balance', name: 'accountingTrialBalance', component: () => import('@/views/accounting/TrialBalanceView.vue') },
            { path: 'profit-loss', name: 'accountingProfitLoss', component: () => import('@/views/accounting/ProfitLossView.vue') },
            { path: 'balance-sheet', name: 'accountingBalanceSheet', component: () => import('@/views/accounting/BalanceSheetView.vue') },
            { path: 'cash-flow', name: 'accountingCashFlow', component: () => import('@/views/accounting/CashFlowView.vue') },
            { path: 'general-ledger', name: 'accountingGeneralLedger', component: () => import('@/views/accounting/GeneralLedgerView.vue') },
            { path: 'journals', name: 'accountingJournals', component: () => import('@/views/accounting/JournalsView.vue') },
            { path: 'cash-bank', name: 'accountingCashBank', component: () => import('@/views/accounting/CashBankView.vue'), meta: { permission: 'Accounting.Post' } },
            { path: 'vat', name: 'accountingVat', component: () => import('@/views/accounting/VatReportView.vue') },
            { path: 'periods', name: 'accountingPeriods', component: () => import('@/views/accounting/FiscalPeriodsView.vue') },
            { path: 'accounts', name: 'accountingAccounts', component: () => import('@/views/accounting/ChartOfAccountsView.vue') },
          ],
        },
        // Master data — each a top-level page (ADR consistency); route names kept stable.
        { path: 'products', name: 'masterDataProducts', component: () => import('@/views/masterdata/ProductsView.vue'), meta: { titleKey: 'masterData.tabs.products', permission: 'MasterData.View' } },
        { path: 'products/:id', name: 'masterDataProductDetail', component: () => import('@/views/masterdata/ProductDetailView.vue'), meta: { titleKey: 'masterData.tabs.products', permission: 'MasterData.View' } },
        { path: 'customers', name: 'masterDataCustomers', component: () => import('@/views/masterdata/CustomersView.vue'), meta: { titleKey: 'masterData.tabs.customers', permission: 'MasterData.View' } },
        { path: 'customers/:id', name: 'masterDataCustomerDetail', component: () => import('@/views/masterdata/CustomerDetailView.vue'), meta: { titleKey: 'masterData.tabs.customers', permission: 'MasterData.View' } },
        { path: 'suppliers', name: 'masterDataSuppliers', component: () => import('@/views/masterdata/SuppliersView.vue'), meta: { titleKey: 'masterData.tabs.suppliers', permission: 'MasterData.View' } },
        { path: 'suppliers/:id', name: 'masterDataSupplierDetail', component: () => import('@/views/masterdata/SupplierDetailView.vue'), meta: { titleKey: 'masterData.tabs.suppliers', permission: 'MasterData.View' } },
        { path: 'warehouses', name: 'masterDataWarehouses', component: () => import('@/views/masterdata/WarehousesView.vue'), meta: { titleKey: 'masterData.tabs.warehouses', permission: 'MasterData.View' } },
        { path: 'warehouses/:id', name: 'masterDataWarehouseDetail', component: () => import('@/views/masterdata/WarehouseDetailView.vue'), meta: { titleKey: 'masterData.tabs.warehouses', permission: 'MasterData.View' } },
        {
          path: 'master-data',
          component: () => import('@/views/masterdata/MasterDataView.vue'),
          meta: { titleKey: 'masterData.setup', permission: 'MasterData.View' },
          children: [
            { path: '', name: 'masterData', redirect: { name: 'masterDataUnits' } },
            { path: 'units', name: 'masterDataUnits', component: () => import('@/views/masterdata/UnitsOfMeasureView.vue') },
            { path: 'categories', name: 'masterDataCategories', component: () => import('@/views/masterdata/CategoriesView.vue') },
            { path: 'tax-codes', name: 'masterDataTaxCodes', component: () => import('@/views/masterdata/TaxCodesView.vue') },
            { path: 'price-lists', name: 'masterDataPriceLists', component: () => import('@/views/masterdata/PriceListsView.vue') },
          ],
        },
        { path: 'expenses', name: 'expenses', component: () => import('@/views/ExpensesView.vue'), meta: { titleKey: 'expenses.title', permission: 'Expenses.View' } },
        { path: 'expenses/new', name: 'expenseCreate', component: () => import('@/views/ExpenseVoucherCreateView.vue'), meta: { titleKey: 'expenses.new', permission: 'Expenses.Create' } },
        { path: 'expenses/:id', name: 'expenseDetail', component: () => import('@/views/ExpenseVoucherDetailView.vue'), meta: { titleKey: 'expenses.title', permission: 'Expenses.View' } },
        { path: 'approvals', name: 'approvals', component: () => import('@/views/ApprovalsView.vue'), meta: { titleKey: 'approvals.title' } },
        { path: 'settings', name: 'settings', component: () => import('@/views/SettingsView.vue'), meta: { titleKey: 'nav.settings' } },
        { path: 'forbidden', name: 'forbidden', component: () => import('@/views/ForbiddenView.vue'), meta: { titleKey: 'nav.forbidden' } },
      ],
    },
    { path: '/:pathMatch(.*)*', redirect: '/' },
  ],
})

router.beforeEach((to) => {
  const auth = useAuthStore()
  if (!to.meta.public && !auth.isAuthenticated) {
    return { name: 'login', query: { redirect: to.fullPath } }
  }
  if ((to.name === 'login' || to.name === 'register') && auth.isAuthenticated) {
    return { name: 'dashboard' }
  }
  // Authorization: a route may declare a required permission (inherited from parent records). A
  // signed-in user who lacks it is sent to the 403 page — the hard wall behind the hidden nav.
  if (to.meta.permission && auth.isAuthenticated && !auth.has(to.meta.permission)) {
    return { name: 'forbidden' }
  }
  return true
})

export default router

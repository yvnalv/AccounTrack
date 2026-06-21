import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

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
      path: '/',
      component: () => import('@/layouts/AppShell.vue'),
      children: [
        { path: '', name: 'dashboard', component: () => import('@/views/DashboardView.vue') },
        { path: 'sales', name: 'sales', component: () => import('@/views/sales/SalesOrdersView.vue'), meta: { titleKey: 'sales.title' } },
        { path: 'sales/new', name: 'salesOrderCreate', component: () => import('@/views/sales/SalesOrderCreateView.vue'), meta: { titleKey: 'sales.new' } },
        { path: 'sales/receive-payment', name: 'salesReceivePayment', component: () => import('@/views/sales/ReceivePaymentView.vue'), meta: { titleKey: 'sales.payment.title' } },
        { path: 'sales/:id', name: 'salesOrderDetail', component: () => import('@/views/sales/SalesOrderDetailView.vue'), meta: { titleKey: 'sales.title' } },
        { path: 'purchasing', name: 'purchasing', component: () => import('@/views/purchasing/PurchaseOrdersView.vue'), meta: { titleKey: 'purchasing.title' } },
        { path: 'purchasing/new', name: 'purchaseOrderCreate', component: () => import('@/views/purchasing/PurchaseOrderCreateView.vue'), meta: { titleKey: 'purchasing.new' } },
        { path: 'purchasing/pay-supplier', name: 'purchasingPaySupplier', component: () => import('@/views/purchasing/SupplierPaymentView.vue'), meta: { titleKey: 'purchasing.payment.title' } },
        { path: 'purchasing/:id', name: 'purchaseOrderDetail', component: () => import('@/views/purchasing/PurchaseOrderDetailView.vue'), meta: { titleKey: 'purchasing.title' } },
        { path: 'inventory', name: 'inventory', component: () => import('@/views/inventory/InventoryView.vue'), meta: { titleKey: 'inventory.title' } },
        { path: 'inventory/stock-card', name: 'inventoryStockCard', component: () => import('@/views/inventory/StockCardView.vue'), meta: { titleKey: 'inventory.card.title' } },
        {
          path: 'accounting',
          component: () => import('@/views/accounting/AccountingView.vue'),
          meta: { titleKey: 'accounting.title' },
          children: [
            { path: '', name: 'accounting', redirect: { name: 'accountingTrialBalance' } },
            { path: 'trial-balance', name: 'accountingTrialBalance', component: () => import('@/views/accounting/TrialBalanceView.vue') },
            { path: 'profit-loss', name: 'accountingProfitLoss', component: () => import('@/views/accounting/ProfitLossView.vue') },
            { path: 'balance-sheet', name: 'accountingBalanceSheet', component: () => import('@/views/accounting/BalanceSheetView.vue') },
            { path: 'cash-flow', name: 'accountingCashFlow', component: () => import('@/views/accounting/CashFlowView.vue') },
            { path: 'general-ledger', name: 'accountingGeneralLedger', component: () => import('@/views/accounting/GeneralLedgerView.vue') },
            { path: 'vat', name: 'accountingVat', component: () => import('@/views/accounting/VatReportView.vue') },
            { path: 'periods', name: 'accountingPeriods', component: () => import('@/views/accounting/FiscalPeriodsView.vue') },
          ],
        },
        {
          path: 'master-data',
          component: () => import('@/views/masterdata/MasterDataView.vue'),
          meta: { titleKey: 'masterData.title' },
          children: [
            { path: '', name: 'masterData', redirect: { name: 'masterDataProducts' } },
            { path: 'products', name: 'masterDataProducts', component: () => import('@/views/masterdata/ProductsView.vue') },
            { path: 'customers', name: 'masterDataCustomers', component: () => import('@/views/masterdata/CustomersView.vue') },
            { path: 'suppliers', name: 'masterDataSuppliers', component: () => import('@/views/masterdata/SuppliersView.vue') },
            { path: 'warehouses', name: 'masterDataWarehouses', component: () => import('@/views/masterdata/WarehousesView.vue') },
            { path: 'units', name: 'masterDataUnits', component: () => import('@/views/masterdata/UnitsOfMeasureView.vue') },
            { path: 'categories', name: 'masterDataCategories', component: () => import('@/views/masterdata/CategoriesView.vue') },
            { path: 'tax-codes', name: 'masterDataTaxCodes', component: () => import('@/views/masterdata/TaxCodesView.vue') },
          ],
        },
        { path: 'expenses', name: 'expenses', component: () => import('@/views/ExpensesView.vue'), meta: { titleKey: 'expenses.title' } },
        { path: 'approvals', name: 'approvals', component: () => import('@/views/ApprovalsView.vue'), meta: { titleKey: 'approvals.title' } },
        { path: 'settings', name: 'settings', component: () => import('@/views/SettingsView.vue'), meta: { titleKey: 'nav.settings' } },
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
  if (to.name === 'login' && auth.isAuthenticated) {
    return { name: 'dashboard' }
  }
  return true
})

export default router

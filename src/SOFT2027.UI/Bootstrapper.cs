using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using SOFT2027.Core.Interfaces;
using SOFT2027.Data.Repositories;
using SOFT2027.Data.Services;
using SOFT2027.Presentation.ViewModels;

namespace SOFT2027.UI
{
    public interface IAppContainer : IServiceProvider
    {
        T GetRequiredService<T>() where T : class;
    }

    public static class ServiceProviderExtensions
    {
        public static T GetRequiredService<T>(this IServiceProvider provider) where T : class
        {
            ArgumentNullException.ThrowIfNull(provider);

            if (provider is IAppContainer appContainer)
            {
                return appContainer.GetRequiredService<T>();
            }

            var service = provider.GetService(typeof(T)) as T;
            if (service == null)
            {
                throw new InvalidOperationException($"No service registration found for type '{typeof(T).FullName}'.");
            }
            return service;
        }
    }

    public sealed class AppContainer : IAppContainer
    {
        private readonly Dictionary<Type, Func<IAppContainer, object>> _factories = new();
        private readonly ConcurrentDictionary<Type, object> _singletons = new();

        public void RegisterSingleton<TService>(Func<IAppContainer, TService> factory) where TService : class
        {
            _factories[typeof(TService)] = sp => _singletons.GetOrAdd(typeof(TService), _ => factory(sp));
        }

        public void RegisterTransient<TService>(Func<IAppContainer, TService> factory) where TService : class
        {
            _factories[typeof(TService)] = sp => factory(sp);
        }

        public object? GetService(Type serviceType)
        {
            if (_factories.TryGetValue(serviceType, out var factory))
            {
                return factory(this);
            }
            return null;
        }

        public T GetRequiredService<T>() where T : class
        {
            var service = GetService(typeof(T)) as T;
            if (service == null)
            {
                throw new InvalidOperationException($"No service registration found for type '{typeof(T).FullName}'.");
            }
            return service;
        }
    }

    public static class Bootstrapper
    {
        public static string GetDefaultDatabasePath()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appDir = Path.Combine(localAppData, "SOFT2027");
            if (!Directory.Exists(appDir))
            {
                Directory.CreateDirectory(appDir);
            }
            return Path.Combine(appDir, "soft2027.db");
        }

        public static IAppContainer CreateContainer(string? connectionString = null, string? schemaSqlPath = null)
        {
            var connStr = string.IsNullOrWhiteSpace(connectionString)
                ? $"Data Source={GetDefaultDatabasePath()};Foreign Keys=True;"
                : connectionString;

            var factory = new SqliteConnectionFactory(connStr);
            var resolvedSchemaPath = ResolveSchemaSqlPath(schemaSqlPath);
            var initializer = new DatabaseInitializer(factory, resolvedSchemaPath);

            return CreateContainer(factory, initializer);
        }

        public static IAppContainer CreateContainer(ISqliteConnectionFactory connectionFactory)
        {
            var resolvedSchemaPath = ResolveSchemaSqlPath(null);
            var initializer = new DatabaseInitializer(connectionFactory, resolvedSchemaPath);

            return CreateContainer(connectionFactory, initializer);
        }

        public static IAppContainer CreateContainer(ISqliteConnectionFactory connectionFactory, IDatabaseInitializer? initializer)
        {
            ArgumentNullException.ThrowIfNull(connectionFactory);

            var container = new AppContainer();

            // 1. Infrastructure
            container.RegisterSingleton<ISqliteConnectionFactory>(_ => connectionFactory);
            if (connectionFactory is SqliteConnectionFactory concreteFactory)
            {
                container.RegisterSingleton<SqliteConnectionFactory>(_ => concreteFactory);
            }

            if (initializer != null)
            {
                container.RegisterSingleton<IDatabaseInitializer>(_ => initializer);
                if (initializer is DatabaseInitializer concreteInitializer)
                {
                    container.RegisterSingleton<DatabaseInitializer>(_ => concreteInitializer);
                }
            }

            // 2. Repositories
            container.RegisterSingleton<ICustomerRepository>(sp => new CustomerRepository(sp.GetRequiredService<ISqliteConnectionFactory>()));
            container.RegisterSingleton<CustomerRepository>(sp => (CustomerRepository)sp.GetRequiredService<ICustomerRepository>());

            container.RegisterSingleton<ISupplierRepository>(sp => new SupplierRepository(sp.GetRequiredService<ISqliteConnectionFactory>()));
            container.RegisterSingleton<SupplierRepository>(sp => (SupplierRepository)sp.GetRequiredService<ISupplierRepository>());

            container.RegisterSingleton<IProductRepository>(sp => new ProductRepository(sp.GetRequiredService<ISqliteConnectionFactory>()));
            container.RegisterSingleton<ProductRepository>(sp => (ProductRepository)sp.GetRequiredService<IProductRepository>());

            container.RegisterSingleton<IAdminUserRepository>(sp => new AdminUserRepository(sp.GetRequiredService<ISqliteConnectionFactory>()));
            container.RegisterSingleton<AdminUserRepository>(sp => (AdminUserRepository)sp.GetRequiredService<IAdminUserRepository>());
            container.RegisterSingleton<IAdminSession>(_ => new AdminSession());
            container.RegisterSingleton<AdminSession>(sp => (AdminSession)sp.GetRequiredService<IAdminSession>());
            container.RegisterSingleton<IAuditLogRepository>(sp => new AuditLogRepository(sp.GetRequiredService<ISqliteConnectionFactory>()));
            container.RegisterSingleton<IBusinessLookupRepository>(sp => new BusinessLookupRepository(sp.GetRequiredService<ISqliteConnectionFactory>(), sp.GetRequiredService<IAdminSession>()));

            container.RegisterSingleton<ICustomerPricingHistoryRepository>(sp => new CustomerPricingHistoryRepository(sp.GetRequiredService<ISqliteConnectionFactory>()));
            container.RegisterSingleton<CustomerPricingHistoryRepository>(sp => (CustomerPricingHistoryRepository)sp.GetRequiredService<ICustomerPricingHistoryRepository>());

            container.RegisterSingleton<ISupplierPricingHistoryRepository>(sp => new SupplierPricingHistoryRepository(sp.GetRequiredService<ISqliteConnectionFactory>()));
            container.RegisterSingleton<SupplierPricingHistoryRepository>(sp => (SupplierPricingHistoryRepository)sp.GetRequiredService<ISupplierPricingHistoryRepository>());

            container.RegisterSingleton<ISalesRepository>(sp => new SalesRepository(sp.GetRequiredService<ISqliteConnectionFactory>()));
            container.RegisterSingleton<SalesRepository>(sp => (SalesRepository)sp.GetRequiredService<ISalesRepository>());

            container.RegisterSingleton<IPurchaseRepository>(sp => new PurchaseRepository(sp.GetRequiredService<ISqliteConnectionFactory>()));
            container.RegisterSingleton<PurchaseRepository>(sp => (PurchaseRepository)sp.GetRequiredService<IPurchaseRepository>());

            container.RegisterSingleton<IStockMovementRepository>(sp => new StockMovementRepository(sp.GetRequiredService<ISqliteConnectionFactory>()));
            container.RegisterSingleton<StockMovementRepository>(sp => (StockMovementRepository)sp.GetRequiredService<IStockMovementRepository>());

            container.RegisterSingleton<ICustomerLedgerRepository>(sp => new CustomerLedgerRepository(sp.GetRequiredService<ISqliteConnectionFactory>()));
            container.RegisterSingleton<CustomerLedgerRepository>(sp => (CustomerLedgerRepository)sp.GetRequiredService<ICustomerLedgerRepository>());

            container.RegisterSingleton<ISupplierLedgerRepository>(sp => new SupplierLedgerRepository(sp.GetRequiredService<ISqliteConnectionFactory>()));
            container.RegisterSingleton<SupplierLedgerRepository>(sp => (SupplierLedgerRepository)sp.GetRequiredService<ISupplierLedgerRepository>());

            container.RegisterSingleton<IReceiptRepository>(sp => new ReceiptRepository(sp.GetRequiredService<ISqliteConnectionFactory>()));
            container.RegisterSingleton<ReceiptRepository>(sp => (ReceiptRepository)sp.GetRequiredService<IReceiptRepository>());

            container.RegisterSingleton<IPaymentRepository>(sp => new PaymentRepository(sp.GetRequiredService<ISqliteConnectionFactory>()));
            container.RegisterSingleton<PaymentRepository>(sp => (PaymentRepository)sp.GetRequiredService<IPaymentRepository>());

            container.RegisterSingleton<IExpenseRepository>(sp => new ExpenseRepository(sp.GetRequiredService<ISqliteConnectionFactory>()));
            container.RegisterSingleton<ExpenseRepository>(sp => (ExpenseRepository)sp.GetRequiredService<IExpenseRepository>());

            container.RegisterSingleton<ICashTransactionRepository>(sp => new CashTransactionRepository(sp.GetRequiredService<ISqliteConnectionFactory>()));
            container.RegisterSingleton<CashTransactionRepository>(sp => (CashTransactionRepository)sp.GetRequiredService<ICashTransactionRepository>());

            container.RegisterSingleton<IBankTransactionRepository>(sp => new BankTransactionRepository(sp.GetRequiredService<ISqliteConnectionFactory>()));
            container.RegisterSingleton<BankTransactionRepository>(sp => (BankTransactionRepository)sp.GetRequiredService<IBankTransactionRepository>());

            container.RegisterSingleton<IReportRepository>(sp => new ReportRepository(sp.GetRequiredService<ISqliteConnectionFactory>()));
            container.RegisterSingleton<ReportRepository>(sp => (ReportRepository)sp.GetRequiredService<IReportRepository>());

            // 3. Coordinators
            container.RegisterSingleton<ITransactionCoordinator>(sp => new TransactionCoordinator(
                sp.GetRequiredService<ISqliteConnectionFactory>(),
                sp.GetRequiredService<ISalesRepository>(),
                sp.GetRequiredService<IPurchaseRepository>(),
                sp.GetRequiredService<IProductRepository>(),
                sp.GetRequiredService<IStockMovementRepository>(),
                sp.GetRequiredService<ICustomerRepository>(),
                sp.GetRequiredService<ISupplierRepository>(),
                sp.GetRequiredService<ICustomerLedgerRepository>(),
                sp.GetRequiredService<ISupplierLedgerRepository>(),
                sp.GetRequiredService<ICustomerPricingHistoryRepository>(),
                sp.GetRequiredService<ISupplierPricingHistoryRepository>(),
                sp.GetRequiredService<ICashTransactionRepository>(),
                sp.GetRequiredService<IBankTransactionRepository>()));
            container.RegisterSingleton<TransactionCoordinator>(sp => (TransactionCoordinator)sp.GetRequiredService<ITransactionCoordinator>());

            container.RegisterSingleton<IFinancialCoordinator>(sp => new FinancialCoordinator(
                sp.GetRequiredService<ISqliteConnectionFactory>(),
                sp.GetRequiredService<IReceiptRepository>(),
                sp.GetRequiredService<IPaymentRepository>(),
                sp.GetRequiredService<IExpenseRepository>(),
                sp.GetRequiredService<ICashTransactionRepository>(),
                sp.GetRequiredService<IBankTransactionRepository>(),
                sp.GetRequiredService<ICustomerRepository>(),
                sp.GetRequiredService<ISupplierRepository>(),
                sp.GetRequiredService<ICustomerLedgerRepository>(),
                sp.GetRequiredService<ISupplierLedgerRepository>()));
            container.RegisterSingleton<FinancialCoordinator>(sp => (FinancialCoordinator)sp.GetRequiredService<IFinancialCoordinator>());

            container.RegisterSingleton<IInventoryCoordinator>(sp => new InventoryCoordinator(
                sp.GetRequiredService<ISqliteConnectionFactory>(),
                sp.GetRequiredService<IProductRepository>(),
                sp.GetRequiredService<IStockMovementRepository>()));
            container.RegisterSingleton<InventoryCoordinator>(sp => (InventoryCoordinator)sp.GetRequiredService<IInventoryCoordinator>());
            container.RegisterSingleton<IDatabaseBackupService>(_ => new DatabaseBackupService(App.GetDatabasePath()));
            container.RegisterSingleton<DatabaseBackupService>(sp => (DatabaseBackupService)sp.GetRequiredService<IDatabaseBackupService>());

            // 4. ViewModels (Transient for fresh UI state per navigation)
            container.RegisterTransient(sp => new SalesEntryViewModel(
                sp.GetRequiredService<ITransactionCoordinator>(),
                sp.GetRequiredService<ICustomerRepository>(),
                sp.GetRequiredService<IProductRepository>(),
                sp.GetRequiredService<ICustomerPricingHistoryRepository>(),
                sp.GetRequiredService<IAdminSession>(),
                sp.GetRequiredService<ISalesRepository>()));

            container.RegisterTransient(sp => new PurchaseEntryViewModel(
                sp.GetRequiredService<ITransactionCoordinator>(),
                sp.GetRequiredService<ISupplierRepository>(),
                sp.GetRequiredService<IProductRepository>(),
                sp.GetRequiredService<ISupplierPricingHistoryRepository>(),
                sp.GetRequiredService<IAdminSession>(),
                sp.GetRequiredService<IPurchaseRepository>()));

            container.RegisterTransient(sp => new ReportsDashboardViewModel(
                sp.GetRequiredService<IReportRepository>(),
                sp.GetRequiredService<ICustomerRepository>(),
                sp.GetRequiredService<ISupplierRepository>(),
                sp.GetRequiredService<IProductRepository>(),
                sp.GetRequiredService<IBusinessLookupRepository>()));
            container.RegisterTransient(sp => new InventoryViewModel(
                sp.GetRequiredService<IProductRepository>(),
                sp.GetRequiredService<IStockMovementRepository>(),
                sp.GetRequiredService<IInventoryCoordinator>(),
                sp.GetRequiredService<IAdminSession>()));
            container.RegisterSingleton<Func<InventoryViewModel>>(sp => () => sp.GetRequiredService<InventoryViewModel>());
            container.RegisterTransient(sp => new CashBankViewModel(
                sp.GetRequiredService<ICashTransactionRepository>(),
                sp.GetRequiredService<IBankTransactionRepository>()));
            container.RegisterSingleton<Func<CashBankViewModel>>(sp => () => sp.GetRequiredService<CashBankViewModel>());
            container.RegisterTransient(sp => new BackupViewModel(sp.GetRequiredService<IDatabaseBackupService>()));
            container.RegisterSingleton<Func<BackupViewModel>>(sp => () => sp.GetRequiredService<BackupViewModel>());
            container.RegisterSingleton<Func<bool, ReturnEntryViewModel>>(sp => saleReturn => new ReturnEntryViewModel(
                saleReturn, sp.GetRequiredService<ISalesRepository>(), sp.GetRequiredService<IPurchaseRepository>(),
                sp.GetRequiredService<IProductRepository>(), sp.GetRequiredService<ITransactionCoordinator>(), sp.GetRequiredService<IAdminSession>()));
            container.RegisterSingleton<Func<bool, LedgerViewModel>>(sp => customerLedger => new LedgerViewModel(
                customerLedger, sp.GetRequiredService<ICustomerRepository>(), sp.GetRequiredService<ISupplierRepository>(),
                sp.GetRequiredService<ICustomerLedgerRepository>(), sp.GetRequiredService<ISupplierLedgerRepository>()));
            container.RegisterSingleton<Func<AdministrationViewModel>>(sp => () => new AdministrationViewModel(
                sp.GetRequiredService<IAdminUserRepository>(), sp.GetRequiredService<IAuditLogRepository>(), sp.GetRequiredService<IAdminSession>()));
            container.RegisterSingleton<Func<LookupManagementViewModel>>(sp => () => new LookupManagementViewModel(
                sp.GetRequiredService<IBusinessLookupRepository>()));
            container.RegisterSingleton<Func<string, FinancialEntryViewModel>>(sp => kind => new FinancialEntryViewModel(
                kind,
                sp.GetRequiredService<IFinancialCoordinator>(),
                sp.GetRequiredService<ICustomerRepository>(),
                sp.GetRequiredService<ISupplierRepository>(),
                sp.GetRequiredService<IExpenseRepository>(),
                sp.GetRequiredService<IAdminSession>()));
            container.RegisterSingleton<Func<string, MasterDataViewModel>>(sp => section => new MasterDataViewModel(
                section,
                sp.GetRequiredService<IProductRepository>(),
                sp.GetRequiredService<ICustomerRepository>(),
                sp.GetRequiredService<ISupplierRepository>(),
                sp.GetRequiredService<IBusinessLookupRepository>()));

            // 5. Delegate Factories for Navigation
            container.RegisterSingleton<Func<SalesEntryViewModel>>(sp => () => sp.GetRequiredService<SalesEntryViewModel>());
            container.RegisterSingleton<Func<PurchaseEntryViewModel>>(sp => () => sp.GetRequiredService<PurchaseEntryViewModel>());
            container.RegisterSingleton<Func<ReportsDashboardViewModel>>(sp => () => sp.GetRequiredService<ReportsDashboardViewModel>());

            // 6. Shell ViewModel (Transient)
            container.RegisterTransient(sp => new MainViewModel(
                sp.GetRequiredService<Func<SalesEntryViewModel>>(),
                sp.GetRequiredService<Func<ReportsDashboardViewModel>>(),
                sp.GetRequiredService<Func<string, MasterDataViewModel>>(),
                sp.GetRequiredService<Func<PurchaseEntryViewModel>>(),
                sp.GetRequiredService<IAdminUserRepository>(),
                sp.GetRequiredService<IAdminSession>(),
                sp.GetRequiredService<Func<string, FinancialEntryViewModel>>(),
                sp.GetRequiredService<Func<InventoryViewModel>>(),
                sp.GetRequiredService<Func<CashBankViewModel>>(),
                sp.GetRequiredService<Func<BackupViewModel>>(),
                sp.GetRequiredService<Func<bool, ReturnEntryViewModel>>(),
                sp.GetRequiredService<Func<bool, LedgerViewModel>>(),
                sp.GetRequiredService<Func<AdministrationViewModel>>(),
                sp.GetRequiredService<Func<LookupManagementViewModel>>()));

            return container;
        }

        public static string ResolveSchemaSqlPath(string? explicitPath = null)
        {
            if (!string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath))
            {
                return explicitPath;
            }

            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SOFT2027");

            string[] candidates = new[]
            {
                // Production published paths
                Path.Combine(baseDir, "docs", "DATABASE_SCHEMA.sql"),
                Path.Combine(baseDir, "DATABASE_SCHEMA.sql"),
                // AppData paths
                Path.Combine(appDataFolder, "docs", "DATABASE_SCHEMA.sql"),
                Path.Combine(appDataFolder, "DATABASE_SCHEMA.sql"),
                // Development / test directory traversing paths
                Path.Combine(baseDir, "..", "..", "..", "..", "..", "docs", "DATABASE_SCHEMA.sql"),
                Path.Combine(baseDir, "..", "..", "..", "..", "docs", "DATABASE_SCHEMA.sql"),
                Path.Combine(baseDir, "..", "..", "..", "docs", "DATABASE_SCHEMA.sql"),
                Path.Combine(Directory.GetCurrentDirectory(), "docs", "DATABASE_SCHEMA.sql"),
                Path.Combine(Directory.GetCurrentDirectory(), "DATABASE_SCHEMA.sql")
            };

            foreach (var candidate in candidates)
            {
                try
                {
                    var fullPath = Path.GetFullPath(candidate);
                    if (File.Exists(fullPath))
                    {
                        return fullPath;
                    }
                }
                catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
                {
                    // Candidate path format is not viable on this platform; continue searching
                }
            }

            throw new FileNotFoundException("Could not locate the database schema file (DATABASE_SCHEMA.sql) in any expected directory.");
        }
    }
}

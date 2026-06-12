using System;
using System.Reflection;
using CapitalUniversity.Modules.Payments.Application.Treasury;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Payments;

public class SettlementServiceIdempotencyTests
{
    [Theory]
    [InlineData(2627, "Violation of UNIQUE KEY constraint 'IX_TreasuryPaymentTransactions_MerchantOrderId_IdempotencyKey'.")]
    [InlineData(2601, "Cannot insert duplicate key row in object 'dbo.Payments' with unique index 'IX_Payments_FeeId'.")]
    public void IsUniqueViolation_True_ForIdempotencyIndexes(int number, string message)
    {
        var sqlEx = CreateSqlException(number, message);
        var dbEx = new DbUpdateException("db", sqlEx);

        var result = InvokeIsUniqueViolation(dbEx);

        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(2627, "Violation of UNIQUE KEY constraint 'IX_Orders_MerchantOrderId'.")]
    [InlineData(547, "The INSERT statement conflicted with the FOREIGN KEY constraint.")]
    public void IsUniqueViolation_False_ForUnrelatedViolations(int number, string message)
    {
        var sqlEx = CreateSqlException(number, message);
        var dbEx = new DbUpdateException("db", sqlEx);

        var result = InvokeIsUniqueViolation(dbEx);

        result.Should().BeFalse();
    }

    private static bool InvokeIsUniqueViolation(DbUpdateException ex)
    {
        var method = typeof(SettlementService).GetMethod("IsUniqueViolation", BindingFlags.NonPublic | BindingFlags.Static);
        if (method == null) throw new InvalidOperationException("Could not find private static method IsUniqueViolation on SettlementService.");
        return (bool)method.Invoke(null, new object[] { ex });
    }

    private static SqlException CreateSqlException(int number, string message)
    {
        // SqlException and its constituents are internal/protected and hard to instantiate directly.
        // We use reflection to build the necessary state for testing provider-specific logic.
        
        var collection = (SqlErrorCollection)Activator.CreateInstance(typeof(SqlErrorCollection), true);
        
        // Constructor: internal SqlError(int number, byte state, byte errorClass, string server, string message, string procedure, int lineNumber, Exception innerException)
        var error = (SqlError)Activator.CreateInstance(typeof(SqlError), 
            BindingFlags.NonPublic | BindingFlags.Instance, null, 
            new object[] { number, (byte)0, (byte)0, "server", message, "proc", 0, null }, null);
        
        var addMethod = typeof(SqlErrorCollection).GetMethod("Add", BindingFlags.NonPublic | BindingFlags.Instance);
        addMethod.Invoke(collection, new object[] { error });

        // Constructor: internal SqlException(string message, SqlErrorCollection errorCollection, Exception innerException, Guid conId)
        return (SqlException)Activator.CreateInstance(typeof(SqlException), 
            BindingFlags.NonPublic | BindingFlags.Instance, null, 
            new object[] { message, collection, null, Guid.NewGuid() }, null);
    }
}

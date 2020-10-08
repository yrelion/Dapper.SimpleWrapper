using Dapper.SimpleWrapper.Abstractions;
using System;
using System.Data;
using System.Runtime.CompilerServices;
using System.Transactions;

namespace Dapper.SimpleWrapper.Common
{
    public class DbContext<TSettings> : DatabaseContext<TSettings> where TSettings : IDatabaseSettings
    {
        public DbContext(IConnectionFactory<TSettings> factory) : base(factory) { }

        /// <summary>
        /// Begins an <see cref="IDbTransaction"/>, if not already present, and associates it with this method's caller as
        /// the <see cref="ITransactionContext"/> originator
        /// </summary>
        /// <param name="actionOriginator">The method caller to associate the <see cref="ITransactionContext"/> with</param>
        /// <returns>An <see cref="ITransactionContext"/></returns>
        public override ITransactionContext BeginTransaction([CallerMemberName] string actionOriginator = null)
        {
            if (Connection.State == ConnectionState.Closed)
            {
                Connection.Open();

                TransactionContext.Transaction = Connection.BeginTransaction();
                TransactionContext.OriginatorName = actionOriginator;
            }

            return TransactionContext;
        }

        /// <summary>
        /// Attempts to commit the <see cref="IDbTransaction"/> whose originator matches with the method's caller
        /// </summary>
        /// <param name="suppressOriginator">An optional flag to suppress the <see cref="ITransactionContext"/>
        /// originator check and force transaction action</param>
        /// <param name="actionOriginator">The method caller to verify the <see cref="ITransactionContext"/> against</param>
        /// <returns>The <see cref="ITransactionContext"/>'s <see cref="TransactionStatus"/></returns>
        public override TransactionStatus TryCommitTransaction(bool suppressOriginator = false, [CallerMemberName] string actionOriginator = null)
        {
            if (!suppressOriginator && !VerifyOriginatorIsFinalizer(actionOriginator))
                return TransactionContext.Status;

            try
            {
                TransactionContext.Transaction.Commit();
                TransactionContext.Status = TransactionStatus.Committed;
            }
            catch (Exception e)
            {
                TransactionContext.Transaction.Rollback();
                TransactionContext.Status = TransactionStatus.Aborted;
                TransactionContext.Errors.Add(e.Message);
            }
            finally
            {
                PostTransactionCleanup();
            }

            return TransactionContext.Status;
        }

        /// <summary>
        /// Attempts to rollback the <see cref="IDbTransaction"/> whose originator matches with the method's caller
        /// </summary>
        /// <param name="suppressOriginator">An optional flag to suppress the <see cref="ITransactionContext"/>
        /// originator check and force the transaction action</param>
        /// <param name="actionOriginator">The method caller to verify the <see cref="ITransactionContext"/> against</param>
        /// <returns>The <see cref="ITransactionContext"/>'s <see cref="TransactionStatus"/></returns>
        public override TransactionStatus TryRollbackTransaction(bool suppressOriginator = false, [CallerMemberName] string actionOriginator = null)
        {
            if (!suppressOriginator && !VerifyOriginatorIsFinalizer(actionOriginator))
                return TransactionContext.Status;

            TransactionContext.Transaction.Rollback();
            TransactionContext.Status = TransactionStatus.Aborted;
            PostTransactionCleanup();

            return TransactionContext.Status;
        }

        /// <summary>
        /// Runs post transaction actions to reset contextual information
        /// </summary>
        protected void PostTransactionCleanup()
        {
            TransactionContext.OriginatorName = string.Empty;
        }

        /// <summary>
        /// Checks whether the provided caller name matches with the <see cref="ITransactionContext.OriginatorName"/>
        /// </summary>
        /// <param name="transactionFinalizerName">The caller name who attempts to finalize the action</param>
        protected bool VerifyOriginatorIsFinalizer(string transactionFinalizerName)
        {
            return TransactionContext.OriginatorName == transactionFinalizerName;
        }
    }
}

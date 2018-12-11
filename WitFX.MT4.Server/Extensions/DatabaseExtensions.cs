using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using WitFX.Backend.Extensions;
using WitFX.Backend.Helpers;
using WitFX.Backend.Services;

namespace WitFX.MT4.Server.Extensions
{
    public static class DatabaseExtensions
    {
        public static async Task<T?> ExecuteValueOrNullAsync<T>(
            this DatabaseService database, string query, Func<MySqlDataReader, T> factory,
            CancellationToken cancellationToken)
            where T : struct
        {
            using (
                var conn = await database.OpenMainConnectionAsync(
                    cancellationToken).ConfigureAwait(false))
                return await conn.ExecuteRecordOptionalAsync(
                    query, factory, cancellationToken).ConfigureAwait(false);
        }

        public static async Task<T> ExecuteRecordOrNullAsync<T>(
            this DatabaseService database, string query, Func<MySqlDataReader, T> factory,
            CancellationToken cancellationToken)
            where T : class
        {
            using (
                var conn = await database.OpenMainConnectionAsync(
                    cancellationToken).ConfigureAwait(false))
                return await conn.ExecuteRecordOptionalAsync(
                    query, factory, cancellationToken).ConfigureAwait(false);
        }

        public static async Task<T> ExecuteRecordRequredAsync<T>(
            this DatabaseService database, string query, Func<MySqlDataReader, T> factory,
            CancellationToken cancellationToken)
        {
            using (
                var conn = await database.OpenMainConnectionAsync(
                    cancellationToken).ConfigureAwait(false))
                return await conn.ExecuteRecordRequredAsync(
                    query, factory, cancellationToken).ConfigureAwait(false);
        }

        public static async Task<IReadOnlyList<T>> ExecuteRecordsAsync<T>(
            this DatabaseService database, string query, Func<MySqlDataReader, T> factory,
            CancellationToken cancellationToken)
        {
            using (
                var conn = await database.OpenMainConnectionAsync(
                    cancellationToken).ConfigureAwait(false))
                return await conn.ExecuteRecordsAsync(
                    query, factory, cancellationToken).ConfigureAwait(false);
        }

        public static async Task ExecuteEachRecordsAsync(
            this DatabaseService database, string query, Action<MySqlDataReader> iterator,
            CancellationToken cancellationToken)
        {
            using (
                var conn = await database.OpenMainConnectionAsync(
                    cancellationToken).ConfigureAwait(false))
                await conn.ExecuteEachRecordsAsync(
                    query, iterator, cancellationToken).ConfigureAwait(false);
        }

        public static async Task ExecuteNonQueryAsync(
            this DatabaseService database, string query, CancellationToken cancellationToken)
        {
            using (
                var conn = await database.OpenMainConnectionAsync(
                    cancellationToken).ConfigureAwait(false))
                await conn.ExecuteNonQueryAsync(
                    query, cancellationToken).ConfigureAwait(false);
        }

        public static async Task ExecuteInsertAsync(
            this DatabaseService database, string tableName, IReadOnlyCollection<(string, string)> fields, CancellationToken cancellationToken)
        {
            using (
                var conn = await database.OpenMainConnectionAsync(
                    cancellationToken).ConfigureAwait(false))
                await conn.ExecuteNonQueryAsync(
                    DatabaseHelper.BuildInsertQuery(tableName, fields),
                    cancellationToken).ConfigureAwait(false);
        }

        //internal static Task<int> GetAutoIncrementAsync(
        //    this DatabaseService database, string tableName, CancellationToken cancellationToken)
        //{
        //    return database.ExecuteRecordRequredAsync(
        //       $"SELECT `AUTO_INCREMENT` FROM  INFORMATION_SCHEMA.TABLES" +
        //       $" WHERE TABLE_SCHEMA = 'social_trading'" +
        //       $" AND TABLE_NAME = '{tableName}'; ",
        //       reader => reader.GetInt32(0), cancellationToken);
        //}
    }
}

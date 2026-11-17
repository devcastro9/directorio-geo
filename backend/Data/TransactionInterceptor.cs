using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System;
using System.Data;
using System.Data.Common;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace backend.Data
{
    public class TransactionInterceptor(IHttpContextAccessor httpContextAccessor) : DbTransactionInterceptor
    {
        public override async ValueTask<DbTransaction> TransactionStartedAsync(DbConnection connection, TransactionEndEventData eventData, DbTransaction result, CancellationToken cancellationToken = default)
        {
            await SetActorAsync(connection, result, cancellationToken);
            return result;
        }

        public override DbTransaction TransactionStarted(DbConnection connection, TransactionEndEventData eventData, DbTransaction result)
        {
            SetActor(connection, result);
            return result;
        }

        private async Task SetActorAsync(DbConnection connection, DbTransaction transaction, CancellationToken ct)
        {
            if (!TryGetActorId(out Guid actorId)) return;

            await using DbCommand cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = "SELECT set_config('cofre.actor_id', @actor_id, true);";
            AddActorParameter(cmd, actorId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        private void SetActor(DbConnection connection, DbTransaction transaction)
        {
            if (!TryGetActorId(out Guid actorId)) return;

            using DbCommand cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = "SELECT set_config('cofre.actor_id', @actor_id, true);";
            AddActorParameter(cmd, actorId);
            cmd.ExecuteNonQuery();
        }

        private bool TryGetActorId(out Guid actorId)
        {
            actorId = Guid.Empty;
            ClaimsPrincipal? user = httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true) return false;

            string? rawId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
            return Guid.TryParse(rawId, out actorId) && actorId != Guid.Empty;
        }

        private static void AddActorParameter(DbCommand cmd, Guid actorId)
        {
            DbParameter param = cmd.CreateParameter();
            param.ParameterName = "actor_id";
            param.DbType = DbType.String;
            param.Value = actorId.ToString();
            cmd.Parameters.Add(param);
        }
    }
}

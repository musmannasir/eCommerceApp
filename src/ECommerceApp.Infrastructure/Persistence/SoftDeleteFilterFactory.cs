using System.Linq.Expressions;
using ECommerceApp.Domain.Common;

namespace ECommerceApp.Infrastructure.Persistence;

/// <summary>
/// Builds the <c>e =&gt; !e.IsDeleted</c> global query filter expression for a
/// given entity CLR type, needed because EF Core's <c>HasQueryFilter</c> requires
/// a filter typed to the exact entity, not the shared <see cref="ISoftDeletable"/>
/// interface.
/// </summary>
internal static class SoftDeleteFilterFactory
{
    public static LambdaExpression Build(Type entityType)
    {
        var parameter = Expression.Parameter(entityType, "e");
        var property = Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
        var notDeleted = Expression.Not(property);
        return Expression.Lambda(notDeleted, parameter);
    }
}

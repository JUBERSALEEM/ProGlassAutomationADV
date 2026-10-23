using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Licensing;

namespace ProGlassAutomation.Services.Production
{
    /// <summary>
    /// Authorization boundary shared by public production workflows and the
    /// production DbContext's SaveChanges path. Every production mutation
    /// requires an active signed-in identity, Production.Edit, and a writable
    /// license session.
    /// </summary>
    public static class ProductionWriteGuard
    {
        public static void Demand(string operation)
        {
            OperationGuard.Demand(AppPermissions.ProductionEdit, operation);
            ReadOnlyGate.Demand(operation);
        }
    }
}
